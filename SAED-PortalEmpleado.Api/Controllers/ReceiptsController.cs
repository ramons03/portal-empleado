using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SAED_PortalEmpleado.Application.Common.Interfaces;

namespace SAED_PortalEmpleado.Api.Controllers;

[ApiController]
[Route("api/receipts")]
[Authorize]
public class ReceiptsController : ControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IReceiptCatalogCache _receiptCatalogCache;
    private readonly IReceiptSourceAws _receiptSourceAws;
    private readonly IReceiptPeriodLock _receiptPeriodLock;
    private readonly ILogger<ReceiptsController> _logger;

    public ReceiptsController(
        ICurrentUserService currentUserService,
        IEmployeeRepository employeeRepository,
        IReceiptCatalogCache receiptCatalogCache,
        IReceiptSourceAws receiptSourceAws,
        IReceiptPeriodLock receiptPeriodLock,
        ILogger<ReceiptsController> logger)
    {
        _currentUserService = currentUserService;
        _employeeRepository = employeeRepository;
        _receiptCatalogCache = receiptCatalogCache;
        _receiptSourceAws = receiptSourceAws;
        _receiptPeriodLock = receiptPeriodLock;
        _logger = logger;
    }

    [HttpGet("years")]
    public async Task<IActionResult> GetYears()
    {
        var (cuil, error) = await ResolveCurrentCuilAsync(HttpContext.RequestAborted);
        if (error is not null)
        {
            return error;
        }

        var years = await _receiptCatalogCache.GetAvailableYearsAsync(cuil!, HttpContext.RequestAborted);
        return Ok(new ReceiptYearsResponse(years));
    }

    [HttpGet("{year:int}/months")]
    public async Task<IActionResult> GetMonths(int year)
    {
        if (!IsValidYear(year))
        {
            return BadRequest(new { message = "Año fuera de rango permitido." });
        }

        var (cuil, error) = await ResolveCurrentCuilAsync(HttpContext.RequestAborted);
        if (error is not null)
        {
            return error;
        }

        var months = await _receiptCatalogCache.GetAvailableMonthsAsync(cuil!, year, HttpContext.RequestAborted);
        return Ok(new ReceiptMonthsResponse(year, months));
    }

    [HttpGet("{year:int}/{month:int}")]
    public async Task<IActionResult> GetLatestByPeriod(int year, int month)
    {
        if (!IsValidYearMonth(year, month))
        {
            return BadRequest(new { message = "Periodo inválido. Verifique año y mes." });
        }

        var (cuil, error) = await ResolveCurrentCuilAsync(HttpContext.RequestAborted);
        if (error is not null)
        {
            return error;
        }

        var currentCuil = cuil!;
        var current = await _receiptCatalogCache.GetLatestAsync(currentCuil, year, month, HttpContext.RequestAborted);
        if (current is not null)
        {
            _logger.LogInformation(
                "Receipt cache hit. Cuil={Cuil} Period={Year}-{Month} Version={Version}",
                currentCuil,
                year,
                month,
                current.Version);
            return Ok(ToSnapshotResponse(current, "cache_hit"));
        }

        await using var periodLock = await _receiptPeriodLock.AcquireAsync(currentCuil, year, month, HttpContext.RequestAborted);
        current = await _receiptCatalogCache.GetLatestAsync(currentCuil, year, month, HttpContext.RequestAborted);
        if (current is not null)
        {
            return Ok(ToSnapshotResponse(current, "cache_hit_after_wait"));
        }

        var fetch = await _receiptSourceAws.FetchPeriodAsync(
            new ReceiptSourceFetchRequest(currentCuil, year, month, KnownEtag: null, KnownVersionId: null),
            HttpContext.RequestAborted);

        if (fetch.Status == ReceiptSourceFetchStatus.SourceDisabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "La fuente AWS para recibos está deshabilitada."
            });
        }

        if (fetch.Status == ReceiptSourceFetchStatus.NotFound)
        {
            return NotFound(new { message = "No se encontró recibo en AWS para el periodo solicitado." });
        }

        if (fetch.Status != ReceiptSourceFetchStatus.Downloaded || string.IsNullOrWhiteSpace(fetch.PayloadJson))
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "No se pudo obtener una respuesta válida desde la fuente de recibos."
            });
        }

        var saved = await _receiptCatalogCache.SaveNewVersionAsync(
            new ReceiptCacheWriteRequest(
                currentCuil,
                year,
                month,
                fetch.DownloadedAtUtc ?? DateTime.UtcNow,
                fetch.SourceKey,
                fetch.SourceEtag,
                fetch.SourceVersionId,
                fetch.PayloadJson),
            HttpContext.RequestAborted);

        _logger.LogInformation(
            "Receipt cache refresh on miss. Cuil={Cuil} Period={Year}-{Month} Version={Version} SourceKey={SourceKey}",
            currentCuil,
            year,
            month,
            saved.Version,
            saved.SourceKey);

        return Ok(ToSnapshotResponse(saved, "downloaded_on_miss"));
    }

    [HttpPost("{year:int}/{month:int}/refresh")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshPeriod(int year, int month)
    {
        if (!IsValidYearMonth(year, month))
        {
            return BadRequest(new { message = "Periodo inválido. Verifique año y mes." });
        }

        var (cuil, error) = await ResolveCurrentCuilAsync(HttpContext.RequestAborted);
        if (error is not null)
        {
            return error;
        }

        var currentCuil = cuil!;
        await using var periodLock = await _receiptPeriodLock.AcquireAsync(currentCuil, year, month, HttpContext.RequestAborted);
        var current = await _receiptCatalogCache.GetLatestAsync(currentCuil, year, month, HttpContext.RequestAborted);

        var fetch = await _receiptSourceAws.FetchPeriodAsync(
            new ReceiptSourceFetchRequest(currentCuil, year, month, current?.SourceEtag, current?.SourceVersionId),
            HttpContext.RequestAborted);

        if (fetch.Status == ReceiptSourceFetchStatus.SourceDisabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "La fuente AWS para recibos está deshabilitada."
            });
        }

        if (fetch.Status == ReceiptSourceFetchStatus.NotFound)
        {
            if (current is null)
            {
                return NotFound(new { message = "No existe cache local ni archivo remoto para este periodo." });
            }

            return Ok(new ReceiptRefreshResponse(
                Refreshed: false,
                Status: "remote_not_found",
                Snapshot: ToSnapshotResponse(current, "cache_fallback")));
        }

        if (fetch.Status == ReceiptSourceFetchStatus.NotModified)
        {
            if (current is null)
            {
                return NotFound(new { message = "No hay cache local para este periodo y AWS no reportó cambios." });
            }

            _logger.LogInformation(
                "Receipt refresh skipped (not modified). Cuil={Cuil} Period={Year}-{Month} Version={Version}",
                currentCuil,
                year,
                month,
                current.Version);

            return Ok(new ReceiptRefreshResponse(
                Refreshed: false,
                Status: "not_modified",
                Snapshot: ToSnapshotResponse(current, "not_modified")));
        }

        if (fetch.Status != ReceiptSourceFetchStatus.Downloaded || string.IsNullOrWhiteSpace(fetch.PayloadJson))
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "No se pudo descargar el payload de AWS."
            });
        }

        var saved = await _receiptCatalogCache.SaveNewVersionAsync(
            new ReceiptCacheWriteRequest(
                currentCuil,
                year,
                month,
                fetch.DownloadedAtUtc ?? DateTime.UtcNow,
                fetch.SourceKey,
                fetch.SourceEtag,
                fetch.SourceVersionId,
                fetch.PayloadJson),
            HttpContext.RequestAborted);

        _logger.LogInformation(
            "Receipt refresh persisted. Cuil={Cuil} Period={Year}-{Month} Version={Version} SourceKey={SourceKey}",
            currentCuil,
            year,
            month,
            saved.Version,
            saved.SourceKey);

        return Ok(new ReceiptRefreshResponse(
            Refreshed: true,
            Status: "downloaded",
            Snapshot: ToSnapshotResponse(saved, "downloaded")));
    }

    private async Task<(string? Cuil, IActionResult? Error)> ResolveCurrentCuilAsync(CancellationToken cancellationToken)
    {
        var googleSub = _currentUserService.GoogleSub;
        if (string.IsNullOrWhiteSpace(googleSub))
        {
            return (null, Unauthorized());
        }

        var employee = await _employeeRepository.GetByGoogleSubAsync(googleSub, cancellationToken);
        if (employee is null)
        {
            return (null, NotFound(new { message = "Employee not found." }));
        }

        if (string.IsNullOrWhiteSpace(employee.Cuil))
        {
            return (null, BadRequest(new { message = "CUIL no configurado para el usuario." }));
        }

        return (employee.Cuil, null);
    }

    private static bool IsValidYearMonth(int year, int month)
    {
        return IsValidYear(year) && month is >= 1 and <= 12;
    }

    private static bool IsValidYear(int year)
    {
        return year is >= 2000 and <= 2100;
    }

    private static ReceiptSnapshotResponse ToSnapshotResponse(ReceiptCacheEntry entry, string status)
    {
        return new ReceiptSnapshotResponse(
            entry.Cuil,
            entry.Year,
            entry.Month,
            entry.Version,
            entry.DownloadedAtUtc,
            entry.SourceKey,
            entry.SourceEtag,
            entry.SourceVersionId,
            status,
            TryBuildReceiptItem(entry.PayloadJson, entry.Year, entry.Month),
            entry.PayloadJson);
    }

    private static ReceiptItemDto? TryBuildReceiptItem(string payloadJson, int year, int month)
    {
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;

            var id = GetString(root, "ID")
                ?? GetString(root, "Id")
                ?? $"{year:D4}-{month:D2}";

            var periodo = GetString(root, "Periodo")
                ?? $"{CultureInfo.GetCultureInfo("es-AR").DateTimeFormat.GetMonthName(month)} {year}";

            var importe = GetDecimal(root, "Liquido")
                ?? GetDecimal(root, "Importe")
                ?? 0m;

            var moneda = GetString(root, "Moneda") ?? "ARS";
            var estado = GetString(root, "Estado") ?? "Emitido";
            var fechaEmision = GetDateTime(root, "FechaEmision")
                ?? new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);

            var pdfUrl = $"/api/recibo-sueldo/{Uri.EscapeDataString(id)}/pdf";

            return new ReceiptItemDto(
                id,
                periodo,
                importe,
                moneda,
                estado,
                fechaEmision,
                pdfUrl);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            _ => element.ToString()
        };
    }

    private static decimal? GetDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var numeric))
        {
            return numeric;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString();
            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariant))
            {
                return invariant;
            }

            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("es-AR"), out var local))
            {
                return local;
            }
        }

        return null;
    }

    private static DateTime? GetDateTime(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString();
            if (DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedInvariant))
            {
                return parsedInvariant;
            }

            if (DateTime.TryParse(
                text,
                CultureInfo.GetCultureInfo("es-AR"),
                DateTimeStyles.AssumeLocal,
                out var parsedLocal))
            {
                return parsedLocal.ToUniversalTime();
            }
        }

        return null;
    }
}

public sealed record ReceiptYearsResponse(IReadOnlyList<int> Years);

public sealed record ReceiptMonthsResponse(int Year, IReadOnlyList<int> Months);

public sealed record ReceiptSnapshotResponse(
    string Cuil,
    int Year,
    int Month,
    int Version,
    DateTime DownloadedAtUtc,
    string SourceKey,
    string? SourceEtag,
    string? SourceVersionId,
    string Status,
    ReceiptItemDto? Item,
    string PayloadJson);

public sealed record ReceiptRefreshResponse(
    bool Refreshed,
    string Status,
    ReceiptSnapshotResponse Snapshot);

public sealed record ReceiptItemDto(
    string Id,
    string Periodo,
    decimal Importe,
    string Moneda,
    string Estado,
    DateTime FechaEmision,
    string? PdfUrl);
