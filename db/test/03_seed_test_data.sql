-- Тестовые данные для интеграционных и E2E-тестов.
-- Выполняется после db/create_tables.sql (структура + базовые сиды: пользователь admin,
-- аэропорты UUEE=1, UUDD=2, UUWW=3, судно 1, рейс 1 AFL123).
-- Набор детерминирован: все значения фиксированы, поэтому тесты проверяют точные числа.
-- Единственные значения, зависящие от времени, — позиции для карты (относительно NOW()).

-- Пользователь-аналитик (хэш-заглушка: репозиторные тесты пароль не проверяют)
INSERT INTO dds.users (username, email, password_hash, role) VALUES
('analyst1', 'analyst1@atap.com', 'analyst1-hash', 'analyst')
ON CONFLICT (username) DO NOTHING;

-- Суда
INSERT INTO dds.aircrafts (icao24, registration, model, manufacturer, origin_country) VALUES
('400A1B', 'G-TEST1', 'A320-232', 'Airbus', 'United Kingdom'),
('3C6752', 'D-AIBA', 'A319-112', 'Airbus', 'Germany'),
('4B1814', 'HB-JHA', 'A220-300', 'Airbus', 'Switzerland')
ON CONFLICT (icao24) DO NOTHING;

-- Рейсы (рейс 1 AFL123 уже есть в базовых сидах)
INSERT INTO dds.flights (flight_id, callsign, aircraft_id, first_seen, last_seen) VALUES
(2, 'BAW001', 2, '2026-06-10 06:00:00+00', '2026-06-10 08:00:00+00'),
(3, 'DLH002', 3, '2026-06-10 07:00:00+00', '2026-06-10 09:00:00+00'),
(4, 'SWR003', 4, '2026-06-09 22:00:00+00', '2026-06-09 23:00:00+00')
ON CONFLICT (flight_id) DO NOTHING;

-- Позиции для карты: рейсы 2 и 3 «видны» (5 минут назад), рейс 4 устарел (2 часа назад)
INSERT INTO dds.positions (flight_id, time_position, latitude, longitude, altitude, velocity, heading, on_ground) VALUES
(2, NOW() - INTERVAL '5 minutes', 51.470, -0.454, 3200, 210.5, 245, false),
(3, NOW() - INTERVAL '4 minutes', 50.038, 8.562, 2800, 190.0, 90, false),
(4, NOW() - INTERVAL '2 hours', 47.458, 8.548, 1500, 150.0, 180, false);

REFRESH MATERIALIZED VIEW mart.current_flights_map;

-- Витрина загруженности. Аэропорт 1, 10 июня 2026 (период [10.06 00:00; 11.06 00:00)):
--   B (на границе начала), C, D, E (23:59:59) — внутри; A (до начала) и F (на границе конца) — вне.
--   Внутри периода: вылетов 1 (C), прилётов 3 (B, D, E), всего 4.
-- Аэропорт 2: одна запись за те же сутки (проверка фильтра по аэропорту). Аэропорт 3: данных нет.
INSERT INTO mart.mart_airport_congestion
    (airport_id, flight_id, callsign, entry_time, exit_time, duration_seconds, date_hour,
     is_departure, is_arrival, max_altitude_in_zone, min_altitude_in_zone) VALUES
(1, 4, 'SWR003', '2026-06-09 23:59:59+00', '2026-06-10 00:02:00+00', 121, '2026-06-09 23:00:00+00', true,  false, 500, 300),
(1, 1, 'AFL123', '2026-06-10 00:00:00+00', '2026-06-10 00:03:00+00', 180, '2026-06-10 00:00:00+00', false, true,  540, 310),
(1, 2, 'BAW001', '2026-06-10 07:06:00+00', '2026-06-10 07:08:00+00', 120, '2026-06-10 07:00:00+00', true,  false, 480, 250),
(1, 3, 'DLH002', '2026-06-10 12:30:00+00', '2026-06-10 12:40:00+00', 600, '2026-06-10 12:00:00+00', false, true,  600, 200),
(1, 1, 'AFL123', '2026-06-10 23:59:59+00', '2026-06-11 00:05:00+00', 301, '2026-06-10 23:00:00+00', false, true,  450, 180),
(1, 2, 'BAW001', '2026-06-11 00:00:00+00', '2026-06-11 00:04:00+00', 240, '2026-06-11 00:00:00+00', true,  false, 520, 260),
(2, 3, 'DLH002', '2026-06-10 10:00:00+00', '2026-06-10 10:05:00+00', 300, '2026-06-10 10:00:00+00', false, true,  580, 290);

-- Сохранённые отчёты аналитика: два отчёта по аэропорту 1
INSERT INTO dds.saved_reports (user_id, airport_id, name, date_from, date_to)
SELECT u.user_id, 1, 'Шереметьево, 10 июня', DATE '2026-06-10', DATE '2026-06-10'
FROM dds.users u WHERE u.username = 'analyst1';
INSERT INTO dds.saved_reports (user_id, airport_id, name, date_from, date_to)
SELECT u.user_id, 1, 'Шереметьево, неделя', DATE '2026-06-04', DATE '2026-06-10'
FROM dds.users u WHERE u.username = 'analyst1';
