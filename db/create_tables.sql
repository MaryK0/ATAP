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
('admin', 'admin@atap.com', 'admin123', 'admin')
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
CREATE OR REPLACE PROCEDURE calculate_airport_congestion()
LANGUAGE plpgsql
AS $$
DECLARE
    pos_count INTEGER;
BEGIN
    SELECT COUNT(*) INTO pos_count
    FROM dds.positions
    WHERE time_position >= NOW() - INTERVAL '1 hour';
    
    IF pos_count = 0 THEN
        RAISE NOTICE 'Нет позиций за последний час. Витрина не обновлена.';
        RETURN;
    END IF;
    
    INSERT INTO mart.mart_airport_congestion (
        airport_id, flight_id, callsign,
        entry_time, exit_time, duration_seconds,
        date_hour, is_departure, is_arrival,
        max_altitude_in_zone, min_altitude_in_zone
    )
    WITH
    positions_last_hour AS (
        SELECT *
        FROM dds.positions
        WHERE time_position >= NOW() - INTERVAL '1 hour'
    ),
    positions_with_zones AS (
        SELECT
            p.flight_id,
            p.time_position,
            p.altitude,
            a.airport_id,
            a.analysis_radius,
            ST_Distance(a.geom, p.geom) AS horizontal_dist,
            fa.relation_type
        FROM positions_last_hour p
        CROSS JOIN dds.airports a
        LEFT JOIN dds.flights_airports fa
            ON fa.flight_id = p.flight_id AND fa.airport_id = a.airport_id
    ),
    filtered_positions AS (
        SELECT
            flight_id,
            time_position,
            altitude,
            airport_id,
            relation_type
        FROM positions_with_zones
        WHERE horizontal_dist < analysis_radius AND altitude <= 610
    ),
    zone_groups AS (
        SELECT
            flight_id,
            airport_id,
            time_position,
            altitude,
            relation_type,
            ROW_NUMBER() OVER (PARTITION BY flight_id, airport_id ORDER BY time_position) -
            ROW_NUMBER() OVER (PARTITION BY flight_id, airport_id, 1 ORDER BY time_position) AS group_id
        FROM filtered_positions
    ),
    zone_intervals AS (
        SELECT
            flight_id,
            airport_id,
            MIN(time_position) AS entry_time,
            MAX(time_position) AS exit_time,
            MAX(altitude) AS max_altitude_in_zone,
            MIN(altitude) AS min_altitude_in_zone,
            MAX(relation_type) AS relation_type
        FROM zone_groups
        GROUP BY flight_id, airport_id, group_id
    )
    SELECT
        zi.airport_id,
        zi.flight_id,
        f.callsign,
        zi.entry_time,
        zi.exit_time,
        EXTRACT(EPOCH FROM (zi.exit_time - zi.entry_time))::INTEGER AS duration_seconds,
        DATE_TRUNC('hour', zi.entry_time) AS date_hour,
        CASE WHEN zi.relation_type = 'departure' THEN TRUE ELSE FALSE END AS is_departure,
        CASE WHEN zi.relation_type = 'arrival' THEN TRUE ELSE FALSE END AS is_arrival,
        zi.max_altitude_in_zone,
        zi.min_altitude_in_zone
    FROM zone_intervals zi
    JOIN dds.flights f ON f.flight_id = zi.flight_id;
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


CREATE TABLE IF NOT EXISTS dds.saved_reports (
    id          SERIAL PRIMARY KEY,
    user_id     INTEGER NOT NULL REFERENCES dds.users(user_id) ON DELETE CASCADE,
    airport_id  INTEGER NOT NULL REFERENCES dds.airports(airport_id),
    name        VARCHAR(100) NOT NULL,
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
