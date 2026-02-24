namespace SAED_PortalEmpleado.Application.Common.Interfaces;

public interface IReceiptSourceAws
{
    Task<ReceiptSourceFetchResult> FetchPeriodAsync(
        ReceiptSourceFetchRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ReceiptSourceFetchRequest(
    string Cuil,
    int Year,
    int Month,
    string? KnownEtag,
    string? KnownVersionId);

public enum ReceiptSourceFetchStatus
{
    Downloaded = 0,
    NotModified = 1,
    NotFound = 2,
    SourceDisabled = 3
}

public sealed record ReceiptSourceFetchResult(
    ReceiptSourceFetchStatus Status,
    string? PayloadJson,
    DateTime? DownloadedAtUtc,
    string SourceKey,
    string? SourceEtag,
    string? SourceVersionId);
