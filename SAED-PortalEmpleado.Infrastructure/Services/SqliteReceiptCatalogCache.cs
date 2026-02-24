using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SAED_PortalEmpleado.Application.Common.Interfaces;

namespace SAED_PortalEmpleado.Infrastructure.Services;

public sealed class SqliteReceiptCatalogCache : IReceiptCatalogCache
{
    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS receipt_snapshots (
            snapshot_id INTEGER PRIMARY KEY AUTOINCREMENT,
            cuil TEXT NOT NULL,
            year INTEGER NOT NULL CHECK (year BETWEEN 2000 AND 2100),
            month INTEGER NOT NULL CHECK (month BETWEEN 1 AND 12),
            version INTEGER NOT NULL CHECK (version >= 1),
            downloaded_at_utc TEXT NOT NULL,
            source_key TEXT NOT NULL,
            source_etag TEXT NULL,
            source_version_id TEXT NULL,
            payload_gzip BLOB NOT NULL,
            payload_sha256 TEXT NOT NULL,
            payload_size_bytes INTEGER NOT NULL CHECK (payload_size_bytes > 0),
            created_at_utc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
            UNIQUE (cuil, year, month, version)
        );

        CREATE TABLE IF NOT EXISTS receipt_latest (
            cuil TEXT NOT NULL,
            year INTEGER NOT NULL CHECK (year BETWEEN 2000 AND 2100),
            month INTEGER NOT NULL CHECK (month BETWEEN 1 AND 12),
            snapshot_id INTEGER NOT NULL,
            updated_at_utc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
            PRIMARY KEY (cuil, year, month),
            FOREIGN KEY (snapshot_id) REFERENCES receipt_snapshots(snapshot_id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS idx_receipt_snapshots_lookup
            ON receipt_snapshots(cuil, year, month, version DESC);

        CREATE INDEX IF NOT EXISTS idx_receipt_snapshots_downloaded_at
            ON receipt_snapshots(downloaded_at_utc DESC);

        CREATE INDEX IF NOT EXISTS idx_receipt_latest_snapshot_id
            ON receipt_latest(snapshot_id);
        """;

    private readonly ILogger<SqliteReceiptCatalogCache> _logger;
    private readonly string _connectionString;
    private readonly string _databasePath;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public SqliteReceiptCatalogCache(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<SqliteReceiptCatalogCache> logger)
    {
        _logger = logger;

        var configuredPath = configuration["ReceiptCache:DatabasePath"];
        _databasePath = ResolveDatabasePath(configuredPath, hostEnvironment.ContentRootPath);
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        };
        _connectionString = builder.ToString();
    }

    public async Task<ReceiptCacheEntry?> GetLatestAsync(
        string cuil,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                s.snapshot_id,
                s.cuil,
                s.year,
                s.month,
                s.version,
                s.downloaded_at_utc,
                s.source_key,
                s.source_etag,
                s.source_version_id,
                s.payload_gzip
            FROM receipt_latest l
            INNER JOIN receipt_snapshots s ON s.snapshot_id = l.snapshot_id
            WHERE l.cuil = $cuil AND l.year = $year AND l.month = $month;
            """;
        command.Parameters.AddWithValue("$cuil", NormalizeCuil(cuil));
        command.Parameters.AddWithValue("$year", year);
        command.Parameters.AddWithValue("$month", month);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadCacheEntry(reader);
    }

    public async Task<IReadOnlyList<int>> GetAvailableYearsAsync(
        string cuil,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var years = new List<int>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT year
            FROM receipt_latest
            WHERE cuil = $cuil
            ORDER BY year DESC;
            """;
        command.Parameters.AddWithValue("$cuil", NormalizeCuil(cuil));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            years.Add(reader.GetInt32(0));
        }

        return years;
    }

    public async Task<IReadOnlyList<int>> GetAvailableMonthsAsync(
        string cuil,
        int year,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var months = new List<int>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT month
            FROM receipt_latest
            WHERE cuil = $cuil AND year = $year
            ORDER BY month DESC;
            """;
        command.Parameters.AddWithValue("$cuil", NormalizeCuil(cuil));
        command.Parameters.AddWithValue("$year", year);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            months.Add(reader.GetInt32(0));
        }

        return months;
    }

    public async Task<ReceiptCacheEntry> SaveNewVersionAsync(
        ReceiptCacheWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var normalizedCuil = NormalizeCuil(request.Cuil);
        var payloadBytes = Encoding.UTF8.GetBytes(request.PayloadJson);
        var compressedPayload = Compress(payloadBytes);
        var payloadHash = Convert.ToHexString(SHA256.HashData(payloadBytes));
        var downloadedAtUtc = request.DownloadedAtUtc.Kind == DateTimeKind.Utc
            ? request.DownloadedAtUtc
            : request.DownloadedAtUtc.ToUniversalTime();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var begin = connection.CreateCommand();
        begin.CommandText = "BEGIN IMMEDIATE;";
        await begin.ExecuteNonQueryAsync(cancellationToken);

        try
        {
            var version = await GetNextVersionAsync(connection, normalizedCuil, request.Year, request.Month, cancellationToken);
            var snapshotId = await InsertSnapshotAsync(
                connection,
                normalizedCuil,
                request.Year,
                request.Month,
                version,
                downloadedAtUtc,
                request.SourceKey,
                request.SourceEtag,
                request.SourceVersionId,
                compressedPayload,
                payloadHash,
                cancellationToken);

            await UpsertLatestAsync(connection, normalizedCuil, request.Year, request.Month, snapshotId, cancellationToken);

            await using var commit = connection.CreateCommand();
            commit.CommandText = "COMMIT;";
            await commit.ExecuteNonQueryAsync(cancellationToken);

            return new ReceiptCacheEntry(
                snapshotId,
                normalizedCuil,
                request.Year,
                request.Month,
                version,
                downloadedAtUtc,
                request.SourceKey,
                request.SourceEtag,
                request.SourceVersionId,
                request.PayloadJson);
        }
        catch
        {
            await using var rollback = connection.CreateCommand();
            rollback.CommandText = "ROLLBACK;";
            await rollback.ExecuteNonQueryAsync(cancellationToken);
            throw;
        }
    }

    private static string ResolveDatabasePath(string? configuredPath, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.Combine(contentRootPath, "App_Data", "receipt-cache.db");
        }

        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = SchemaSql;
            await command.ExecuteNonQueryAsync(cancellationToken);

            _initialized = true;
            _logger.LogInformation("Receipt cache schema ready at {DatabasePath}", _databasePath);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA busy_timeout = 5000;
            PRAGMA foreign_keys = ON;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }

    private static async Task<int> GetNextVersionAsync(
        SqliteConnection connection,
        string cuil,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(MAX(version), 0) + 1
            FROM receipt_snapshots
            WHERE cuil = $cuil AND year = $year AND month = $month;
            """;
        command.Parameters.AddWithValue("$cuil", cuil);
        command.Parameters.AddWithValue("$year", year);
        command.Parameters.AddWithValue("$month", month);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static async Task<long> InsertSnapshotAsync(
        SqliteConnection connection,
        string cuil,
        int year,
        int month,
        int version,
        DateTime downloadedAtUtc,
        string sourceKey,
        string? sourceEtag,
        string? sourceVersionId,
        byte[] payloadGzip,
        string payloadHash,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO receipt_snapshots
            (
                cuil,
                year,
                month,
                version,
                downloaded_at_utc,
                source_key,
                source_etag,
                source_version_id,
                payload_gzip,
                payload_sha256,
                payload_size_bytes
            )
            VALUES
            (
                $cuil,
                $year,
                $month,
                $version,
                $downloadedAtUtc,
                $sourceKey,
                $sourceEtag,
                $sourceVersionId,
                $payloadGzip,
                $payloadHash,
                $payloadSizeBytes
            );
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$cuil", cuil);
        command.Parameters.AddWithValue("$year", year);
        command.Parameters.AddWithValue("$month", month);
        command.Parameters.AddWithValue("$version", version);
        command.Parameters.AddWithValue("$downloadedAtUtc", downloadedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$sourceKey", sourceKey);
        command.Parameters.AddWithValue("$sourceEtag", (object?)sourceEtag ?? DBNull.Value);
        command.Parameters.AddWithValue("$sourceVersionId", (object?)sourceVersionId ?? DBNull.Value);
        command.Parameters.Add("$payloadGzip", SqliteType.Blob).Value = payloadGzip;
        command.Parameters.AddWithValue("$payloadHash", payloadHash);
        command.Parameters.AddWithValue("$payloadSizeBytes", payloadGzip.Length);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
    }

    private static async Task UpsertLatestAsync(
        SqliteConnection connection,
        string cuil,
        int year,
        int month,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO receipt_latest (cuil, year, month, snapshot_id, updated_at_utc)
            VALUES ($cuil, $year, $month, $snapshotId, $updatedAtUtc)
            ON CONFLICT(cuil, year, month)
            DO UPDATE SET
                snapshot_id = excluded.snapshot_id,
                updated_at_utc = excluded.updated_at_utc;
            """;
        command.Parameters.AddWithValue("$cuil", cuil);
        command.Parameters.AddWithValue("$year", year);
        command.Parameters.AddWithValue("$month", month);
        command.Parameters.AddWithValue("$snapshotId", snapshotId);
        command.Parameters.AddWithValue("$updatedAtUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static ReceiptCacheEntry ReadCacheEntry(SqliteDataReader reader)
    {
        var snapshotId = reader.GetInt64(0);
        var cuil = reader.GetString(1);
        var year = reader.GetInt32(2);
        var month = reader.GetInt32(3);
        var version = reader.GetInt32(4);
        var downloadedAtUtc = ParseUtcDateTime(reader.GetString(5));
        var sourceKey = reader.GetString(6);
        var sourceEtag = reader.IsDBNull(7) ? null : reader.GetString(7);
        var sourceVersionId = reader.IsDBNull(8) ? null : reader.GetString(8);
        var payloadGzip = (byte[])reader["payload_gzip"];
        var payloadJson = Decompress(payloadGzip);

        return new ReceiptCacheEntry(
            snapshotId,
            cuil,
            year,
            month,
            version,
            downloadedAtUtc,
            sourceKey,
            sourceEtag,
            sourceVersionId,
            payloadJson);
    }

    private static byte[] Compress(byte[] payload)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(payload, 0, payload.Length);
        }

        return output.ToArray();
    }

    private static string Decompress(byte[] payloadGzip)
    {
        using var input = new MemoryStream(payloadGzip);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string NormalizeCuil(string cuil)
    {
        return new string(cuil.Where(char.IsDigit).ToArray());
    }

    private static DateTime ParseUtcDateTime(string value)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
    }
}
