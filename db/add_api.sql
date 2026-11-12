-- Additive migration for WebLab#3; may be run on an existing database.
CREATE TABLE IF NOT EXISTS dds.report_exports (
    id UUID PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES dds.users(user_id) ON DELETE CASCADE,
    airport_id INTEGER REFERENCES dds.airports(airport_id),
    date_from DATE NOT NULL,
    date_to DATE NOT NULL,
    status VARCHAR(10) NOT NULL DEFAULT 'queued'
        CHECK (status IN ('queued','running','completed','failed')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    lease_until TIMESTAMPTZ,
    result BYTEA,
    error TEXT,
    CHECK (date_to >= date_from AND date_to - date_from < 31)
);
CREATE INDEX IF NOT EXISTS idx_report_exports_queue ON dds.report_exports(status, created_at);

-- Repair only the original plaintext demonstration seed; never overwrite a user's chosen hash.
UPDATE dds.users
SET password_hash = 'pbkdf2-sha256$120000$jASZdR/ox++lleFc92FBaQ==$N4Chr5AV2f/4N4iNFsRbz9WGR/UOOjC/6QrPFG8IHaw='
WHERE username = 'admin' AND password_hash = 'admin123';
