-- Демо-данные: 100 рейсов на карте
-- Используем CTE чтобы сразу получить flight_id без DO-блока

-- Удаляем мусорные позиции без рейса
DELETE FROM dds.positions WHERE flight_id IS NULL;

-- Сбрасываем последовательность на максимальный существующий ID
SELECT setval('dds.flights_flight_id_seq', (SELECT MAX(flight_id) FROM dds.flights));

WITH new_aircrafts AS (
    INSERT INTO dds.aircrafts (icao24, registration, model, manufacturer, origin_country)
    SELECT
        UPPER(LPAD(TO_HEX(700000 + g), 6, '0')),
        'DEMO-' || g,
        (ARRAY['A320','B737','A321','B777','A319','A330','B787','A350','E190','B747'])[(g % 10) + 1],
        CASE (g % 2) WHEN 0 THEN 'Airbus' ELSE 'Boeing' END,
        'Demo'
    FROM generate_series(1, 100) AS g
    ON CONFLICT (icao24) DO UPDATE SET registration = EXCLUDED.registration
    RETURNING icao24, aircraft_id
),
new_flights AS (
    INSERT INTO dds.flights (callsign, aircraft_id, first_seen, last_seen)
    SELECT
        (ARRAY['DLH','BAW','AFR','KLM','EIN','SWR','UAE','THY','IBE','LOT',
               'AZA','TAP','SAS','FIN','CSN','AFL','UAL','AAL','DAL','RYR'])[(g % 20) + 1]
            || LPAD(g::TEXT, 3, '0'),
        a.aircraft_id,
        NOW() - INTERVAL '3 hours',
        NOW()
    FROM generate_series(1, 100) AS g
    JOIN new_aircrafts a ON a.icao24 = UPPER(LPAD(TO_HEX(700000 + g), 6, '0'))
    RETURNING flight_id, callsign
)
INSERT INTO dds.positions
    (flight_id, time_position, latitude, longitude, altitude, velocity, heading, on_ground)
SELECT
    f.flight_id,
    NOW(),
    -- широта 36–67°N: неравномерно через sin/cos
    36.0 + 31.0 * (0.5 + 0.5 * SIN(g * 2.399)),
    -- долгота -9–39°E: другая частота
    -9.0 + 48.0 * (0.5 + 0.5 * COS(g * 1.618)),
    8000 + (g % 12) * 400,
    180.0 + (g % 20) * 5,
    ((g * 73 + 17) % 360)::SMALLINT,
    false
FROM generate_series(1, 100) AS g
JOIN new_flights f ON f.callsign =
    (ARRAY['DLH','BAW','AFR','KLM','EIN','SWR','UAE','THY','IBE','LOT',
           'AZA','TAP','SAS','FIN','CSN','AFL','UAL','AAL','DAL','RYR'])[(g % 20) + 1]
    || LPAD(g::TEXT, 3, '0');

-- Обновляем витрину
REFRESH MATERIALIZED VIEW mart.current_flights_map;

SELECT COUNT(*) AS flights_on_map FROM mart.current_flights_map;
