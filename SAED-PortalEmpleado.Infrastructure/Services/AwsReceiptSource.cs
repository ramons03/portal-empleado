using System.Text;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SAED_PortalEmpleado.Application.Common.Interfaces;

namespace SAED_PortalEmpleado.Infrastructure.Services;

public sealed class AwsReceiptSource : IReceiptSourceAws
{
    private readonly ILogger<AwsReceiptSource> _logger;
    private readonly ReceiptS3Options _options;

    public AwsReceiptSource(
        IConfiguration configuration,
        ILogger<AwsReceiptSource> logger)
    {
        _logger = logger;
        _options = new ReceiptS3Options(
            GetBool(configuration["ReciboSueldo:S3:Enabled"], false),
            configuration["ReciboSueldo:S3:Bucket"] ?? string.Empty,
            configuration["ReciboSueldo:S3:Region"] ?? string.Empty,
            configuration["ReciboSueldo:S3:Prefix"] ?? string.Empty,
            configuration["ReciboSueldo:S3:KeyTemplate"] ?? "{period}/Personal_{cuil}_{period}.json",
            GetBool(configuration["ReciboSueldo:S3:TraceSearches"], false));
    }

    public async Task<ReceiptSourceFetchResult> FetchPeriodAsync(
        ReceiptSourceFetchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.Bucket))
        {
            _logger.LogWarning("AWS receipt source is disabled or bucket is missing.");
            return new ReceiptSourceFetchResult(
                ReceiptSourceFetchStatus.SourceDisabled,
                null,
                null,
                string.Empty,
                null,
                null);
        }

        var cuilDigits = NormalizeCuil(request.Cuil);
        if (string.IsNullOrWhiteSpace(cuilDigits))
        {
            return new ReceiptSourceFetchResult(
                ReceiptSourceFetchStatus.NotFound,
                null,
                null,
                string.Empty,
                null,
                null);
        }

        var keys = BuildCandidateKeys(cuilDigits, request.Year, request.Month);
        using var s3Client = CreateS3Client();

        foreach (var key in keys)
        {
            try
            {
                TraceSearch("Checking S3 metadata. Bucket={Bucket} Key={Key}", _options.Bucket, key);
                var metadata = await s3Client.GetObjectMetadataAsync(
                    new GetObjectMetadataRequest
                    {
                        BucketName = _options.Bucket,
                        Key = key
                    },
                    cancellationToken);

                var metadataEtag = NormalizeEtag(metadata.ETag);
                var metadataVersionId = NormalizeValue(metadata.VersionId);
                if (IsNotModified(request, metadataEtag, metadataVersionId))
                {
                    _logger.LogInformation(
                        "Receipt source not modified for period {Year}-{Month}. Key={Key} ETag={ETag} VersionId={VersionId}",
                        request.Year,
                        request.Month,
                        key,
                        metadataEtag,
                        metadataVersionId);

                    return new ReceiptSourceFetchResult(
                        ReceiptSourceFetchStatus.NotModified,
                        null,
                        null,
                        key,
                        metadataEtag,
                        metadataVersionId);
                }

                TraceSearch("Downloading S3 object. Bucket={Bucket} Key={Key}", _options.Bucket, key);
                using var response = await s3Client.GetObjectAsync(
                    new GetObjectRequest
                    {
                        BucketName = _options.Bucket,
                        Key = key
                    },
                    cancellationToken);

                await using var responseStream = response.ResponseStream;
                using var reader = new StreamReader(responseStream, Encoding.UTF8);
                var payloadJson = await reader.ReadToEndAsync(cancellationToken);
                var etag = NormalizeEtag(response.ETag) ?? metadataEtag;
                var versionId = NormalizeValue(response.VersionId) ?? metadataVersionId;

                _logger.LogInformation(
                    "Receipt source downloaded from S3. Bucket={Bucket} Key={Key} ETag={ETag} VersionId={VersionId}",
                    _options.Bucket,
                    key,
                    etag,
                    versionId);

                return new ReceiptSourceFetchResult(
                    ReceiptSourceFetchStatus.Downloaded,
                    payloadJson,
                    DateTime.UtcNow,
                    key,
                    etag,
                    versionId);
            }
            catch (AmazonS3Exception ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
            {
                TraceSearch("S3 key not found. Key={Key}", key);
                continue;
            }
        }

        _logger.LogWarning(
            "Receipt source not found for period {Year}-{Month}. Bucket={Bucket}",
            request.Year,
            request.Month,
            _options.Bucket);

        return new ReceiptSourceFetchResult(
            ReceiptSourceFetchStatus.NotFound,
            null,
            null,
            string.Empty,
            null,
            null);
    }

    private void TraceSearch(string messageTemplate, params object[] args)
    {
        if (_options.TraceSearches)
        {
            _logger.LogInformation(messageTemplate, args);
        }
    }

    private IAmazonS3 CreateS3Client()
    {
        if (string.IsNullOrWhiteSpace(_options.Region))
        {
            return new AmazonS3Client();
        }

        return new AmazonS3Client(RegionEndpoint.GetBySystemName(_options.Region));
    }

    private IReadOnlyList<string> BuildCandidateKeys(string cuilDigits, int year, int month)
    {
        var periodToken = $"{year:D4}{month:D2}";
        var periodId = $"{year:D4}-{month:D2}";

        var cuilVariants = new[] { cuilDigits, ToDashedCuil(cuilDigits) }
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var periodTokens = new[] { periodToken, periodId }
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var periodFolders = new[] { periodToken, periodId }
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var keys = new List<string>();
        foreach (var cuil in cuilVariants)
        {
            foreach (var token in periodTokens)
            {
                foreach (var folder in periodFolders)
                {
                    var relativeKey = _options.KeyTemplate
                        .Replace("{cuil}", cuil, StringComparison.OrdinalIgnoreCase)
                        .Replace("{cuil_digits}", cuilDigits, StringComparison.OrdinalIgnoreCase)
                        .Replace("{period}", token, StringComparison.OrdinalIgnoreCase)
                        .Replace("{period_token}", token, StringComparison.OrdinalIgnoreCase)
                        .Replace("{period_id}", periodId, StringComparison.OrdinalIgnoreCase)
                        .Replace("{period_folder}", folder, StringComparison.OrdinalIgnoreCase);

                    var key = string.IsNullOrWhiteSpace(_options.Prefix)
                        ? relativeKey.TrimStart('/')
                        : $"{_options.Prefix.Trim().TrimEnd('/')}/{relativeKey.TrimStart('/')}";

                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        keys.Add(key);
                    }
                }
            }
        }

        return keys.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsNotModified(
        ReceiptSourceFetchRequest request,
        string? candidateEtag,
        string? candidateVersionId)
    {
        if (!string.IsNullOrWhiteSpace(request.KnownVersionId) &&
            string.Equals(request.KnownVersionId, candidateVersionId, StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(request.KnownEtag) &&
            string.Equals(NormalizeEtag(request.KnownEtag), NormalizeEtag(candidateEtag), StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static string NormalizeCuil(string value)
    {
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static string ToDashedCuil(string cuilDigits)
    {
        if (cuilDigits.Length != 11)
        {
            return cuilDigits;
        }

        return $"{cuilDigits[..2]}-{cuilDigits.Substring(2, 8)}-{cuilDigits[10]}";
    }

    private static string? NormalizeEtag(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().Trim('"');
    }

    private static string? NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool GetBool(string? value, bool defaultValue)
    {
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private sealed record ReceiptS3Options(
        bool Enabled,
        string Bucket,
        string Region,
        string Prefix,
        string KeyTemplate,
        bool TraceSearches);
}
