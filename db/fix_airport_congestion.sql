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
