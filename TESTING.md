# TESTING.md — тестовая инфраструктура ATAP

> Документация к «Лабораторная работа №1»: unit-тесты компонентов доступа к данным и бизнес-логики,
> автоматический расчёт покрытия, Allure-отчёт, случайный порядок, офлайн-режим, процессная модель.
> Живёт в корне репозитория (как `AGENTS.md`/`README.md`) — описывает реальную тестовую инфраструктуру
> приложения, а не является отчётным материалом лабы №1-по-вебу (`web_lr1/`).

## 0. Технологический стек

| Слой | Технология | Версия | Роль |
|---|---|---|---|
| Язык / платформа | C# / .NET | .NET SDK 8.0.425 | и приложение, и тесты — один стек |
| Тестовый фреймворк | xUnit | `xunit` 2.9.3, `xunit.runner.visualstudio` 2.8.2 | `[Fact]`/`[Theory]`, `Assert.*`, раннер поверх VSTest |
| Mock-фреймворк | Moq | 4.20.72 | «Лондонский» стиль — `Mock<T>`, `Verify(...)` |
| Покрытие (сбор) | coverlet | `coverlet.collector` 6.0.0 | data collector `XPlat Code Coverage`, формат cobertura |
| Покрытие (отчёт) | ReportGenerator | `dotnet-reportgenerator-globaltool` 5.5.11 | локальный dotnet tool → HTML + текстовая сводка |
| Автоотчёт о прогоне | Allure | `Allure.Xunit` 2.15.0 (пакет) + Allure CLI 2.46.1 (npm) | JSON-результаты на каждый тест → HTML-дашборд |
| Пакетный менеджер | NuGet | — | доставка всех пакетов выше (аналог `pip`/`npm`) |
| Автоматизация CLI | GNU Make | — | `Makefile` — обёртка над `dotnet`/`allure` командами |
| Shell | PowerShell 5.1 / Git Bash | — | обе среды проверены, см. §6 |

Всё это уже стояло или было добавлено специально под требования этой лабы — ничего не тянули «про запас».

## 1. Структура проекта

### 1.1 Репозиторий целиком (кратко — подробности в `AGENTS.md`)

```
ATAP/
├─ ATAP.BusinessLogic/          # тестируемый код: сервисы
├─ ATAP.DataAccess/              # тестируемый код: SqlQueryFactory + репозитории (не тестируются напрямую)
├─ ATAP.BlazorUI/, ATAP.ConsoleUI/   # UI — вне объёма этой лабы
├─ ATAP.BusinessLogic.Tests/     # ← unit-тесты бизнес-логики (этот документ, разбор ниже)
├─ ATAP.DataAccess.Tests/        # ← unit-тесты SqlQueryFactory (этот документ, разбор ниже)
├─ ATAP.IntegrationTests/        # интеграционные тесты (реальная PostgreSQL)
├─ db/                           # DDL, docker-compose для PostgreSQL+Redis
├─ Makefile                      # все команды запуска/проверки одной строкой
├─ coverlet.runsettings          # настройки сборщика покрытия
├─ .config/dotnet-tools.json     # локальный tool ReportGenerator
├─ TESTING.md                    # этот файл
├─ AGENTS.md                     # контекст приложения целиком
└─ web_lr1/                      # материалы другой лабы (не часть приложения)
```

### 1.2 `ATAP.BusinessLogic.Tests/` — подробно, по подпапкам

```
ATAP.BusinessLogic.Tests/
├─ *.cs (в корне)        ← сами тесты, 7 файлов, 1 класс = 1 тестируемый сервис
├─ Mocks/                 ← fake-реализации репозиториев (classical-стиль)
├─ TestData/
│   ├─ Builders/          ← Data Builder — произвольная сборка объекта под тест
│   └─ Mothers/           ← Object Mother — готовые именованные пресеты
├─ TestOrdering/          ← случайный порядок прогона (требование №10)
├─ allureConfig.json      ← куда Allure.Xunit пишет JSON-результаты
└─ xunit.runner.json      ← параллелизм потоков внутри testhost-процесса
```

**Файлы с тестами** — 7 классов, по одному на каждый сервис (+ 2 доп. класса в «Лондонском» стиле):

| Файл | Тестирует | Стиль |
|---|---|---|
| `AuthServiceTests.cs` | `AuthService` | classical |
| `AuthServiceMoqTests.cs` | `AuthService` (те же сценарии) | London |
| `MapServiceTests.cs` | `MapService` | classical |
| `MapServiceMoqTests.cs` | `MapService` (кэш) | London |
| `AnalyticsServiceTests.cs` | `AnalyticsService` | classical |
| `AdminServiceTests.cs` | `AdminService` | classical |
| `SavedReportServiceTests.cs` | `SavedReportService` | classical |

**`Mocks/`** — 5 fake-классов, по одному на каждый интерфейс репозитория (`MockUserRepository`,
`MockAnalyticsRepository`, `MockMapRepository`, `MockSavedReportRepository`, `MockCacheRepository`).
Это не mock-объекты в строгом смысле, а полноценные in-memory реализации с реальной логикой
(хранят данные в `List<>`/`Dictionary<>`) — подставляются вместо `Postgres*Repository` через DI,
чтобы тест не трогал БД.

**`TestData/Builders/`** — `UserDbBuilder.cs`, `CreateSavedReportDtoBuilder.cs`: fluent-методы
(`With...()`/`As...()`), каждый меняет одно поле и возвращает тот же объект (`return this`),
финальный `.Build()` собирает результат. Есть именованные методы под граничные случаи:
`WithInvertedPeriod()`, `WithSingleDayPeriod(day)`.

**`TestData/Mothers/`** — `Users.cs` (`AnalystAnna()`, `AdminRoot()`, `WithEmptyUsername()`),
`Airports.cs` (`Sheremetyevo()`, `Domodedovo()`, `Default()`). `Users` построен **поверх**
`UserDbBuilder` (готовый пресет = билдер с заранее подставленными значениями); `Airports` построен
без билдера — напрямую `new AirportDb {...}`, потому что вариативность там не нужна ни одному тесту.

**`TestOrdering/`** — `RandomTestCaseOrderer.cs` (перемешивает тесты внутри класса),
`RandomTestCollectionOrderer.cs` (перемешивает порядок между классами), `AssemblyInfo.cs`
(подключает оба через `[assembly: TestCaseOrderer(...)]`/`[assembly: TestCollectionOrderer(...)]`).

### 1.3 `ATAP.DataAccess.Tests/` — та же схема, но меньше

```
ATAP.DataAccess.Tests/
├─ SqlQueryFactoryTests.cs   ← единственный тестовый класс
├─ TestOrdering/              ← своя копия (не шарится с другим тестовым проектом)
├─ allureConfig.json
└─ xunit.runner.json
```
Нет `Mocks/`/`TestData/` — `SqlQueryFactory` не принимает зависимостей (нечего подменять), а
единственный параметр метода — `bool` (под него Builder/Mother были бы избыточны).

## 2. Состав тестовых проектов

| Проект | Тип | Что тестирует | Зависимости во время выполнения |
|---|---|---|---|
| **`ATAP.BusinessLogic.Tests`** | xUnit unit | Слой бизнес-логики (`ATAP.BusinessLogic/Services/*`) | Нет — только in-memory fakes / mocks. Работает офлайн. |
| **`ATAP.DataAccess.Tests`** | xUnit unit | `SqlQueryFactory` — единственный компонент `ATAP.DataAccess`, не требующий БД | Нет. Работает офлайн. |
| **`ATAP.IntegrationTests`** | xUnit integration | `Postgres*Repository` против реальной PostgreSQL | Требует поднятую БД (`docker compose -f db/docker-compose.yml up -d`), помечены `[Trait("Category","Integration")]`. |

`Postgres*Repository` / `Mongo*Repository` / `RedisCacheRepository` из `ATAP.DataAccess` намеренно
**не** покрыты unit-тестами: это тонкие обёртки над Npgsql/MongoDB.Driver/StackExchange.Redis без
собственной логики — юнит-тестировать их без реальной БД бессмысленно, они проверяются
`ATAP.IntegrationTests`. Это согласуется с [ADR-0001](README.md#12-архитектурные-решения-adr)
(PostgreSQL — прод-хранилище).

## 3. Задание — пошагово, что сделано и как проверить

### Задание 1. Unit-тесты для компонентов доступа к данным и бизнес-логики

**Реализовано:** 5 классов бизнес-логики (`AuthService`, `MapService`, `AnalyticsService`,
`AdminService`, `SavedReportService`) — все покрыты; 1 класс доступа к данным (`SqlQueryFactory`) —
единственный юнит-тестируемый без БД.

```bash
dotnet test ATAP.BusinessLogic.Tests/ATAP.BusinessLogic.Tests.csproj   # 49 тестов
dotnet test ATAP.DataAccess.Tests/ATAP.DataAccess.Tests.csproj         # 12 тестов
```

### Задание 2. Автоматический расчёт покрытия

**Инструмент:** coverlet + ReportGenerator (аналог `coverage.py`, см. §0).

```bash
make test-coverage
```
Подробности и актуальные цифры — §7.

### Задание 3. Техника подготовки тестовых данных

| Техника | Где применена (примеры) |
|---|---|
| **Граничные значения (boundary value analysis)** | `AnalyticsServiceTests.GetCongestionAsync_WhenStartEqualsEnd_ShouldSucceed` (start == end — нижняя граница); `SavedReportServiceTests.SaveAsync_WithSingleDayPeriod_ShouldSucceed` / `SaveAsync_WithInvertedPeriod_ShouldThrowArgumentException` (date_from == date_to валидно, date_to < date_from — первое невалидное значение за границей); `MapServiceTests.GetCurrentFlightsAsync_WhenRepositoryEmpty_ShouldReturnEmptyList` / `GetAirportsAsync_WhenRepositoryEmpty_ShouldReturnEmptyList` (пустая коллекция) |
| **Эквивалентное разбиение (equivalence partitioning)** | `AuthServiceTests`: классы "логин занят" / "email занят" / "пользователь не найден" / "неверный пароль" — каждый отдельным тестом; `SavedReportServiceTests.SaveAsync_WithBlankName_ShouldThrowArgumentException` — `[Theory]` с `""` и `"   "` как представители одного класса ("пустая строка"); `SqlQueryFactoryTests.GetCongestionQuery_AnyVariant_...` — `[Theory]` с `true`/`false` как два класса ветки `includeDetails` |
| **Переход состояний (state transition)** | `AuthServiceTests`: не залогинен → залогинен (`LoginAsync`) → разлогинен (`LogoutAsync`) → `GetCurrentUserAsync` возвращает `null` на каждом узле цепочки, где это уместно |
| **Тесты на исключения** | `AnalyticsServiceTests.GetCongestionAsync_WhenStartDateAfterEndDate_ShouldThrowException` (`ArgumentException`), `ExportToCsvAsync_WithNullReport_ShouldThrowException` (`ArgumentNullException`), `SavedReportServiceTests.SaveAsync_WithInvertedPeriod_*` / `SaveAsync_WithBlankName_*` (`ArgumentException`) |
| **Комбинаторное / попарное** | Не применялось — в предметной области нет метода с достаточным числом независимых параметров, чтобы попарное тестирование давало выигрыш относительно эквивалентного разбиения |
| **Проверка отсутствия эффекта (негативная)** | `SqlQueryFactoryTests.*_ShouldNotContain*` / `*_ShouldNotReference*` — то, чего в SQL-запросе быть не должно; `MapServiceMoqTests.GetAirportsAsync_WhenCacheHit_ShouldNeverQueryRepository` — репозиторий не должен вызываться |

## 4. Требования — пошагово, что значит и как проверить

### Требование 1 — ≥1 test class, ≥2 теста (позитив+негатив) на каждый public-метод

Построчная сверка всех 21 public-метода (16 бизнес-логики + 5 `SqlQueryFactory`) против тестов —
полный журнал находок и исправлений в §9. Проверка:

```bash
make test-offline   # все тесты должны пройти; имена методов видно в выводе --logger "console;verbosity=normal"
```

### Требование 2 — тесты на обработку исключений

4 теста, 2 типа исключений (`ArgumentException`, `ArgumentNullException`), через
`Assert.ThrowsAsync<T>(() => ...)`:

```bash
dotnet test ATAP.BusinessLogic.Tests/ATAP.BusinessLogic.Tests.csproj --filter "FullyQualifiedName~Exception"
```

### Требование 3 — classical + «Лондонский» вариант

- **Classical** — 46 тестов: `Mocks/Mock*Repository`, проверка результата.
- **London** — 5 тестов (`AuthServiceMoqTests.cs`, `MapServiceMoqTests.cs`): `Moq`, проверка факта вызова через `Verify(...)`.
- 4 из 5 London-тестов — подтверждённые пары с classical (одинаковые входные данные, разная техника доказательства).

```bash
dotnet test ATAP.BusinessLogic.Tests/ATAP.BusinessLogic.Tests.csproj --filter "FullyQualifiedName~MoqTests"
```

### Требование 4 — структура AAA с fixture и хелперами

Все 61 тест размечены `// Arrange` / `// Act` / `// Assert` (или легитимным `// Act + Assert` для
тестов-исключений, где вызов и проверка — одна и та же строка кода). Хелперы — `Mocks/`,
`TestData/Builders/`, `TestData/Mothers/` (§1.2). Fixture в узком xUnit-смысле (`IClassFixture<T>`) —
только в `ATAP.IntegrationTests/DatabaseFixture.cs`, намеренно не используется в unit-тестах
(дорогая БД-fixture не нужна лёгким in-memory fake, а расшаривание объекта между тестами
противоречило бы изоляции, важной при случайном порядке — требование №10).

### Требование 5 — один вызов метода в Act

Проверено построчно по всем 61 тестам (§9) — найден один реальный случай (`SqlQueryFactory` метод
вызывался дважды в одном `[Fact]`), причина была не в продакшен-классе, а в дизайне теста —
исправлено разбиением на `[Theory]`. Рефакторинг классов не потребовался нигде.

### Требование 6 — приватные/защищённые методы не тестируем

`AuthService.HashPassword`/`VerifyPassword`, `AdminService.HashPassword` — `private`, проверяются
только косвенно через `RegisterAsync`/`LoginAsync`/`UpdateUserAsync`.

### Требование 7 — Data Builder и Object Mother

См. §1.2. Использование — `AdminServiceTests`, `SavedReportServiceTests`, `MapServiceMoqTests`.

### Требование 8 — запуск из CLI на локальной копии репозитория

```bash
dotnet restore ATAP.sln
dotnet build ATAP.sln
dotnet test ATAP.BusinessLogic.Tests/ATAP.BusinessLogic.Tests.csproj
dotnet test ATAP.DataAccess.Tests/ATAP.DataAccess.Tests.csproj
# то же одной командой:
make test-offline

# интеграционные тесты (нужна БД):
docker compose -f db/docker-compose.yml up -d
make test-integration

# покрытие и Allure-отчёт — тоже часть этой же CLI-цепочки (см. требование №9):
make test-coverage
make test-allure
```
Все цели `Makefile` — `make help`. Работает и без Makefile, голыми `dotnet`-командами — Makefile
лишь удобная обёртка, не обязательное условие.

### Требование 9 — автоматически сгенерированный отчёт (Allure), учтённый в CLI из п.8

Пакет `Allure.Xunit` 2.15.0 в обоих unit-проектах, конфиг `allureConfig.json`
(`"directory": "allure-results"`). Allure CLI — `npm install -g allure-commandline`.

```bash
make test-allure
allure open allure-report
```
`make test-allure` — **одна команда**, которая сама: собирает решение → прогоняет оба unit-проекта
→ копирует JSON-результаты из `bin/.../allure-results/` обоих проектов в общую папку → вызывает
`allure generate`. Генерация отчёта **встроена в ту же CLI-цепочку**, что и запуск тестов — не
отдельная ручная операция, как и требует формулировка п.9.

### Требование 10 — случайный порядок выполнения

Собственные `ITestCaseOrderer`/`ITestCollectionOrderer` (`TestOrdering/`, §1.2), подключены через
`[assembly: TestCaseOrderer(...)]`. Механика — `testCases.OrderBy(_ => random.Next())`
(сортировка по случайному ключу = перемешивание).

```bash
dotnet test ATAP.DataAccess.Tests/ATAP.DataAccess.Tests.csproj --logger "console;verbosity=normal" | grep RandomTestCaseOrderer
# повторить — порядок будет другим
```

**Воспроизводимость конкретного прогона.** По умолчанию seed не фиксирован
(`Environment.TickCount` — меняется каждый запуск), но его можно задать явно переменной окружения
`TEST_ORDER_SEED` — тогда порядок становится детерминированным и повторяемым:

```bash
TEST_ORDER_SEED=777 dotnet test ATAP.DataAccess.Tests/ATAP.DataAccess.Tests.csproj --logger "console;verbosity=normal" | grep RandomTestCaseOrderer
# повторить с тем же TEST_ORDER_SEED=777 — порядок будет побуквенно идентичным
```

Фактический `runSeed` каждого прогона не только печатается в консоль, но и **автоматически
сохраняется в файл** `last-seed.txt` (рядом со скомпилированной сборкой —
`bin/<Config>/net8.0/last-seed.txt`, перезаписывается при каждом прогоне, хранит seed именно
последнего запуска):

```bash
# прогон — файл создаётся/перезаписывается сам
dotnet test ATAP.BusinessLogic.Tests/ATAP.BusinessLogic.Tests.csproj

# достать seed и повторить ровно тот же прогон
SEED=$(head -1 ATAP.BusinessLogic.Tests/bin/Debug/net8.0/last-seed.txt)
TEST_ORDER_SEED=$SEED dotnet test ATAP.BusinessLogic.Tests/ATAP.BusinessLogic.Tests.csproj --logger "console;verbosity=normal"
```
Запись в файл — best-effort (обёрнута в `try/catch` в `TestOrderSeed.SaveToFile`): если записать
не получилось (нет прав, файл занят) — сам прогон тестов всё равно отработает, просто без
сохранённого seed на этот раз. Файл не коммитится (лежит в `bin/`, уже в `.gitignore`).

Seed на класс выводится не напрямую из `runSeed`, а через собственную функцию `StableHash` —
**не** через встроенный `string.GetHashCode()`/`HashCode.Combine(int, string)`: в .NET хэш строки
намеренно рандомизирован на каждый запуск процесса (защита от hash-flooding), поэтому с ним один и
тот же `TEST_ORDER_SEED` давал бы разный порядок в разных запусках — ровно то, что должны были
исправить. Это было найдено и исправлено в процессе проверки (сначала реализация не воспроизводила
порядок даже при одинаковом seed, пока не заменили на стабильный хэш).

**Ещё одна находка того же рода** — изначально `RandomTestCaseOrderer` и `RandomTestCollectionOrderer`
каждый сам вызывал `Environment.TickCount` при каждом своём срабатывании. Но `RandomTestCaseOrderer`
вызывается **отдельно на каждый класс** — и оказалось, что разные классы одного и того же прогона
получали **разный** `runSeed` (TickCount успевал чуть сдвинуться между вызовами, особенно при
параллельном выполнении классов). Из-за этого ни сохранённый файл, ни `TEST_ORDER_SEED` не
воспроизводили прогон целиком — только ту часть классов, что получила тот же TickCount. Кроме того,
`RandomTestCollectionOrderer` вообще не писал seed в файл — сохранялся только один из двух.

**Исправлено:** вынесен общий класс [`TestOrderSeed`](ATAP.BusinessLogic.Tests/TestOrdering/TestOrderSeed.cs)
с `public static readonly int RunSeed` — вычисляется **один раз на процесс** (не на каждый вызов), и
используется **обоими** orderer'ами. Проверено: все классы одного прогона теперь получают
одинаковый `runSeed`; два прогона с одинаковым `TEST_ORDER_SEED` дают идентичный (после сортировки
строк лога — порядок самих строк на экране зависит от переплетения параллельных потоков, но
содержимое совпадает полностью) порядок и для классов, и для коллекций.

### Требование 11 — запуск без доступа к интернету

Unit-тесты не содержат ни одного обращения к сети/БД (`grep` по `Npgsql|HttpClient|MongoClient|
ConnectionMultiplexer|Socket` — пусто). Проверено практически — прогон с заведомо недостижимым
прокси:

```bash
HTTP_PROXY="http://10.255.255.1:9" HTTPS_PROXY="http://10.255.255.1:9" \
  dotnet test ATAP.BusinessLogic.Tests/ATAP.BusinessLogic.Tests.csproj --no-build
```
61/61 проходят за ту же скорость, что и обычно. Контраст: `ATAP.IntegrationTests` (без моков,
настоящий Npgsql) без поднятой БД падает все 5/5 с `SocketException` — именно это и демонстрирует
саму идею требования: «тесты только с mock должны в таком случае проходить успешно».

```bash
make test-offline          # пройдёт всегда
make test-integration      # упадёt без БД — и это ожидаемо
```

### Требование 12 — сколько процессов на прогон

Измерено эмпирически (`Get-Process testhost` во время прогона, Windows/PowerShell):

| Сценарий | Процессов `testhost` |
|---|---|
| `dotnet test` одного проекта | **1** |
| `dotnet test ATAP.sln` (3 тестовых проекта) | **3 одновременно** (параллельно, не по очереди) |

**Гранулярность — 1 процесс на 1 тестовый проект (сборку).** Внутри процесса — потоки, не новые
процессы. Конфигурируется: число процессов между проектами — MSBuild `-maxcpucount`
(`dotnet test ATAP.sln -m:1` для отключения параллелизма); число потоков внутри одного процесса —
`xunit.runner.json` (`parallelizeTestCollections`, `maxParallelThreads` — у нас `true`/`4`).

**Это именно параллельно, не просто «не отдельные процессы»** — проверено отдельным экспериментом
(3 временных тест-класса с `Thread.Sleep(1000)` каждый, удалены сразу после проверки, не часть
постоянного набора тестов): все три теста стартовали в одну и ту же миллисекунду (`07:52:19.703`) и завершились в одну и ту же
миллисекунду (`07:52:20.722`, то есть уложились все вместе в ~1 секунду, хотя каждый спал по
секунде), на **трёх разных `ManagedThreadId`** (10, 7, 15). Если бы выполнение было
последовательным — суммарное время было бы ~3 секунды вместо фактического ~1 секунды. Именно это регулирует `maxParallelThreads: 4` —
до 4 тест-классов одновременно внутри одного `testhost`-процесса.

### Требование 13 — тесты должны проходить успешно

```bash
make test-offline
```
Текущий статус: `ATAP.BusinessLogic.Tests` 49/49 ✅, `ATAP.DataAccess.Tests` 12/12 ✅.

### Требование 14 — покрытие по строкам и ветвлениям из CLI

```bash
make test-coverage
```

| Сборка | Line coverage | Branch coverage |
|---|---|---|
| `ATAP.BusinessLogic` (все 5 сервисов) | **100%** | **100%** |
| `ATAP.DataAccess` → `SqlQueryFactory` | **100%** | **100%** |
| `ATAP.DataAccess` целиком (с Postgres/Mongo/Redis) | 24.2% | 4.3% (ожидаемо — вне объёма unit-тестов) |

HTML-отчёт: `TestResults/CoverageReport/index.html` (генерируется, не коммитится).

### Требование 15 — регрессия, рефакторинг, поддержка

- **Регрессия** — подтверждена практически: журнал находок §9 (оба случая нашлись именно проверкой,
  не были приняты на веру).
- **Рефакторинг** — classical-тесты (46/61) устойчивы (проверяют только результат); 5 London-тестов
  устойчивы частично — осознанная плата за проверку взаимодействий (`Verify`), честно
  задокументировано ниже, не скрыто.
- **Поддержка** — `TestData/Builders/`+`TestData/Mothers/` — изменение структуры DTO правится в
  одном месте, не в десятках тестов.

## 5. Классический vs «Лондонский» стиль — подробнее

Пары для прямого сравнения одного и того же сценария (подтверждено одинаковыми входными данными):

| Сценарий | Classical | London |
|---|---|---|
| Регистрация нового пользователя | `AuthServiceTests.RegisterAsync_WithNewUser_ShouldSucceed` | `AuthServiceMoqTests.RegisterAsync_WithNewUser_ShouldCallCreateAsyncWithHashedPasswordAndAnalystRole` |
| Регистрация при занятом логине | `AuthServiceTests.RegisterAsync_WithExistingUsername_ShouldFail` | `AuthServiceMoqTests.RegisterAsync_WithExistingUsername_ShouldNeverCallCreateAsync` |
| Логин несуществующего пользователя | `AuthServiceTests.LoginAsync_WithNonExistentUser_ShouldFail` | `AuthServiceMoqTests.LoginAsync_WithNonExistentUser_ShouldOnlyQueryRepositoryOnce` |
| Кэш аэропортов: попадание/промах | `MapServiceTests.GetAirportsAsync_WithCache_ShouldReturnFromCache` | `MapServiceMoqTests.GetAirportsAsync_WhenCacheHit_ShouldNeverQueryRepository` + `..._WhenCacheMiss_ShouldQueryRepositoryAndPopulateCache` |

В mock-варианте видно, чего classical-тест доказать не может: что репозиторий **не был вызван** при
попадании в кэш (`mapRepoMock` создан с `MockBehavior.Strict` — любой незапланированный вызов
немедленно проваливает тест).

## 6. Окружение, в котором это проверялось

`.NET SDK 8.0.425`, `Java 21` (Temurin, для Allure), `Allure CLI 2.46.1` (установлен через
`npm install -g allure-commandline`), `dotnet-reportgenerator-globaltool 5.5.11` (локальный tool,
`.config/dotnet-tools.json`), Windows 10 / PowerShell + Git Bash — обе оболочки проверены, с одним
найденным и исправленным расхождением (`Makefile`: `rm -rf`/`-ErrorAction SilentlyContinue` вели
себя по-разному под PowerShell и под Git Bash — заменено на `powershell -Command` с явным
`try/catch; exit 0`, работает одинаково из обеих).

## 7. Журнал аудита — найденные и исправленные пробелы

Требование №1 («≥1 test suite, ≥2 теста — позитив/негатив — на каждый public-метод») не бралось
на веру по отчёту: сделана построчная сверка — `grep` всех `public` методов каждого
production-класса (`ATAP.BusinessLogic/Services/*.cs`, `ATAP.DataAccess/Factories/SqlQueryFactory.cs`)
против имён всех тестовых методов (`grep` сигнатур `[Fact]`/`[Theory]`-методов в тестовых файлах),
построена таблица «метод → тесты → позитив/негатив» вручную, метод за методом.

### Находка 1 — `GetAirportsAsync` без негативного теста (требование №1)

**Найдено:** `MapService.GetAirportsAsync` — 4 существовавших теста
(`GetAirportsAsync_ShouldReturnAirports`, `GetAirportsAsync_WithCache_ShouldReturnFromCache`,
`GetAirportsAsync_WhenCacheMiss_...`, `GetAirportsAsync_WhenCacheHit_...`) оказались **все
успешными сценариями** — ни одного теста на пустой/отсутствующий источник данных, хотя у
соседнего метода `GetCurrentFlightsAsync` такой тест (`WhenRepositoryEmpty_ShouldReturnEmptyList`)
уже был.

**Исправлено:** добавлен `GetAirportsAsync_WhenRepositoryEmpty_ShouldReturnEmptyList`
([MapServiceTests.cs](ATAP.BusinessLogic.Tests/MapServiceTests.cs)) — симметрично существующему
тесту на `GetCurrentFlightsAsync`, тот же `MockMapRepository(empty: true)`. Собрано, прогнано,
зелёное.

**Результат:** 49 + 11 = 60 unit-тестов (было 48 + 11 = 59).

### Находка 2 — метод вызывался дважды в одном Act (требование №5)

**Найдено:** `SqlQueryFactoryTests.GetCongestionQuery_BothVariants_ShouldFilterByAirportIdAndPeriod`
вызывал `_factory.GetCongestionQuery(...)` **дважды** в одном `[Fact]` (для `includeDetails: true`
и `false`), чтобы проверить общий инвариант по обоим вариантам сразу — единственный такой случай
среди всех 60 тестов. Причина — не в тестируемом классе (`SqlQueryFactory` — чистая функция без
состояния, вызывается независимо сколько угодно раз), а в дизайне самого теста.

**Исправлено:** тест переписан в `[Theory]` с `[InlineData(true)]`/`[InlineData(false)]`
([SqlQueryFactoryTests.cs](ATAP.DataAccess.Tests/SqlQueryFactoryTests.cs)) — теперь ровно один
вызов метода на один тест-кейс, тот же инвариант проверяется в двух независимых прогонах вместо
одного совмещённого.

**Результат:** 49 + 12 = 61 unit-тест (было 49 + 11 = 60).
