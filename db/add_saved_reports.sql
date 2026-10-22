-- Lab 8: Сохранённые отчёты аналитика
CREATE TABLE IF NOT EXISTS dds.saved_reports (
    id          SERIAL PRIMARY KEY,
    user_id     INTEGER NOT NULL REFERENCES dds.users(user_id) ON DELETE CASCADE,
    name        VARCHAR(100) NOT NULL,
    airport_id  INTEGER NOT NULL REFERENCES dds.airports(airport_id),
    date_from   DATE NOT NULL,
    date_to     DATE NOT NULL,
    created_at  TIMESTAMPTZ DEFAULT NOW()
);

ALTER TABLE dds.saved_reports
    ADD CONSTRAINT check_saved_reports_dates CHECK (date_from <= date_to);

ALTER TABLE dds.saved_reports
    ADD CONSTRAINT check_saved_reports_name CHECK (LENGTH(TRIM(name)) > 0);

GRANT SELECT, INSERT, DELETE ON dds.saved_reports TO analyst;
GRANT USAGE, SELECT ON SEQUENCE dds.saved_reports_id_seq TO analyst;
