-- =====================================================
-- ATAP: Полный скрипт создания базы данных
-- =====================================================

CREATE EXTENSION IF NOT EXISTS postgis;

-- =====================================================
-- 1. stg (сырые данные)
-- =====================================================
CREATE SCHEMA IF NOT EXISTS stg;

CREATE TABLE IF NOT EXISTS stg.stg_positions (
    id BIGSERIAL PRIMARY KEY,
    raw_data JSONB NOT NULL,
    loaded_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS stg.stg_flights (
    id BIGSERIAL PRIMARY KEY,
    raw_data JSONB NOT NULL,
    loaded_at TIMESTAMPTZ DEFAULT NOW()
);

-- =====================================================
-- 2. dds (очищенные данные)
-- =====================================================
CREATE SCHEMA IF NOT EXISTS dds;

CREATE TABLE IF NOT EXISTS dds.users (
    user_id SERIAL PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    email VARCHAR(100) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    role VARCHAR(20),
    created_at TIMESTAMPTZ DEFAULT NOW(),
    loaded_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS dds.aircrafts (
    aircraft_id SERIAL PRIMARY KEY,
    icao24 VARCHAR(6) UNIQUE NOT NULL,
    registration VARCHAR(50),
    model VARCHAR(100),
    manufacturer VARCHAR(100),
    origin_country VARCHAR(100),
    loaded_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS dds.airports (
    airport_id SERIAL PRIMARY KEY,
    icao_code CHAR(4) UNIQUE NOT NULL,
    iata_code CHAR(3),
    name VARCHAR(200) NOT NULL,
    city VARCHAR(100),
    country_code CHAR(2),
    latitude NUMERIC(9,6),
    longitude NUMERIC(9,6),
    geom GEOMETRY(POINT, 4326),
    analysis_radius INTEGER DEFAULT 10000,
    airport_type VARCHAR(20),
    elevation INTEGER,
    loaded_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS dds.flights (
    flight_id SERIAL PRIMARY KEY,
    callsign VARCHAR(10) NOT NULL,
    aircraft_id INTEGER REFERENCES dds.aircrafts(aircraft_id),
    first_seen TIMESTAMPTZ,
    last_seen TIMESTAMPTZ,
    loaded_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS dds.positions (
    position_id BIGSERIAL PRIMARY KEY,
    flight_id INTEGER REFERENCES dds.flights(flight_id),
    time_position TIMESTAMPTZ NOT NULL,
    latitude NUMERIC(9,6),
    longitude NUMERIC(9,6),
    geom GEOMETRY(POINT, 4326),
    altitude INTEGER,
    velocity NUMERIC(5,1),
    heading SMALLINT,
    on_ground BOOLEAN,
    loaded_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS dds.flights_airports (
    flight_id INTEGER REFERENCES dds.flights(flight_id),
    airport_id INTEGER REFERENCES dds.airports(airport_id),
    relation_type VARCHAR(10),
    event_time TIMESTAMPTZ NOT NULL,
    distance_meters INTEGER,
    loaded_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (flight_id, airport_id, relation_type, event_time)
);

CREATE TABLE IF NOT EXISTS dds.saved_reports (
    id          SERIAL PRIMARY KEY,
    user_id     INTEGER NOT NULL REFERENCES dds.users(user_id) ON DELETE CASCADE,
    airport_id  INTEGER NOT NULL REFERENCES dds.airports(airport_id),
    name        VARCHAR(100) NOT NULL,
    date_from   DATE NOT NULL,
    date_to     DATE NOT NULL,
    created_at  TIMESTAMPTZ DEFAULT NOW()
);

-- Persistent asynchronous export jobs (WebLab#3).
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

-- =====================================================
-- 3. mart (аналитические витрины)
-- =====================================================
CREATE SCHEMA IF NOT EXISTS mart;

-- Материализованное представление для карты (обновляется каждые 5 минут)
CREATE MATERIALIZED VIEW mart.current_flights_map AS
SELECT 
    f.flight_id,
    a.icao24,
    f.callsign,
    a.origin_country,
    a.model AS aircraft_model,
    p.latitude,
    p.longitude,
    ST_SetSRID(ST_MakePoint(p.longitude, p.latitude), 4326) AS geom,
    ST_GeoHash(ST_SetSRID(ST_MakePoint(p.longitude, p.latitude), 4326), 6) AS geohash,
    p.altitude,
    p.velocity,
    p.heading AS true_track,
    p.on_ground,
    CASE 
        WHEN p.heading >= 337.5 OR p.heading < 22.5 THEN 'N'
        WHEN p.heading >= 22.5 AND p.heading < 67.5 THEN 'NE'
        WHEN p.heading >= 67.5 AND p.heading < 112.5 THEN 'E'
        WHEN p.heading >= 112.5 AND p.heading < 157.5 THEN 'SE'
        WHEN p.heading >= 157.5 AND p.heading < 202.5 THEN 'S'
        WHEN p.heading >= 202.5 AND p.heading < 247.5 THEN 'SW'
        WHEN p.heading >= 247.5 AND p.heading < 292.5 THEN 'W'
        WHEN p.heading >= 292.5 AND p.heading < 337.5 THEN 'NW'
        ELSE 'Unknown'
    END AS heading_text,
    CASE WHEN p.on_ground THEN 'on ground' ELSE 'airborne' END AS flight_status,
    p.time_position AS position_time,
    p.loaded_at AS last_contact_time,
    p.loaded_at + INTERVAL '15 minutes' AS expires_at,
    NULL AS departure_airport,
    NULL AS arrival_airport,
    p.loaded_at
FROM dds.flights f
JOIN dds.aircrafts a ON f.aircraft_id = a.aircraft_id
JOIN dds.positions p ON f.flight_id = p.flight_id
WHERE p.time_position > NOW() - INTERVAL '15 minutes'
ORDER BY p.flight_id, p.time_position DESC;

-- Индекс для материализованного представления
CREATE INDEX idx_current_flights_map_geom ON mart.current_flights_map USING GIST(geom);
CREATE INDEX idx_current_flights_map_flight_id ON mart.current_flights_map(flight_id);

-- Обычная таблица для витрины загруженности аэропортов
CREATE TABLE IF NOT EXISTS mart.mart_airport_congestion (
    id BIGSERIAL PRIMARY KEY,
    airport_id INTEGER REFERENCES dds.airports(airport_id),
    flight_id INTEGER REFERENCES dds.flights(flight_id),
    callsign VARCHAR(10),
    entry_time TIMESTAMPTZ,
    exit_time TIMESTAMPTZ,
    duration_seconds INTEGER,
    date_hour TIMESTAMPTZ,
    is_departure BOOLEAN,
    is_arrival BOOLEAN,
    max_altitude_in_zone INTEGER,
    min_altitude_in_zone INTEGER,
    loaded_at TIMESTAMPTZ DEFAULT NOW()
);

-- =====================================================
-- 4. Ограничения целостности
-- =====================================================

ALTER TABLE dds.users ADD CONSTRAINT check_users_role CHECK (role IN ('guest', 'analyst', 'admin'));

ALTER TABLE dds.airports ADD CONSTRAINT check_airports_latitude CHECK (latitude BETWEEN -90 AND 90);
ALTER TABLE dds.airports ADD CONSTRAINT check_airports_longitude CHECK (longitude BETWEEN -180 AND 180);
ALTER TABLE dds.airports ADD CONSTRAINT check_airports_geom_valid CHECK (ST_IsValid(geom));
ALTER TABLE dds.airports ADD CONSTRAINT check_airports_analysis_radius CHECK (analysis_radius > 0);
ALTER TABLE dds.airports ADD CONSTRAINT check_airports_type CHECK (airport_type IN ('large', 'medium', 'small'));
ALTER TABLE dds.airports ADD CONSTRAINT check_airports_elevation CHECK (elevation >= -500);

ALTER TABLE dds.aircrafts ADD CONSTRAINT check_aircrafts_icao24_format CHECK (icao24 ~ '^[A-F0-9]{6}$');

ALTER TABLE dds.positions ADD CONSTRAINT check_positions_altitude CHECK (altitude >= 0);
ALTER TABLE dds.positions ADD CONSTRAINT check_positions_velocity CHECK (velocity > 0);
ALTER TABLE dds.positions ADD CONSTRAINT check_positions_heading CHECK (heading BETWEEN 0 AND 360);
ALTER TABLE dds.positions ADD CONSTRAINT check_positions_latitude CHECK (latitude BETWEEN -90 AND 90);
ALTER TABLE dds.positions ADD CONSTRAINT check_positions_longitude CHECK (longitude BETWEEN -180 AND 180);
ALTER TABLE dds.positions ADD CONSTRAINT check_positions_geom_valid CHECK (ST_IsValid(geom));

ALTER TABLE dds.flights_airports ADD CONSTRAINT check_flights_airports_relation_type CHECK (relation_type IN ('departure', 'arrival'));
ALTER TABLE dds.flights_airports ADD CONSTRAINT check_flights_airports_distance CHECK (distance_meters >= 0);

ALTER TABLE dds.saved_reports ADD CONSTRAINT check_saved_reports_dates CHECK (date_from <= date_to);
ALTER TABLE dds.saved_reports ADD CONSTRAINT check_saved_reports_name CHECK (LENGTH(TRIM(name)) > 0);

ALTER TABLE mart.mart_airport_congestion ADD CONSTRAINT check_congestion_duration CHECK (duration_seconds >= 0);
ALTER TABLE mart.mart_airport_congestion ADD CONSTRAINT check_congestion_entry_exit CHECK (entry_time < exit_time);
ALTER TABLE mart.mart_airport_congestion ADD CONSTRAINT check_congestion_max_min_altitude CHECK (max_altitude_in_zone >= min_altitude_in_zone);
ALTER TABLE mart.mart_airport_congestion ADD CONSTRAINT check_congestion_max_altitude CHECK (max_altitude_in_zone >= 0);
ALTER TABLE mart.mart_airport_congestion ADD CONSTRAINT check_congestion_min_altitude CHECK (min_altitude_in_zone >= 0);

-- =====================================================
-- 5. Создание ролей
-- =====================================================
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'guest') THEN
        CREATE ROLE guest WITH LOGIN PASSWORD 'guest_pass' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
    END IF;
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'analyst') THEN
        CREATE ROLE analyst WITH LOGIN PASSWORD 'analyst_pass' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
    END IF;
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'admin') THEN
        CREATE ROLE admin WITH LOGIN PASSWORD 'admin_pass' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
    END IF;
END
$$;

-- =====================================================
-- 6. Назначение прав доступа
-- =====================================================
GRANT CONNECT ON DATABASE atap_db TO guest;
GRANT SELECT ON mart.current_flights_map TO guest;

GRANT CONNECT ON DATABASE atap_db TO analyst;
GRANT SELECT ON dds.airports, dds.flights, dds.positions TO analyst;
GRANT SELECT ON mart.current_flights_map, mart.mart_airport_congestion TO analyst;
GRANT SELECT, INSERT, DELETE ON dds.saved_reports TO analyst;
GRANT USAGE, SELECT ON SEQUENCE dds.saved_reports_id_seq TO analyst;

GRANT CONNECT ON DATABASE atap_db TO admin;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA stg, dds TO admin;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA mart TO admin;
GRANT SELECT ON mart.current_flights_map TO admin;

-- =====================================================
-- 7. Тестовые данные
-- =====================================================

-- 1. Пользователи
INSERT INTO dds.users (username, email, password_hash, role) VALUES
-- Demo password: Admin#2026. Only the salted PBKDF2 hash is stored.
('admin', 'admin@atap.com', 'pbkdf2-sha256$120000$jASZdR/ox++lleFc92FBaQ==$N4Chr5AV2f/4N4iNFsRbz9WGR/UOOjC/6QrPFG8IHaw=', 'admin')
ON CONFLICT (username) DO NOTHING;

-- 2. Аэропорты
INSERT INTO dds.airports (icao_code, iata_code, name, city, country_code, latitude, longitude, airport_type) VALUES
('UUEE', 'SVO', 'Sheremetyevo', 'Moscow', 'RU', 55.972, 37.415, 'large'),
('UUDD', 'DME', 'Domodedovo', 'Moscow', 'RU', 55.409, 37.902, 'large'),
('UUWW', 'VKO', 'Vnukovo', 'Moscow', 'RU', 55.592, 37.267, 'medium')
ON CONFLICT (icao_code) DO NOTHING;

-- 3. Воздушные суда
INSERT INTO dds.aircrafts (icao24, registration, model, manufacturer, origin_country) VALUES
('4242D2', 'RA-12345', '737-800', 'Boeing', 'Russia')
ON CONFLICT (icao24) DO NOTHING;

-- 4. Рейсы
INSERT INTO dds.flights (flight_id, callsign, aircraft_id, first_seen, last_seen) VALUES
(1, 'AFL123', 1, NOW(), NOW())
ON CONFLICT (flight_id) DO NOTHING;

-- 5. Позиции
INSERT INTO dds.positions (flight_id, time_position, latitude, longitude, altitude, velocity, heading, on_ground) VALUES
(1, NOW(), 55.752, 37.615, 10000, 250.0, 90, false);

-- Обновляем материализованное представление
REFRESH MATERIALIZED VIEW mart.current_flights_map;

-- =====================================================
-- 8. Функция и триггеры для loaded_at
-- =====================================================
CREATE OR REPLACE FUNCTION update_loaded_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.loaded_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trigger_update_loaded_at ON stg.stg_positions;
CREATE TRIGGER trigger_update_loaded_at BEFORE INSERT OR UPDATE ON stg.stg_positions FOR EACH ROW EXECUTE FUNCTION update_loaded_at_column();

DROP TRIGGER IF EXISTS trigger_update_loaded_at ON stg.stg_flights;
CREATE TRIGGER trigger_update_loaded_at BEFORE INSERT OR UPDATE ON stg.stg_flights FOR EACH ROW EXECUTE FUNCTION update_loaded_at_column();

DROP TRIGGER IF EXISTS trigger_update_loaded_at ON dds.users;
CREATE TRIGGER trigger_update_loaded_at BEFORE INSERT OR UPDATE ON dds.users FOR EACH ROW EXECUTE FUNCTION update_loaded_at_column();

DROP TRIGGER IF EXISTS trigger_update_loaded_at ON dds.aircrafts;
CREATE TRIGGER trigger_update_loaded_at BEFORE INSERT OR UPDATE ON dds.aircrafts FOR EACH ROW EXECUTE FUNCTION update_loaded_at_column();

DROP TRIGGER IF EXISTS trigger_update_loaded_at ON dds.airports;
CREATE TRIGGER trigger_update_loaded_at BEFORE INSERT OR UPDATE ON dds.airports FOR EACH ROW EXECUTE FUNCTION update_loaded_at_column();

DROP TRIGGER IF EXISTS trigger_update_loaded_at ON dds.flights;
CREATE TRIGGER trigger_update_loaded_at BEFORE INSERT OR UPDATE ON dds.flights FOR EACH ROW EXECUTE FUNCTION update_loaded_at_column();

DROP TRIGGER IF EXISTS trigger_update_loaded_at ON dds.positions;
CREATE TRIGGER trigger_update_loaded_at BEFORE INSERT OR UPDATE ON dds.positions FOR EACH ROW EXECUTE FUNCTION update_loaded_at_column();

DROP TRIGGER IF EXISTS trigger_update_loaded_at ON dds.flights_airports;
CREATE TRIGGER trigger_update_loaded_at BEFORE INSERT OR UPDATE ON dds.flights_airports FOR EACH ROW EXECUTE FUNCTION update_loaded_at_column();

DROP TRIGGER IF EXISTS trigger_update_loaded_at ON mart.mart_airport_congestion;
CREATE TRIGGER trigger_update_loaded_at BEFORE INSERT OR UPDATE ON mart.mart_airport_congestion FOR EACH ROW EXECUTE FUNCTION update_loaded_at_column();

-- =====================================================
-- 9. Хранимая процедура расчета загруженности аэропортов
-- =====================================================
-- Установка исправленного расчёта. Данные витрины этот файл не пересчитывает.
-- Координаты карты приоритетны: сохранённый geom может быть устаревшим.
-- p_from/p_to выбирают рейсы с наблюдениями в [p_from, p_to).
-- Для них пересчитывается ВЕСЬ имеющийся трек. Хранить исходные позиции
-- необходимо: агрегаты выбранных рейсов заменяются, а не дописываются.
CREATE INDEX IF NOT EXISTS idx_positions_congestion_flight_time
    ON dds.positions (flight_id, time_position, position_id);
CREATE INDEX IF NOT EXISTS idx_positions_congestion_coordinates_geography
    ON dds.positions USING gist
    ((COALESCE(ST_SetSRID(ST_MakePoint(longitude, latitude), 4326), geom)::geography));

CREATE OR REPLACE PROCEDURE calculate_airport_congestion(p_from timestamptz, p_to timestamptz)
LANGUAGE plpgsql
AS $$
DECLARE
    affected_flights integer[];
BEGIN
    IF p_from IS NULL OR p_to IS NULL OR p_from >= p_to THEN
        RAISE EXCEPTION 'Ожидается непустой период: p_from < p_to' USING ERRCODE = '22023';
    END IF;
    -- Замена агрегатов атомарна; два одновременно запущенных расчёта не дублируют строки.
    PERFORM pg_advisory_xact_lock(hashtext('atap:airport-congestion'));
    SELECT array_agg(DISTINCT flight_id) INTO affected_flights
    FROM dds.positions
    WHERE time_position >= p_from AND time_position < p_to AND flight_id IS NOT NULL;
    IF affected_flights IS NULL THEN RETURN; END IF;

    DELETE FROM mart.mart_airport_congestion WHERE flight_id = ANY(affected_flights);

    INSERT INTO mart.mart_airport_congestion
        (airport_id, flight_id, callsign, entry_time, exit_time, duration_seconds,
         date_hour, is_departure, is_arrival, max_altitude_in_zone, min_altitude_in_zone)
    WITH positions AS MATERIALIZED (
        -- При двух наблюдениях в один момент выбираем последнее по position_id.
        SELECT DISTINCT ON (flight_id, time_position)
            flight_id, time_position, altitude,
            COALESCE(ST_SetSRID(ST_MakePoint(longitude, latitude), 4326), geom)::geography AS location
        FROM dds.positions WHERE flight_id = ANY(affected_flights)
        ORDER BY flight_id, time_position, position_id DESC
    ), airports AS (
        SELECT airport_id, analysis_radius,
            COALESCE(ST_SetSRID(ST_MakePoint(longitude, latitude), 4326), geom)::geography AS location
        FROM dds.airports
    ), candidates AS (
        -- Пространственный отбор вместо сравнения расстояния в градусах с метрами.
        SELECT DISTINCT p.flight_id, a.airport_id
        FROM dds.positions p JOIN airports a
          ON ST_DWithin(COALESCE(ST_SetSRID(ST_MakePoint(p.longitude, p.latitude), 4326), p.geom)::geography,
                        a.location, a.analysis_radius)
        WHERE p.flight_id = ANY(affected_flights) AND p.altitude <= 610
    ), classified AS (
        -- Внешние/высокие точки остаются в последовательности и разделяют посещения.
        SELECT c.flight_id, c.airport_id, p.time_position, p.altitude,
            COALESCE(p.altitude <= 610 AND
                ST_DWithin(p.location, a.location, a.analysis_radius) AND
                ST_Distance(p.location, a.location) < a.analysis_radius, false) AS in_zone
        FROM candidates c JOIN airports a USING (airport_id)
        JOIN positions p ON p.flight_id = c.flight_id
    ), previous AS (
        SELECT *, lag(in_zone, 1, false) OVER w AS was_inside
        FROM classified
        WINDOW w AS (PARTITION BY flight_id, airport_id ORDER BY time_position)
    ), numbered AS (
        SELECT *, sum(CASE WHEN in_zone AND NOT was_inside THEN 1 ELSE 0 END)
            OVER (PARTITION BY flight_id, airport_id ORDER BY time_position) AS visit
        FROM previous
    ), intervals AS (
        SELECT flight_id, airport_id, min(time_position) AS entry_time,
            max(time_position) AS exit_time, max(altitude) AS max_altitude,
            min(altitude) AS min_altitude
        FROM numbered WHERE in_zone
        GROUP BY flight_id, airport_id, visit
        -- Одна точка не подтверждает длительность пребывания.
        HAVING min(time_position) < max(time_position)
    )
    SELECT i.airport_id, i.flight_id, f.callsign, i.entry_time, i.exit_time,
        extract(epoch FROM i.exit_time - i.entry_time)::integer,
        date_trunc('hour', i.entry_time, 'UTC'),
        EXISTS (SELECT 1 FROM dds.flights_airports fa WHERE fa.flight_id = i.flight_id
            AND fa.airport_id = i.airport_id AND fa.relation_type = 'departure'
            AND fa.event_time BETWEEN i.entry_time AND i.exit_time),
        EXISTS (SELECT 1 FROM dds.flights_airports fa WHERE fa.flight_id = i.flight_id
            AND fa.airport_id = i.airport_id AND fa.relation_type = 'arrival'
            AND fa.event_time BETWEEN i.entry_time AND i.exit_time),
        i.max_altitude, i.min_altitude
    FROM intervals i JOIN dds.flights f USING (flight_id);
END;
$$;

-- Совместимость прежнего вызова: выбирает рейсы с точками за последний час.
CREATE OR REPLACE PROCEDURE calculate_airport_congestion()
LANGUAGE plpgsql
AS $$
BEGIN
    CALL calculate_airport_congestion(now() - interval '1 hour', now());
END;
$$;

-- =====================================================
-- 10. Функция для обновления материализованного представления
-- =====================================================
CREATE OR REPLACE FUNCTION refresh_current_flights_map()
RETURNS void AS $$
BEGIN
    REFRESH MATERIALIZED VIEW CONCURRENTLY mart.current_flights_map;
END;
$$ LANGUAGE plpgsql;


