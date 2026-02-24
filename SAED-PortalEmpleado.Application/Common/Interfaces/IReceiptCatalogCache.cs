namespace SAED_PortalEmpleado.Application.Common.Interfaces;

public interface IReceiptCatalogCache
{
    Task<ReceiptCacheEntry?> GetLatestAsync(
        string cuil,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetAvailableYearsAsync(
        string cuil,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetAvailableMonthsAsync(
        string cuil,
        int year,
        CancellationToken cancellationToken = default);

    Task<ReceiptCacheEntry> SaveNewVersionAsync(
        ReceiptCacheWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ReceiptCacheWriteRequest(
    string Cuil,
    int Year,
    int Month,
    DateTime DownloadedAtUtc,
    string SourceKey,
    string? SourceEtag,
    string? SourceVersionId,
    string PayloadJson);

public sealed record ReceiptCacheEntry(
    long SnapshotId,
    string Cuil,
    int Year,
    int Month,
    int Version,
    DateTime DownloadedAtUtc,
    string SourceKey,
    string? SourceEtag,
    string? SourceVersionId,
    string PayloadJson);
