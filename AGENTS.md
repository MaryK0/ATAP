# AGENTS.md — контекст для ИИ-агента (ATAP)

> Контекст для ИИ-инструментов, работающих с репозиторием **Air Traffic Analytics Platform**.
> Описание продукта — [`README.md`](README.md).

## 1. Цель проекта

Веб-приложение для аналитики воздушного движения: карта текущих позиций воздушных судов (данные
OpenSky) и аналитическая витрина загруженности аэропортов (`mart_airport_congestion`) с выгрузкой
CSV и сохранением отчётов. Роли: **guest** (аноним), **analyst**, **admin**.

Цель, проблема и возможность — [`README.md` §1](README.md#1-цель-проблема-и-возможность).

## 2. Словарь предметной области

| Термин | Значение |
|---|---|
| **Воздушное судно** (aircraft) | борт: `icao24` (6 hex, `^[A-F0-9]{6}$`), модель, производитель, страна регистрации |
| **Рейс** (flight) | `callsign`, привязка к судну, `first_seen` / `last_seen` |
| **Позиция** (position) | точка трека: координаты, `geom` (PostGIS POINT 4326), высота, скорость, курс (0..360), `on_ground` |
| **Аэропорт** (airport) | `icao_code` (4), `iata_code` (3), координаты, `analysis_radius` в метрах (по умолч. 10000), тип `large/medium/small` |
| **Зона аэропорта** | окружность радиуса `analysis_radius` вокруг аэропорта; судно «в зоне», если расстояние < `analysis_radius` и `altitude <= 610` м. Расстояние считается в метрах через `geography` (сейчас процедура витрины считает `ST_Distance` по `GEOMETRY(4326)` в градусах — требует исправления) |
| **Аналитическая витрина** (congestion) | `mart_airport_congestion`: интервалы пребывания судна в зоне — `entry_time`/`exit_time`, `duration_seconds`, `date_hour`, `is_departure`/`is_arrival` |
| **Карта** (map) | сессионное состояние просмотра: центр, зум, bbox, фильтры. В БД не хранится |
| **Сохранённый отчёт** (saved report) | параметры аналитики: имя + аэропорт + период (`date_from ≤ date_to`) |
| **Пользователь** (user) | `username` (UK), `email` (UK), `password_hash`, `role` (в БД пишутся только `analyst`/`admin`; `CHECK` в DDL допускает и `guest`) |
| **stg** | сырые данные из OpenSky (`raw_data JSONB`) |
| **dds** | очищенные нормализованные данные (OLTP) |
| **mart** | аналитические витрины и materialized view карты |

## 3. Границы системы

**Входит в ATAP:**

- Веб-приложение (Blazor Server): карта, аналитика, сохранённые отчёты, управление пользователями.
- Технологический UI (`ATAP.ConsoleUI`).
- Слои бизнес-логики и доступа к данным (общие для обоих UI).
- PostgreSQL + PostGIS (основное хранилище), Redis (опциональный кэш).
- Для администратора — переход в интерфейс Airflow для мониторинга загрузки данных.

**За границей ATAP:**

- Управление Airflow (вход, перезапуск DAG, параметры) — в родном интерфейсе Airflow.
- Сбор данных из OpenSky/OpenFlights — DAG Airflow + dbt (спроектированы, в этом репозитории нет).
- Картографические тайлы — внешний сервис OpenStreetMap.

## 4. Ссылки на требования, сценарии и ADR

| Материал | Где |
|---|---|
| Роли | [`README.md` §2](README.md#2-роли) |
| Функциональные требования FR-01…FR-20 | [`README.md` §3.1](README.md#31-функциональные-требования) |
| Нефункциональные требования NFR-P1, NFR-R2, NFR-S1 | [`README.md` §3.2](README.md#32-нефункциональные-требования) |
| Use-case | [`README.md` §4](README.md#4-use-case-диаграмма), исходник `web_lr1/diagrams/use_case.puml` |
| BPMN | [`README.md` §5](README.md#5-bpmn-основных-бизнес-процессов), только PNG в `web_lr1/diagrams/` |
| Пользовательские сценарии и критерии приёмки | [`README.md` §6](README.md#6-пользовательские-сценарии) |
| ER | [`README.md` §7](README.md#7-er-диаграмма-сущностей), исходник `web_lr1/diagrams/er.puml` |
| Стек | [`README.md` §8](README.md#8-технологический-стек) |
| Схема БД | [`README.md` §9](README.md#9-диаграмма-базы-данных), исходник — DDL `db/create_tables.sql` |
| C4 Context/Container/Component (+ L4) | [`README.md` §10](README.md#10-диаграммы-c4), исходники `web_lr1/diagrams/c4/*.puml` |
| Экраны и черновик API | [`README.md` §11](README.md#11-черновые-экраны-web-приложения) |
| Контракт REST API (OpenAPI 3.1) | [`web_lr2/openapi.yaml`](web_lr2/openapi.yaml) |
| ADR-0001 Хранилище, ADR-0002 Слои и два UI, ADR-0003 Сборка и развёртывание, ADR-0004 Пароли | [`README.md` §12](README.md#12-архитектурные-решения-adr) |
| Связь сценариев с требованиями, сущностями, экранами и API | [`README.md` §13](README.md#13-связь-сценариев-с-требованиями-сущностями-и-экранами) |
| Разбор проекта: компоненты, ограничения, пробелы G-1…G-12, решения по вопросам Q-1…Q-5, противоречия | [`AI_REVIEW.md`](AI_REVIEW.md#weblab1--проектные-материалы) |

## 5. Структура репозитория

```
ATAP/
├─ ATAP.sln
├─ Makefile                     # build / test-* / db-up / db-seed / demo-setup / run / run-blazor / publish
├─ README.md                    # описание продукта
├─ AGENTS.md                    # этот файл
├─ AI_REVIEW.md                 # журнал работы с ИИ
├─ TESTING.md                   # тестовая инфраструктура: покрытие, Allure, офлайн-режим, процессы
├─ LR6_LR7.md                   # логирование, конфигурация, сборка компонентов; GUI
├─ coverlet.runsettings         # настройки сборщика покрытия
├─ .config/dotnet-tools.json    # локальный инструмент dotnet-reportgenerator-globaltool
├─ ATAP.DataAccess/             # репозитории Postgres*/Mongo*, RedisCacheRepository, SqlQueryFactory
├─ ATAP.BusinessLogic/          # сервисы (Auth, Map, Analytics, Admin, SavedReport), DTO, интерфейсы репозиториев
├─ ATAP.BlazorUI/               # веб-приложение, MVVM
│   ├─ Pages/                   #   Index, Login, Register, Logout, Map, Analytics, Reports, Admin, Airports, Flights
│   ├─ ViewModels/
│   ├─ Services/AppStateService.cs
│   ├─ wwwroot/js/map.js        #   карта Leaflet
│   └─ Program.cs               #   DI, выбор хранилища по StorageBackend
├─ ATAP.ConsoleUI/              # технологический UI: меню по ролям, Serilog
├─ ATAP.BusinessLogic.Tests/    # unit-тесты бизнес-логики (моки, Builders, Mothers, случайный порядок)
├─ ATAP.DataAccess.Tests/       # unit-тесты SqlQueryFactory без БД
├─ ATAP.IntegrationTests/       # 36 интеграционных тестов на стенде (PostgreSQL + Redis), один файл — один тест
├─ ATAP.E2ETests/               # E2E-тест демонстрационного сценария по HTTP
├─ ATAP.Api/                    # REST API (ASP.NET Core 8) по web_lr2/openapi.yaml: срез WebLab#3, Swagger UI
├─ ci/                          # контейнер запуска тестов (Dockerfile.tests, run-tests.sh), Jenkins (jenkins/), скрипты этапов
├─ Jenkinsfile                  # конвейер unit -> integration -> e2e, отчёт Allure с историей
├─ docker-compose.test.yml      # тестовый стенд: postgres-test, redis-test, db-e2e, atap-api
├─ scripts/                     # test-stand.sh (стенд), e2e-curl.sh, capture-traffic.sh
├─ docs/traffic/                # журнал трафика E2E-сценария (mitmproxy)
├─ db/
│   ├─ create_tables.sql        #   DDL: stg/dds/mart, CHECK, роли, процедуры, сиды
│   ├─ add_saved_reports.sql
│   ├─ test/                    #   образ тестовой БД: Dockerfile, детерминированные тестовые данные
│   ├─ seed_demo_flights.sql    #   100 демо-рейсов для карты
│   ├─ docker-compose.yml       #   PostgreSQL + PostGIS, Redis
│   └─ migrate_to_mongo.py
├─ web_lr1/diagrams/            # изображения к README: диаграммы (*.png, исходники *.puml), c4/, screens/
└─ web_lr2/openapi.yaml         # контракт REST API
```

## 6. Команды запуска и проверки

Требования: .NET SDK 8, Docker, GNU Make (необязательно). Shell — PowerShell или bash.

### Проверенные команды

Проверено 15.10.2026: тесты (unit и интеграционные), `make demo-setup`, `make run-blazor`.
Сборка, покрытие, Allure, ConsoleUI и публикация — по прогонам 14.10.2026.

| Задача | Команда | Статус |
|---|---|---|
| Восстановить пакеты | `dotnet restore ATAP.sln` | ✅ |
| Собрать решение | `make build` или `dotnet build ATAP.sln -c Debug` | ✅ 0 ошибок |
| Поднять БД и кэш | `make db-up` или `docker compose -f db/docker-compose.yml up -d` | ✅ PostgreSQL :5433, Redis :6379 |
| Поднять БД и залить демо-рейсы | `make demo-setup` | ✅ 100 рейсов на карте |
| Unit-тесты бизнес-логики | `make test-unit` | ✅ 51/51, без БД и сети |
| Unit-тесты SqlQueryFactory | `make test-dataaccess` | ✅ 12/12, без БД и сети |
| Все unit-тесты | `make test-offline` | ✅ 63/63 |
| Интеграционные тесты | `make test-integration` (отдельный стенд в Docker, удаляется после прогона) | ✅ 36/36 |
| E2E-тест демонстрационного сценария | `make test-e2e` (стенд: PostgreSQL, Redis, ATAP.Api) | ✅ 1/1 |
| unit → integration → e2e на одном стенде | `make test` (= `make test-stand-all`) | ✅ |
| REST API локально | `make run-api` → http://localhost:5080, Swagger UI `/swagger` (нужны БД и Redis) | ✅ |
| Jenkins в Docker (CI) | `bash ci/jenkins.sh up` → http://localhost:8085; сборка: `bash ci/jenkins.sh build [integration\|e2e]` | ✅ |
| Имитация запросов E2E и захват трафика | `bash scripts/capture-traffic.sh` → `docs/traffic/` | ✅ 15 запросов |
| Покрытие строк и ветвлений (unit) | `make test-coverage` → `TestResults/CoverageReport/index.html` | ✅ цифры в `TESTING.md` |
| Покрытие unit + интеграционных | `make test-coverage-all` → `TestResults/CoverageAll/` | ✅ строки 75,1% |
| Покрытие вариантов использования тестами | `bash scripts/check-usecase-coverage.sh` (таблица — `docs/use-case-coverage.md`) | ✅ 74 ссылки на тесты, все найдены |
| Allure-отчёт | `make test-allure` → `allure-report/` (нужен `npm i -g allure-commandline`) | ✅ |
| Веб-приложение | `make run-blazor` → http://localhost:5075 (нужны БД и Redis) | ✅ |
| ConsoleUI | `make run` (нужна БД) | ✅ |
| Публикация ConsoleUI | `make publish` → `publish/console` | ✅ |
| Линтер OpenAPI и примеры по схемам | `make api-lint` (Redocly CLI 1.25.0, конфигурация `web_lr2/redocly.yaml`; нужен Node.js 18+) | ✅ 0 ошибок, 0 замечаний |
| Mock-сервер и сценарии по контракту | `make api-scenario` (Prism 5.16.0 поднимается и останавливается скриптом, порт 4010) | ✅ 30 шагов, 2 сценария |
| Вся проверка контракта | `make api-check` | ✅ |

Строка подключения по умолчанию: `Host=localhost;Port=5433;Database=atap_db;Username=postgres;Password=mysecretpassword`.
`StorageBackend` в `ATAP.ConsoleUI/appsettings.json` = `mongodb`; для работы с PostgreSQL запускать
`dotnet run --project ATAP.ConsoleUI -- --StorageBackend=postgres`.

### Планируемые проверки (для ещё не реализованных частей)

| Часть | Планируемая проверка |
|---|---|
| REST API (WebLab#3) | реализован срез (10 из 23 операций), E2E-тест проходит; остальные операции и контрактные тесты по [`web_lr2/openapi.yaml`](web_lr2/openapi.yaml): каждый `operationId` — успешный ответ и описанные ошибки; сценарии `web_lr2/mock-scenario.mjs` можно направить на реальный сервер через `MOCK_URL` |
| Dockerfile веб-приложения (ADR-0003) | `docker compose up` поднимает приложение, БД и Redis; `GET /` → 200 |
| Airflow DAG (FR-17) | ошибка OpenSky API → запись в лог, следующий цикл повторяет загрузку |
| Хэш паролей PBKDF2 (ADR-0004) | unit-тест: одинаковый пароль у двух пользователей → разные хэши; в `dds.users` нет паролей в открытом виде |
| Работа без Redis (NFR-R2) | остановить Redis → сценарии 3–4 проходят без ошибок 5xx |
| Нагрузка (NFR-P1) | k6: `GET /api/v1/flights/current` — 95 % запросов ≤ 1000 мс при 50 запросах в секунду |
| Радиус зоны в метрах | тест витрины: судно в 9 км от аэропорта при `analysis_radius = 10000` попадает в зону, в 11 км — нет |
| Период витрины ≤ 31 дня | запрос с периодом больше 31 дня отклоняется с сообщением, запрос к БД не выполняется |

## 7. Правила согласованности (обязательны для ИИ)

- **Роли:** ровно три — `guest` (только аноним, в БД не пишется), `analyst`, `admin`.
  Регистрация → `analyst`. Значение `"client"` в `UserDb.Role` — устаревшее, не использовать.
- **Сущности:** имена и поля — по [`README.md` §7](README.md#7-er-диаграмма-сущностей) и
  `db/create_tables.sql`. Новые поля добавлять сначала в ER и DDL, затем в *Db-модели и DTO.
- **Бизнес-правила:** `date_from ≤ date_to`; период витрины не больше 31 дня, обе границы (`dateFrom`, `dateTo`) включаются; судно «в зоне» при
  расстоянии < `analysis_radius` (метры) и `altitude ≤ 610`; пароли только хэшированные, длина пароля при создании и смене — 8–128 символов;
  удалять пользователя с ролью `admin` нельзя.
- **Хранилище:** PostgreSQL + PostGIS; MongoDB-репозитории — альтернатива, не основной вариант.
- **Кэш (Redis):** опционален; система обязана работать без него (NFR-R2).
- **Слои:** View → ViewModel → сервис → репозиторий; ViewModel не обращается к репозиторию напрямую.
- **Связи сценариев:** новый сценарий связывать с требованием, сущностью и экраном
  ([`README.md` §11](README.md#11-черновые-экраны-web-приложения)) и добавлять в
  [`README.md` §13](README.md#13-связь-сценариев-с-требованиями-сущностями-и-экранами).
- **Диаграммы:** новые и изменяемые — с редактируемым исходником `.puml` в `web_lr1/diagrams/`.
  Исключения: BPMN (только PNG) и схема БД (исходник — DDL).
- **Открытые вопросы:** если в требованиях нет ответа — формулировать вопрос с вариантами,
  решение принимает пользователь. Принятые решения Q-1…Q-5 — в
  [`AI_REVIEW.md`](AI_REVIEW.md#открытые-вопросы).

## 8. Передача

`README.md`, `web_lr1/diagrams/`, `AI_REVIEW.md` и этот файл — основа для проектирования REST API
(WebLab#2, по связям сценариев и экранам) и реализации GUI (WebLab#7, MVVM — ADR-0002).
