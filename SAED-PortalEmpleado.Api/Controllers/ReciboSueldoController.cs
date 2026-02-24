using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAED_PortalEmpleado.Api.Services;
using SAED_PortalEmpleado.Application.Common.Interfaces;
using SAED_PortalEmpleado.Domain.Entities;
using SAED_PortalEmpleado.Infrastructure.Persistence;

namespace SAED_PortalEmpleado.Api.Controllers;

[ApiController]
[Route("api/recibo-sueldo")]
[Authorize]
public class ReciboSueldoController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IConfiguration _configuration;
    private readonly IReciboSueldoJsonService _reciboSueldoJsonService;
    private readonly IReciboPdfService _reciboPdfService;

    public ReciboSueldoController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IConfiguration configuration,
        IReciboSueldoJsonService reciboSueldoJsonService,
        IReciboPdfService reciboPdfService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _configuration = configuration;
        _reciboSueldoJsonService = reciboSueldoJsonService;
        _reciboPdfService = reciboPdfService;
    }

    /// <summary>
    /// Returns payroll receipt documents for the current user by CUIL.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetReciboSueldo()
    {
        var googleSub = _currentUserService.GoogleSub;
        if (string.IsNullOrWhiteSpace(googleSub))
        {
            return Unauthorized();
        }

        var employee = await _context.Employees.SingleOrDefaultAsync(e => e.GoogleSub == googleSub);
        if (employee == null)
        {
            return NotFound("Employee not found");
        }

        if (string.IsNullOrWhiteSpace(employee.Cuil))
        {
            return BadRequest(new { message = "CUIL no configurado para el usuario." });
        }

        ReciboItem[] items;
        if (ShouldUseMockReciboSueldo())
        {
            items = BuildMockReciboSueldo();
        }
        else
        {
            var reciboSueldoItems = await _reciboSueldoJsonService.GetReciboSueldoForCuilAsync(employee.Cuil, HttpContext.RequestAborted);
            items = reciboSueldoItems
                .Select(r => new ReciboItem(
                    r.Id,
                    r.Periodo,
                    r.Importe,
                    r.Moneda,
                    r.Estado,
                    r.FechaEmision,
                    $"/api/recibo-sueldo/{Uri.EscapeDataString(r.Id)}/pdf",
                    r.Establecimiento,
                    r.Cargo,
                    r.FechaIngreso,
                    r.DiasTrabajados))
                .ToArray();
        }

        var response = new ReciboSueldoResponse(employee.Cuil, items);
        return Ok(response);
    }

    /// <summary>
    /// Returns one receipt document as PDF for the current user.
    /// </summary>
    [HttpGet("{reciboId}/pdf")]
    public async Task<IActionResult> GetReciboPdf(string reciboId, [FromQuery] bool download = false)
    {
        var googleSub = _currentUserService.GoogleSub;
        if (string.IsNullOrWhiteSpace(googleSub))
        {
            return Unauthorized();
        }

        var employee = await _context.Employees.SingleOrDefaultAsync(e => e.GoogleSub == googleSub);
        if (employee == null)
        {
            return NotFound("Employee not found");
        }

        if (string.IsNullOrWhiteSpace(employee.Cuil))
        {
            return BadRequest(new { message = "CUIL no configurado para el usuario." });
        }

        if (ShouldUseMockReciboSueldo())
        {
            return NotFound(new { message = "PDF no disponible cuando ReciboSueldo:MockData=true." });
        }

        var recibo = await _reciboSueldoJsonService.GetReciboByIdForCuilAsync(employee.Cuil, reciboId, HttpContext.RequestAborted);
        if (recibo is null)
        {
            return NotFound(new { message = "Recibo no encontrado o fuera del rango permitido (12 meses)." });
        }

        var pdfBytes = await _reciboPdfService.BuildPdfAsync(recibo, HttpContext.RequestAborted);
        if (download)
        {
            var fileName = $"recibo-sueldo-{recibo.Id}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        return File(pdfBytes, "application/pdf");
    }

    /// <summary>
    /// Returns complete detail for one payroll receipt by cargo.
    /// </summary>
    [HttpGet("{reciboId}")]
    public async Task<IActionResult> GetReciboDetalle(string reciboId)
    {
        var googleSub = _currentUserService.GoogleSub;
        if (string.IsNullOrWhiteSpace(googleSub))
        {
            return Unauthorized();
        }

        var employee = await _context.Employees.SingleOrDefaultAsync(e => e.GoogleSub == googleSub);
        if (employee == null)
        {
            return NotFound("Employee not found");
        }

        if (string.IsNullOrWhiteSpace(employee.Cuil))
        {
            return BadRequest(new { message = "CUIL no configurado para el usuario." });
        }

        if (ShouldUseMockReciboSueldo())
        {
            return NotFound(new { message = "Detalle no disponible cuando ReciboSueldo:MockData=true." });
        }

        var recibo = await _reciboSueldoJsonService.GetReciboByIdForCuilAsync(employee.Cuil, reciboId, HttpContext.RequestAborted);
        if (recibo is null || string.IsNullOrWhiteSpace(recibo.SourceJson))
        {
            return NotFound(new { message = "Recibo no encontrado." });
        }

        var (haberes, descuentos) = ParseConceptTables(recibo.SourceJson, recibo.CargoIndex);
        var response = new ReciboDetalleResponse(
            recibo.Id,
            recibo.Establecimiento,
            recibo.Cargo,
            recibo.Periodo,
            recibo.FechaEmision,
            recibo.FechaIngreso,
            recibo.DiasTrabajados,
            recibo.Bruto ?? 0m,
            recibo.TotalDescuentos ?? 0m,
            recibo.Importe,
            recibo.LiquidoPalabras,
            haberes,
            descuentos,
            $"/api/recibo-sueldo/{Uri.EscapeDataString(recibo.Id)}/pdf");

        return Ok(response);
    }

    /// <summary>
    /// Registers a view/open event for recibo-sueldo analytics.
    /// </summary>
    [HttpPost("view")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogView([FromBody] ReciboSueldoViewRequest request)
    {
        var googleSub = _currentUserService.GoogleSub;
        if (string.IsNullOrWhiteSpace(googleSub))
        {
            return Unauthorized();
        }

        var employee = await _context.Employees.SingleOrDefaultAsync(e => e.GoogleSub == googleSub);
        if (employee == null)
        {
            return NotFound("Employee not found");
        }

        var action = string.IsNullOrWhiteSpace(request.Action) ? "page" : request.Action;
        if (action != "page" && action != "open")
        {
            return BadRequest(new { message = "Acción inválida." });
        }

        var viewEvent = new ReciboViewEvent
        {
            Id = Guid.NewGuid(),
            GoogleSub = googleSub,
            Cuil = employee.Cuil,
            Action = action,
            ReciboId = request.ReciboId,
            ViewedAtUtc = _dateTimeProvider.UtcNow
        };

        _context.ReciboViewEvents.Add(viewEvent);
        await _context.SaveChangesAsync();

        return Ok(new { ok = true });
    }

    /// <summary>
    /// Basic stats for recibo-sueldo views.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] int days = 30)
    {
        if (days <= 0 || days > 365) days = 30;
        var from = _dateTimeProvider.UtcNow.Date.AddDays(-days + 1);

        var total = await _context.ReciboViewEvents.CountAsync(e => e.ViewedAtUtc >= from);
        var opens = await _context.ReciboViewEvents.CountAsync(e => e.ViewedAtUtc >= from && e.Action == "open");
        var pages = await _context.ReciboViewEvents.CountAsync(e => e.ViewedAtUtc >= from && e.Action == "page");

        return Ok(new { from, days, total, pages, opens });
    }

    private bool ShouldUseMockReciboSueldo()
    {
        return _configuration.GetValue<bool>("ReciboSueldo:MockData");
    }

    private ReciboItem[] BuildMockReciboSueldo()
    {
        var now = _dateTimeProvider.UtcNow;
        return new[]
        {
            new ReciboItem("R-2026-01-370-1", "Enero 2026", 182_450.75m, "ARS", "Pendiente", now.AddDays(-45), null, "INSTITUTO MARIA A. DE PAZ Y FI", "HORA CATEDRA NIVEL MEDIO", "23/12/2020", 30),
            new ReciboItem("R-2026-01-SAP-1", "Enero 2026", 210_360.45m, "ARS", "Firmado", now.AddDays(-45), null, "SAED ADMINISTRACION", "ADMINISTRATIVO 2 SOEME", "16/07/2020", 30),
            new ReciboItem("R-2025-12-370-1", "Diciembre 2025", 176_900.10m, "ARS", "Disponible", now.AddDays(-75), null, "INSTITUTO MARIA A. DE PAZ Y FI", "HORA CATEDRA NIVEL MEDIO", "23/12/2020", 30)
        };
    }

    private static (IReadOnlyList<ReciboConceptoItem> Haberes, IReadOnlyList<ReciboConceptoItem> Descuentos) ParseConceptTables(string sourceJson, int cargoIndex)
    {
        using var document = System.Text.Json.JsonDocument.Parse(sourceJson);
        var root = document.RootElement;
        if (!TryGetCargo(root, cargoIndex, out var cargo))
        {
            return ([], []);
        }

        if (!cargo.TryGetProperty("Items", out var items) || items.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return ([], []);
        }

        var haberes = new List<ReciboConceptoItem>();
        var descuentos = new List<ReciboConceptoItem>();

        foreach (var item in items.EnumerateArray())
        {
            var codigo = TryGetString(item, "CodigoItem")
                ?? (item.TryGetProperty("Item", out var nestedItem) ? TryGetString(nestedItem, "CodigoItem") : null)
                ?? "-";
            var concepto = TryGetString(item, "DescripcionItem")
                ?? (item.TryGetProperty("Item", out var nestedItem2) ? TryGetString(nestedItem2, "Descripcion") : null)
                ?? "Concepto";
            var monto = TryGetDecimal(item, "TotalMontoItem")
                ?? TryGetDecimal(item, "MontoItem")
                ?? 0m;
            var esDescuento = TryGetBool(item, "EsDescuento")
                ?? (item.TryGetProperty("Item", out var nestedItem3) ? TryGetBool(nestedItem3, "Descuento") : null)
                ?? false;

            var conceptItem = new ReciboConceptoItem(codigo, concepto, monto);
            if (esDescuento)
            {
                descuentos.Add(conceptItem);
            }
            else
            {
                haberes.Add(conceptItem);
            }
        }

        return (haberes, descuentos);
    }

    private static bool TryGetCargo(System.Text.Json.JsonElement root, int cargoIndex, out System.Text.Json.JsonElement cargo)
    {
        cargo = default;
        if (!root.TryGetProperty("Cargos", out var cargos) ||
            cargos.ValueKind != System.Text.Json.JsonValueKind.Array ||
            cargos.GetArrayLength() == 0)
        {
            return false;
        }

        var safeIndex = cargoIndex;
        if (safeIndex < 0 || safeIndex >= cargos.GetArrayLength())
        {
            safeIndex = 0;
        }

        cargo = cargos[safeIndex];
        return true;
    }

    private static string? TryGetString(System.Text.Json.JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        return element.ValueKind == System.Text.Json.JsonValueKind.String ? element.GetString() : element.ToString();
    }

    private static decimal? TryGetDecimal(System.Text.Json.JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        if (element.ValueKind == System.Text.Json.JsonValueKind.Number && element.TryGetDecimal(out var value))
        {
            return value;
        }

        if (element.ValueKind == System.Text.Json.JsonValueKind.String &&
            decimal.TryParse(element.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool? TryGetBool(System.Text.Json.JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        if (element.ValueKind == System.Text.Json.JsonValueKind.True)
        {
            return true;
        }

        if (element.ValueKind == System.Text.Json.JsonValueKind.False)
        {
            return false;
        }

        if (element.ValueKind == System.Text.Json.JsonValueKind.String &&
            bool.TryParse(element.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }
}

public record ReciboSueldoResponse(string Cuil, IReadOnlyList<ReciboItem> Items);

public record ReciboItem(
    string Id,
    string Periodo,
    decimal Importe,
    string Moneda,
    string Estado,
    DateTime FechaEmision,
    string? PdfUrl,
    string? Establecimiento,
    string? Cargo,
    string? FechaIngreso,
    int? DiasTrabajados
);

public record ReciboSueldoViewRequest(string? Action, string? ReciboId);

public record ReciboDetalleResponse(
    string Id,
    string Establecimiento,
    string Cargo,
    string Periodo,
    DateTime FechaEmision,
    string? FechaIngreso,
    int? DiasTrabajados,
    decimal Bruto,
    decimal TotalDescuentos,
    decimal Liquido,
    string? LiquidoPalabras,
    IReadOnlyList<ReciboConceptoItem> Haberes,
    IReadOnlyList<ReciboConceptoItem> Descuentos,
    string PdfUrl
);

public record ReciboConceptoItem(
    string Codigo,
    string Concepto,
    decimal Monto
);
