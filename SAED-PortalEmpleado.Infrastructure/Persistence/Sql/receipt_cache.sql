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
