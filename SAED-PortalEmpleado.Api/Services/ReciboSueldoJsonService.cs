using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using SAED_PortalEmpleado.Application.Common.Interfaces;
using SAED_PortalEmpleado.Infrastructure.Persistence;

namespace SAED_PortalEmpleado.Api.Services;

public interface IReciboSueldoJsonService
{
    Task<IReadOnlyList<ReciboDocument>> GetReciboSueldoForCuilAsync(string cuil, CancellationToken cancellationToken = default);
    Task<ReciboDocument?> GetReciboByIdForCuilAsync(string cuil, string reciboId, CancellationToken cancellationToken = default);
}

public interface IReciboPdfService
{
    Task<byte[]> BuildPdfAsync(ReciboDocument recibo, CancellationToken cancellationToken = default);
}

public sealed record ReciboDocument(
    string Id,
    string Periodo,
    string Cuil,
    string? Nombre,
    decimal Importe,
    decimal? TotalHaberes,
    decimal? TotalDescuentos,
    string Moneda,
    string Estado,
    DateTime FechaEmision,
    string SourceFile,
    DateTime SourceLastWriteUtc,
    string? SourceJson
);

public sealed record ReciboSueldoS3Settings(
    bool Enabled,
    string Bucket,
    string Region,
    string Prefix,
    string KeyTemplate,
    bool TraceSearches
);

public sealed class ReciboSueldoJsonService : IReciboSueldoJsonService
{
    private static readonly Regex SacPeriodRegex = new(@"(?<year>\d{4})\s*s(?<sac>[12])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MonthlyPeriodRegex = new(@"(?<year>\d{4})[-_]?(?<month>0[1-9]|1[0-2])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] MonthNamesEs =
    [
        "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    ];

    private readonly IConfiguration _configuration;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<ReciboSueldoJsonService> _logger;
    private readonly ReciboSueldoS3Settings _s3Settings;

    public ReciboSueldoJsonService(
        IConfiguration configuration,
        IDateTimeProvider dateTimeProvider,
        IHostEnvironment hostEnvironment,
        ILogger<ReciboSueldoJsonService> logger)
    {
        _configuration = configuration;
        _dateTimeProvider = dateTimeProvider;
        _hostEnvironment = hostEnvironment;
        _logger = logger;

        _s3Settings = new ReciboSueldoS3Settings(
            _configuration.GetValue<bool>("ReciboSueldo:S3:Enabled", false),
            _configuration["ReciboSueldo:S3:Bucket"] ?? string.Empty,
            _configuration["ReciboSueldo:S3:Region"] ?? string.Empty,
            _configuration["ReciboSueldo:S3:Prefix"] ?? string.Empty,
            _configuration["ReciboSueldo:S3:KeyTemplate"] ?? "{period}/Personal_{cuil}_{period}.json",
            _configuration.GetValue<bool>("ReciboSueldo:S3:TraceSearches", false)
        );
    }

    private void LogS3Trace(string messageTemplate, params object[] propertyValues)
    {
        if (_s3Settings.TraceSearches)
        {
            _logger.LogInformation(messageTemplate, propertyValues);
            return;
        }

        _logger.LogDebug(messageTemplate, propertyValues);
    }

    public async Task<IReadOnlyList<ReciboDocument>> GetReciboSueldoForCuilAsync(string cuil, CancellationToken cancellationToken = default)
    {
        var normalizedCuil = NormalizeCuil(cuil);
        if (string.IsNullOrWhiteSpace(normalizedCuil))
        {
            return [];
        }

        var now = _dateTimeProvider.UtcNow;
        var reciboSueldoItems = _s3Settings.Enabled
            ? await GetReciboSueldoFromS3Async(normalizedCuil, now, cancellationToken)
            : GetReciboSueldoFromLocalDirectory(normalizedCuil, now, cancellationToken);

        var deduped = reciboSueldoItems
            .GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.SourceLastWriteUtc).First())
            .OrderByDescending(r => r.FechaEmision)
            .ToArray();

        return deduped;
    }

    private IReadOnlyList<ReciboDocument> GetReciboSueldoFromLocalDirectory(string normalizedCuil, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var dataDirectory = ResolveDataDirectory();
        if (!Directory.Exists(dataDirectory))
        {
            _logger.LogWarning("ReciboSueldo directory not found: {Directory}", dataDirectory);
            return [];
        }

        var periods = BuildAllowedPeriods(nowUtc);
        var reciboSueldoItems = new List<ReciboDocument>();

        foreach (var period in periods)
        {
            foreach (var periodDirectory in BuildLocalPeriodDirectories(dataDirectory, period))
            {
                if (!Directory.Exists(periodDirectory))
                {
                    continue;
                }

                var files = Directory.EnumerateFiles(periodDirectory, "*.json", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (TryParseReciboFile(file, normalizedCuil, nowUtc, forcedPeriod: period, out var recibo) && recibo is not null)
                    {
                        reciboSueldoItems.Add(recibo);
                    }
                }
            }
        }

        return reciboSueldoItems;
    }

    private async Task<IReadOnlyList<ReciboDocument>> GetReciboSueldoFromS3Async(string normalizedCuil, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_s3Settings.Bucket))
        {
            _logger.LogWarning("ReciboSueldo:S3:Enabled is true but ReciboSueldo:S3:Bucket is empty.");
            return [];
        }

        using var s3Client = CreateS3Client();
        var periods = BuildAllowedPeriods(nowUtc);
        var cuilFormats = BuildCuilFormats(normalizedCuil);
        var attemptedKeys = 0;
        var downloadedObjects = 0;

        var reciboSueldoItems = new List<ReciboDocument>();
        LogS3Trace(
            "ReciboSueldo S3 search started. Bucket={Bucket} Region={Region} Prefix={Prefix} Template={Template} Cuil={Cuil} PeriodCount={PeriodCount}",
            _s3Settings.Bucket,
            _s3Settings.Region,
            _s3Settings.Prefix,
            _s3Settings.KeyTemplate,
            normalizedCuil,
            periods.Count);

        foreach (var period in periods)
        {
            var addedForPeriod = false;
            LogS3Trace("ReciboSueldo S3 searching period {PeriodId} (token {PeriodToken})", period.Id, period.StorageToken);

            foreach (var cuil in cuilFormats)
            {
                foreach (var key in BuildS3Keys(cuil, normalizedCuil, period))
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }
                    attemptedKeys++;
                    LogS3Trace("ReciboSueldo S3 trying key {Key} for period {PeriodId} and cuil {Cuil}", key, period.Id, cuil);

                    try
                    {
                        var response = await s3Client.GetObjectAsync(new GetObjectRequest
                        {
                            BucketName = _s3Settings.Bucket,
                            Key = key
                        }, cancellationToken);
                        downloadedObjects++;
                        LogS3Trace("ReciboSueldo S3 key hit {Key} for period {PeriodId}", key, period.Id);

                        await using var stream = response.ResponseStream;
                        using var reader = new StreamReader(stream);
                        var json = await reader.ReadToEndAsync();

                        if (TryParseReciboJson(
                            json,
                            sourceId: $"s3://{_s3Settings.Bucket}/{key}",
                            sourceLastWriteUtc: response.LastModified.ToUniversalTime(),
                            normalizedCuil: normalizedCuil,
                            nowUtc: nowUtc,
                            forcedPeriod: period,
                            out var recibo) && recibo is not null)
                        {
                            reciboSueldoItems.Add(recibo);
                            addedForPeriod = true;
                            _logger.LogInformation("ReciboSueldo S3 receipt selected for period {PeriodId} from key {Key}", period.Id, key);
                            break;
                        }

                        LogS3Trace("ReciboSueldo S3 key {Key} downloaded but did not match parser filters", key);
                    }
                    catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
                    {
                        // Missing file for this cuil/period is expected; continue trying other key variations.
                        LogS3Trace("ReciboSueldo S3 key miss {Key}", key);
                    }
                    catch (AmazonS3Exception ex)
                    {
                        _logger.LogWarning(ex, "Error downloading recibo from S3 bucket {Bucket} key {Key}", _s3Settings.Bucket, key);
                    }
                }

                if (addedForPeriod)
                {
                    break;
                }
            }

            if (addedForPeriod)
            {
                continue;
            }

            LogS3Trace("ReciboSueldo S3 no file found for period {PeriodId}", period.Id);
        }

        _logger.LogInformation(
            "ReciboSueldo S3 search finished. Cuil={Cuil} Periods={PeriodCount} TriedKeys={TriedKeys} Downloaded={Downloaded} Matched={Matched}",
            normalizedCuil,
            periods.Count,
            attemptedKeys,
            downloadedObjects,
            reciboSueldoItems.Count);

        return reciboSueldoItems;
    }

    public async Task<ReciboDocument?> GetReciboByIdForCuilAsync(string cuil, string reciboId, CancellationToken cancellationToken = default)
    {
        var normalizedId = NormalizeReciboId(reciboId);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return null;
        }

        var reciboSueldoItems = await GetReciboSueldoForCuilAsync(cuil, cancellationToken);
        return reciboSueldoItems.FirstOrDefault(r => string.Equals(r.Id, normalizedId, StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveDataDirectory()
    {
        var configured = _configuration["ReciboSueldo:DataDirectory"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = "../recibo-sueldo";
        }

        if (Path.IsPathRooted(configured))
        {
            return configured;
        }

        return Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, configured));
    }

    private static IReadOnlyList<string> BuildLocalPeriodDirectories(string baseDirectory, ReciboPeriod period)
    {
        return new[] { period.Id, period.StorageToken }
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(folder => Path.Combine(baseDirectory, folder))
            .ToArray();
    }

    private bool TryParseReciboFile(string filePath, string normalizedCuil, DateTime nowUtc, ReciboPeriod? forcedPeriod, out ReciboDocument? recibo)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var lastWriteUtc = File.GetLastWriteTimeUtc(filePath);
            return TryParseReciboJson(
                json,
                sourceId: filePath,
                sourceLastWriteUtc: lastWriteUtc,
                normalizedCuil: normalizedCuil,
                nowUtc: nowUtc,
                forcedPeriod: forcedPeriod,
                out recibo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skipping invalid recibo file: {FilePath}", filePath);
            recibo = null;
            return false;
        }
    }

    private bool TryParseReciboJson(
        string json,
        string sourceId,
        DateTime sourceLastWriteUtc,
        string normalizedCuil,
        DateTime nowUtc,
        ReciboPeriod? forcedPeriod,
        out ReciboDocument? recibo)
    {
        recibo = null;

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var cuil = TryGetString(root, "Cuil");
        if (string.IsNullOrWhiteSpace(cuil))
        {
            return false;
        }

        if (!string.Equals(NormalizeCuil(cuil), normalizedCuil, StringComparison.Ordinal))
        {
            return false;
        }

        ReciboPeriod period;
        if (forcedPeriod.HasValue)
        {
            period = forcedPeriod.Value;
        }
        else if (!TryResolvePeriod(root, sourceId, out period))
        {
            return false;
        }

        if (!IsWithinAllowedWindow(period.PeriodDate, nowUtc))
        {
            return false;
        }

        var totalLiquido = TryGetDecimal(root, "TotalLiquido")
            ?? TryGetDecimal(root, "TotalItems")
            ?? TryGetFirstCargoDecimal(root, "Liquido")
            ?? 0m;

        var totalHaberes = TryGetDecimal(root, "TotalHaberes");
        var totalDescuentos = TryGetDecimal(root, "TotalItemsDescuentos");
        var nombre = TryGetString(root, "Nombre");

        recibo = new ReciboDocument(
            period.Id,
            period.Display,
            cuil,
            nombre,
            totalLiquido,
            totalHaberes,
            totalDescuentos,
            "ARS",
            "Emitido",
            period.FechaEmisionUtc,
            sourceId,
            sourceLastWriteUtc,
            json
        );

        return true;
    }

    private bool TryResolvePeriod(JsonElement root, string filePath, out ReciboPeriod period)
    {
        period = default;

        var inputFileName = TryGetString(root, "InputFileName");
        if (TryParsePeriodToken(GetPeriodToken(inputFileName), out period))
        {
            return true;
        }

        if (TryParsePeriodToken(GetPeriodToken(filePath), out period))
        {
            return true;
        }

        if (root.TryGetProperty("Cargos", out var cargos) &&
            cargos.ValueKind == JsonValueKind.Array &&
            cargos.GetArrayLength() > 0)
        {
            var firstCargo = cargos[0];
            if (firstCargo.TryGetProperty("Sueldo", out var sueldo) &&
                sueldo.ValueKind == JsonValueKind.Object)
            {
                var ano = TryGetInt(sueldo, "Ano");
                var mes = TryGetInt(sueldo, "Mes");
                if (ano.HasValue && mes is >= 1 and <= 12)
                {
                    period = BuildMonthlyPeriod(ano.Value, mes.Value);
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetPeriodToken(string? sourcePathOrName)
    {
        if (string.IsNullOrWhiteSpace(sourcePathOrName))
        {
            return string.Empty;
        }

        var fileName = Path.GetFileNameWithoutExtension(sourcePathOrName);
        var token = fileName.Split('_', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return token ?? fileName;
    }

    private static bool TryParsePeriodToken(string token, out ReciboPeriod period)
    {
        period = default;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var sacMatch = SacPeriodRegex.Match(token);
        if (sacMatch.Success &&
            int.TryParse(sacMatch.Groups["year"].Value, out var sacYear) &&
            int.TryParse(sacMatch.Groups["sac"].Value, out var sacPart))
        {
            period = BuildSacPeriod(sacYear, sacPart);
            return true;
        }

        var monthlyMatch = MonthlyPeriodRegex.Match(token);
        if (monthlyMatch.Success &&
            int.TryParse(monthlyMatch.Groups["year"].Value, out var year) &&
            int.TryParse(monthlyMatch.Groups["month"].Value, out var month) &&
            month is >= 1 and <= 12)
        {
            period = BuildMonthlyPeriod(year, month);
            return true;
        }

        return false;
    }

    private static ReciboPeriod BuildMonthlyPeriod(int year, int month)
    {
        var id = $"{year:D4}-{month:D2}";
        var storageToken = $"{year:D4}{month:D2}";
        var display = $"{MonthNamesEs[month - 1]} {year:D4}";
        var periodDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var fechaEmision = new DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Utc);
        return new ReciboPeriod(id, display, periodDate, fechaEmision, storageToken);
    }

    private static ReciboPeriod BuildSacPeriod(int year, int sacPart)
    {
        var month = sacPart == 1 ? 6 : 12;
        var sacLabel = sacPart == 1 ? "SAC Junio" : "SAC Diciembre";
        var id = $"{year:D4}s{sacPart}";
        var storageToken = id;
        var display = $"{id} ({sacLabel})";
        var periodDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var fechaEmision = new DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Utc);
        return new ReciboPeriod(id, display, periodDate, fechaEmision, storageToken);
    }

    private bool IsWithinAllowedWindow(DateTime periodDateUtc, DateTime nowUtc)
    {
        var maxMonthsBack = _configuration.GetValue<int>("ReciboSueldo:MaxMonthsBack", 12);
        if (maxMonthsBack < 1)
        {
            maxMonthsBack = 12;
        }

        var currentPeriod = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var minAllowed = currentPeriod.AddMonths(-(maxMonthsBack - 1));

        return periodDateUtc >= minAllowed && periodDateUtc <= currentPeriod;
    }

    private IReadOnlyList<ReciboPeriod> BuildAllowedPeriods(DateTime nowUtc)
    {
        var maxMonthsBack = _configuration.GetValue<int>("ReciboSueldo:MaxMonthsBack", 12);
        if (maxMonthsBack < 1)
        {
            maxMonthsBack = 12;
        }

        var periods = new List<ReciboPeriod>(maxMonthsBack + 4);
        var currentPeriod = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < maxMonthsBack; i++)
        {
            var monthlyDate = currentPeriod.AddMonths(-i);
            periods.Add(BuildMonthlyPeriod(monthlyDate.Year, monthlyDate.Month));

            if (monthlyDate.Month == 6)
            {
                periods.Add(BuildSacPeriod(monthlyDate.Year, 1));
            }
            else if (monthlyDate.Month == 12)
            {
                periods.Add(BuildSacPeriod(monthlyDate.Year, 2));
            }
        }

        return periods
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(p => p.PeriodDate)
            .ThenByDescending(p => p.Id)
            .ToArray();
    }

    private IReadOnlyList<string> BuildCuilFormats(string normalizedCuil)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(normalizedCuil))
        {
            values.Add(normalizedCuil);
            var dashed = ToDashedCuil(normalizedCuil);
            if (!string.IsNullOrWhiteSpace(dashed))
            {
                values.Add(dashed);
            }
        }

        return values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<string> BuildS3Keys(string cuil, string cuilDigits, ReciboPeriod period)
    {
        var template = string.IsNullOrWhiteSpace(_s3Settings.KeyTemplate)
            ? "{period}/Personal_{cuil}_{period}.json"
            : _s3Settings.KeyTemplate;

        var periodTokenCandidates = new[] { period.StorageToken, period.Id }
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var periodFolderCandidates = new[] { period.StorageToken, period.Id }
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var keys = new List<string>();

        foreach (var periodToken in periodTokenCandidates)
        {
            foreach (var periodFolder in periodFolderCandidates)
            {
                var relativeKey = template
                    .Replace("{cuil}", cuil, StringComparison.OrdinalIgnoreCase)
                    .Replace("{cuil_digits}", cuilDigits, StringComparison.OrdinalIgnoreCase)
                    .Replace("{period}", periodToken, StringComparison.OrdinalIgnoreCase)
                    .Replace("{period_token}", periodToken, StringComparison.OrdinalIgnoreCase)
                    .Replace("{period_id}", period.Id, StringComparison.OrdinalIgnoreCase)
                    .Replace("{period_folder}", periodFolder, StringComparison.OrdinalIgnoreCase);

                var fullKey = string.IsNullOrWhiteSpace(_s3Settings.Prefix)
                    ? relativeKey.TrimStart('/')
                    : $"{_s3Settings.Prefix.Trim().TrimEnd('/')}/{relativeKey.TrimStart('/')}";

                if (!string.IsNullOrWhiteSpace(fullKey))
                {
                    keys.Add(fullKey);
                }
            }
        }

        return keys
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IAmazonS3 CreateS3Client()
    {
        if (string.IsNullOrWhiteSpace(_s3Settings.Region))
        {
            return new AmazonS3Client();
        }

        return new AmazonS3Client(RegionEndpoint.GetBySystemName(_s3Settings.Region));
    }

    private static string NormalizeCuil(string value)
    {
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static string ToDashedCuil(string normalizedCuil)
    {
        if (normalizedCuil.Length != 11)
        {
            return normalizedCuil;
        }

        return $"{normalizedCuil[..2]}-{normalizedCuil.Substring(2, 8)}-{normalizedCuil[10]}";
    }

    private static string NormalizeReciboId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return string.Empty;
        }

        var normalized = id.Trim().ToLowerInvariant();
        var sacMatch = Regex.Match(normalized, @"^(?<year>\d{4})s(?<sac>[12])$");
        if (sacMatch.Success)
        {
            return $"{sacMatch.Groups["year"].Value}s{sacMatch.Groups["sac"].Value}";
        }

        var monthlyCompact = Regex.Match(normalized, @"^(?<year>\d{4})(?<month>0[1-9]|1[0-2])$");
        if (monthlyCompact.Success)
        {
            return $"{monthlyCompact.Groups["year"].Value}-{monthlyCompact.Groups["month"].Value}";
        }

        var monthlyDashed = Regex.Match(normalized, @"^(?<year>\d{4})-(?<month>0[1-9]|1[0-2])$");
        if (monthlyDashed.Success)
        {
            return normalized;
        }

        return normalized;
    }

    private static string? TryGetString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
    }

    private static int? TryGetInt(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var n))
        {
            return n;
        }

        return int.TryParse(element.ToString(), out var parsed) ? parsed : null;
    }

    private static decimal? TryGetDecimal(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        return TryGetDecimal(element);
    }

    private static decimal? TryGetDecimal(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var decimalValue))
        {
            return decimalValue;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedInvariant))
            {
                return parsedInvariant;
            }

            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("es-AR"), out var parsedEs))
            {
                return parsedEs;
            }
        }

        return null;
    }

    private static decimal? TryGetFirstCargoDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty("Cargos", out var cargos) ||
            cargos.ValueKind != JsonValueKind.Array ||
            cargos.GetArrayLength() == 0)
        {
            return null;
        }

        var firstCargo = cargos[0];
        return TryGetDecimal(firstCargo, propertyName);
    }

    private readonly record struct ReciboPeriod(
        string Id,
        string Display,
        DateTime PeriodDate,
        DateTime FechaEmisionUtc,
        string StorageToken
    );
}

public sealed class ReciboPdfService : IReciboPdfService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReciboPdfService> _logger;

    public ReciboPdfService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<ReciboPdfService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<byte[]> BuildPdfAsync(ReciboDocument recibo, CancellationToken cancellationToken = default)
    {
        var company = await ResolveCompanyProfileAsync(cancellationToken);
        var lines = new List<string> { "RECIBO DE HABERES" };

        if (company is not null)
        {
            lines.Add($"Empresa: {company.DisplayName}");
            lines.Add($"CUIT Empresa: {company.Cuit}");

            var domicilio = BuildAddressLine(company);
            if (!string.IsNullOrWhiteSpace(domicilio))
            {
                lines.Add($"Domicilio: {domicilio}");
            }
        }

        lines.AddRange(new[]
        {
            string.Empty,
            new string('=', 118),
            string.Empty,
            $"Periodo: {recibo.Periodo}   Fecha Emisión: {recibo.FechaEmision:dd/MM/yyyy}   ID: {recibo.Id}",
            $"Empleado: {recibo.Nombre ?? "(sin nombre)"}",
            $"CUIL: {recibo.Cuil}",
            string.Empty,
            "Cod   Concepto                                        T   Haber              Descuento",
            new string('-', 118),
        });

        var sourceJson = await ResolveSourceJsonAsync(recibo, cancellationToken);
        if (string.IsNullOrWhiteSpace(sourceJson))
        {
            lines.Add("No se encontraron detalles del recibo para renderizar conceptos.");
            return MinimalPdfBuilder.BuildSinglePage(lines);
        }

        try
        {
            using var document = JsonDocument.Parse(sourceJson);
            var root = document.RootElement;

            AppendMetadataLines(lines, root);

            var conceptos = ExtractConceptRows(root, 30);
            if (conceptos.Count == 0)
            {
                lines.Add("Sin conceptos en detalle.");
            }
            else
            {
                foreach (var concepto in conceptos)
                {
                    var codigo = PadOrTrim(concepto.Codigo, 5);
                    var descripcion = PadOrTrim(concepto.Descripcion, 46);
                    var tipo = PadOrTrim(concepto.Tipo, 2);
                    var haber = concepto.EsDescuento ? string.Empty : FormatAmount(concepto.Monto);
                    var descuento = concepto.EsDescuento ? FormatAmount(concepto.Monto) : string.Empty;

                    lines.Add(
                        $"{codigo} {descripcion} {tipo} {PadLeftOrTrim(haber, 16)} {PadLeftOrTrim(descuento, 16)}");
                }
            }

            var totalHaberes = TryGetFirstCargoDecimal(root, "TotalItemsHaber") ?? recibo.TotalHaberes ?? 0m;
            var totalDescuentos = TryGetFirstCargoDecimal(root, "TotalItemsDescuento") ?? recibo.TotalDescuentos ?? 0m;
            var liquido = TryGetFirstCargoDecimal(root, "Liquido") ?? recibo.Importe;

            lines.Add(new string('-', 118));
            lines.Add(
                $"TOTAL HABERES: {PadLeftOrTrim(FormatAmount(totalHaberes), 18)}   TOTAL DESCUENTOS: {PadLeftOrTrim(FormatAmount(totalDescuentos), 18)}");
            lines.Add($"LÍQUIDO A COBRAR: {FormatAmount(liquido)}");

            var liquidoPalabras = TryGetFirstCargoString(root, "LiquidoPalabras");
            if (!string.IsNullOrWhiteSpace(liquidoPalabras))
            {
                lines.Add(string.Empty);
                lines.Add($"Son pesos: {liquidoPalabras}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not render detailed PDF for source {SourceFile}", recibo.SourceFile);
            lines.Add("No se pudieron cargar los conceptos detallados.");
        }

        return MinimalPdfBuilder.BuildSinglePage(lines);
    }

    private async Task<string?> ResolveSourceJsonAsync(ReciboDocument recibo, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(recibo.SourceJson))
        {
            return recibo.SourceJson;
        }

        if (recibo.SourceFile.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!File.Exists(recibo.SourceFile))
        {
            return null;
        }

        return await File.ReadAllTextAsync(recibo.SourceFile, cancellationToken);
    }

    private async Task<CompanyProfileSnapshot?> ResolveCompanyProfileAsync(CancellationToken cancellationToken)
    {
        try
        {
            var company = await _context.CompanyProfiles
                .AsNoTracking()
                .OrderByDescending(c => c.UpdatedAtUtc ?? c.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (company is not null)
            {
                return new CompanyProfileSnapshot(
                    company.DisplayName,
                    company.Cuit,
                    company.AddressLine,
                    company.City,
                    company.Province,
                    company.PostalCode,
                    company.Country);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load company profile from database.");
        }

        var fallback = new CompanyProfileSnapshot(
            _configuration["ReciboSueldo:Company:DisplayName"] ?? string.Empty,
            _configuration["ReciboSueldo:Company:Cuit"] ?? string.Empty,
            _configuration["ReciboSueldo:Company:AddressLine"],
            _configuration["ReciboSueldo:Company:City"],
            _configuration["ReciboSueldo:Company:Province"],
            _configuration["ReciboSueldo:Company:PostalCode"],
            _configuration["ReciboSueldo:Company:Country"]);

        if (string.IsNullOrWhiteSpace(fallback.DisplayName) || string.IsNullOrWhiteSpace(fallback.Cuit))
        {
            return null;
        }

        return fallback;
    }

    private static string BuildAddressLine(CompanyProfileSnapshot company)
    {
        var pieces = new[]
        {
            company.AddressLine,
            company.City,
            company.Province,
            company.PostalCode,
            company.Country
        };

        return string.Join(", ", pieces.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static void AppendMetadataLines(List<string> lines, JsonElement root)
    {
        if (!TryGetFirstCargo(root, out var firstCargo))
        {
            return;
        }

        var establecimiento = TryGetNestedString(firstCargo, "Establecimiento", "Nombre");
        var domicilio = TryGetNestedString(firstCargo, "Establecimiento", "Domicilio");
        var localidad = TryGetNestedString(firstCargo, "Establecimiento", "Localidad");
        var formaPago = TryGetNestedString(firstCargo, "FormaPago", "Descripcion");
        var cargo = TryGetNestedString(firstCargo, "Cargo", "Descripcion");
        var legajo = TryGetString(root, "Codigo");

        if (!string.IsNullOrWhiteSpace(establecimiento))
        {
            lines.Add($"Establecimiento: {establecimiento}");
        }

        var domicilioCompleto = string.Join(", ", new[] { domicilio, localidad }.Where(v => !string.IsNullOrWhiteSpace(v)));
        if (!string.IsNullOrWhiteSpace(domicilioCompleto))
        {
            lines.Add($"Domicilio Est.: {domicilioCompleto}");
        }

        var legajoText = string.IsNullOrWhiteSpace(legajo) ? "-" : legajo;
        var formaPagoText = string.IsNullOrWhiteSpace(formaPago) ? "-" : formaPago;
        lines.Add($"Legajo: {legajoText}   Forma de Pago: {formaPagoText}");

        if (!string.IsNullOrWhiteSpace(cargo))
        {
            lines.Add($"Cargo: {cargo}");
        }

        var horasCargo = TryGetDecimal(firstCargo, "HorasCargo");
        var antiguedad = TryGetNestedString(firstCargo, "Antiguedad", "Tipo");
        var fechaIngreso = TryGetString(firstCargo, "FechaIngreso");
        var resumen = $"Horas: {(horasCargo?.ToString("0.##", CultureInfo.InvariantCulture) ?? "-")}";
        if (!string.IsNullOrWhiteSpace(antiguedad))
        {
            resumen += $"   Antigüedad: {antiguedad}";
        }
        if (!string.IsNullOrWhiteSpace(fechaIngreso))
        {
            resumen += $"   Fecha Ingreso: {fechaIngreso}";
        }
        lines.Add(resumen);
        lines.Add(string.Empty);
    }

    private static List<ConceptRow> ExtractConceptRows(JsonElement root, int maxItems)
    {
        var rows = new List<ConceptRow>();
        if (!TryGetFirstCargo(root, out var firstCargo))
        {
            return rows;
        }

        if (!firstCargo.TryGetProperty("Items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return rows;
        }

        foreach (var item in items.EnumerateArray().Take(maxItems))
        {
            var descripcion = TryGetString(item, "DescripcionItem");
            if (string.IsNullOrWhiteSpace(descripcion) &&
                item.TryGetProperty("Item", out var nestedItem) &&
                nestedItem.ValueKind == JsonValueKind.Object)
            {
                descripcion = TryGetString(nestedItem, "Descripcion");
            }

            if (string.IsNullOrWhiteSpace(descripcion))
            {
                descripcion = "Concepto";
            }

            var codigo = TryGetString(item, "CodigoItem")
                ?? (item.TryGetProperty("Item", out var itemNode) ? TryGetString(itemNode, "CodigoItem") : null)
                ?? string.Empty;
            var tipo = TryGetString(item, "TipoItem")
                ?? (item.TryGetProperty("Item", out var itemNode2) ? TryGetString(itemNode2, "TipoItem") : null)
                ?? string.Empty;

            var monto = TryGetDecimal(item, "TotalMontoItem")
                ?? TryGetDecimal(item, "MontoItem")
                ?? 0m;

            var esDescuento = TryGetBool(item, "EsDescuento")
                ?? (item.TryGetProperty("Item", out var nestedItemBool) ? TryGetBool(nestedItemBool, "Descuento") : null)
                ?? false;

            rows.Add(new ConceptRow(codigo, descripcion, tipo, monto, esDescuento));
        }

        return rows;
    }

    private static bool TryGetFirstCargo(JsonElement root, out JsonElement cargo)
    {
        cargo = default;
        if (!root.TryGetProperty("Cargos", out var cargos) ||
            cargos.ValueKind != JsonValueKind.Array ||
            cargos.GetArrayLength() == 0)
        {
            return false;
        }

        cargo = cargos[0];
        return true;
    }

    private static string? TryGetFirstCargoString(JsonElement root, string propertyName)
    {
        if (!TryGetFirstCargo(root, out var cargo))
        {
            return null;
        }

        return TryGetString(cargo, propertyName);
    }

    private static decimal? TryGetFirstCargoDecimal(JsonElement root, string propertyName)
    {
        if (!TryGetFirstCargo(root, out var cargo))
        {
            return null;
        }

        return TryGetDecimal(cargo, propertyName);
    }

    private static string? TryGetNestedString(JsonElement root, string nestedProperty, string propertyName)
    {
        if (!root.TryGetProperty(nestedProperty, out var nested) || nested.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return TryGetString(nested, propertyName);
    }

    private static string? TryGetString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
    }

    private static decimal? TryGetDecimal(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        return TryGetDecimal(element);
    }

    private static decimal? TryGetDecimal(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var n))
        {
            return n;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = element.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedInvariant))
        {
            return parsedInvariant;
        }

        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("es-AR"), out var parsedEs)
            ? parsedEs
            : null;
    }

    private static bool? TryGetBool(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.String &&
            bool.TryParse(element.GetString(), out var parsedBool))
        {
            return parsedBool;
        }

        return null;
    }

    private static string FormatAmount(decimal value)
    {
        return value.ToString("N2", CultureInfo.GetCultureInfo("es-AR"));
    }

    private static string PadOrTrim(string? value, int width)
    {
        var safe = (value ?? string.Empty).Trim();
        if (safe.Length > width)
        {
            safe = safe[..Math.Max(0, width - 3)] + "...";
        }

        return safe.PadRight(width);
    }

    private static string PadLeftOrTrim(string? value, int width)
    {
        var safe = (value ?? string.Empty).Trim();
        if (safe.Length > width)
        {
            safe = safe[^width..];
        }

        return safe.PadLeft(width);
    }

    private sealed record CompanyProfileSnapshot(
        string DisplayName,
        string Cuit,
        string? AddressLine,
        string? City,
        string? Province,
        string? PostalCode,
        string? Country
    );

    private sealed record ConceptRow(
        string Codigo,
        string Descripcion,
        string Tipo,
        decimal Monto,
        bool EsDescuento
    );
}

internal static class MinimalPdfBuilder
{
    private static readonly Encoding PdfEncoding = Encoding.Latin1;

    public static byte[] BuildSinglePage(IReadOnlyList<string> lines)
    {
        var safeLines = lines
            .Select(line => Truncate(line ?? string.Empty, 122))
            .Take(66)
            .ToArray();

        var content = BuildTextStream(safeLines);
        var contentBytes = PdfEncoding.GetBytes(content);

        using var stream = new MemoryStream();
        WriteAscii(stream, "%PDF-1.4\n");

        var offsets = new long[6];

        WriteObject(stream, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
        WriteObject(stream, offsets, 2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        WriteObject(
            stream,
            offsets,
            3,
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>");
        WriteObject(stream, offsets, 4, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>");

        offsets[5] = stream.Position;
        WriteAscii(stream, $"5 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
        stream.Write(contentBytes, 0, contentBytes.Length);
        WriteAscii(stream, "\nendstream\nendobj\n");

        var xrefPosition = stream.Position;
        WriteAscii(stream, "xref\n0 6\n0000000000 65535 f \n");
        for (var i = 1; i <= 5; i++)
        {
            WriteAscii(stream, $"{offsets[i]:0000000000} 00000 n \n");
        }

        WriteAscii(stream, "trailer\n<< /Size 6 /Root 1 0 R >>\n");
        WriteAscii(stream, $"startxref\n{xrefPosition}\n%%EOF");

        return stream.ToArray();
    }

    private static void WriteObject(Stream stream, long[] offsets, int id, string body)
    {
        offsets[id] = stream.Position;
        WriteAscii(stream, $"{id} 0 obj\n{body}\nendobj\n");
    }

    private static string BuildTextStream(IReadOnlyList<string> lines)
    {
        var sb = new StringBuilder();
        sb.Append("BT\n");
        sb.Append("/F1 9 Tf\n");
        sb.Append("30 815 Td\n");

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                sb.Append("0 -11 Td\n");
                continue;
            }

            sb.Append('(')
                .Append(EscapePdfText(line))
                .Append(") Tj\n");
            sb.Append("0 -11 Td\n");
        }

        sb.Append("ET");
        return sb.ToString();
    }

    private static void WriteAscii(Stream stream, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string EscapePdfText(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 3)] + "...";
    }
}
