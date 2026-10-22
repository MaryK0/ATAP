DOTNET   := dotnet
SLN      := ATAP.sln
CONSOLE  := ATAP.ConsoleUI/ATAP.ConsoleUI.csproj
BLAZOR   := ATAP.BlazorUI/ATAP.BlazorUI.csproj
BL_TESTS := ATAP.BusinessLogic.Tests/ATAP.BusinessLogic.Tests.csproj
DA_TESTS := ATAP.DataAccess.Tests/ATAP.DataAccess.Tests.csproj
IT_TESTS := ATAP.IntegrationTests/ATAP.IntegrationTests.csproj
E2E_TESTS := ATAP.E2ETests/ATAP.E2ETests.csproj
API      := ATAP.Api/ATAP.Api.csproj
STAND    := scripts/test-stand.sh
CONFIG   ?= Debug
PUBLISH  := publish/console

.PHONY: all build release restore clean test test-unit test-dataaccess test-offline test-integration \
        test-e2e test-stand-all run-api test-coverage test-allure run run-blazor publish help db-up db-down db-seed demo-setup \
        api-lint api-scenario api-check

API_SPEC     := web_lr2/openapi.yaml
API_LINT_CFG := web_lr2/redocly.yaml
REDOCLY      := npx -y @redocly/cli@1.25.0

COVERLET_SETTINGS := coverlet.runsettings
TESTRESULTS_DIR    := TestResults
COVERAGE_DIR       := $(TESTRESULTS_DIR)/CoverageReport
ALLURE_RESULTS_DIR := allure-results
ALLURE_REPORT_DIR  := allure-report

all: build

## Сборка всего решения (Debug по умолчанию, CONFIG=Release для релиза)
build: restore
	$(DOTNET) build $(SLN) --configuration $(CONFIG) --no-restore

release:
	$(MAKE) build CONFIG=Release

## Восстановление пакетов
restore:
	$(DOTNET) restore $(SLN)

## Очистка артефактов сборки
clean:
	$(DOTNET) clean $(SLN)

## Только unit-тесты бизнес-логики (ATAP.BusinessLogic.Tests) — mock/fake-based, работают offline
test-unit: build
	$(DOTNET) test $(BL_TESTS) \
		--configuration $(CONFIG) --no-build --logger "console;verbosity=normal"

## Только unit-тесты слоя доступа к данным (ATAP.DataAccess.Tests: SqlQueryFactory, без БД)
test-dataaccess: build
	$(DOTNET) test $(DA_TESTS) \
		--configuration $(CONFIG) --no-build --logger "console;verbosity=normal"

## Все unit-тесты (offline: без БД, без сети — см. TESTING.md, требование 11)
test-offline: test-unit test-dataaccess

## Интеграционные тесты на отдельном стенде в Docker (PostgreSQL + Redis, стенд удаляется всегда; нужен запущенный Docker)
test-integration: build
	$(STAND) with $(DOTNET) test $(IT_TESTS) \
		--configuration $(CONFIG) --no-build --logger "console;verbosity=normal"

## E2E-тест демонстрационного сценария по HTTP на отдельном стенде (PostgreSQL + Redis + ATAP.Api)
test-e2e: build
	$(STAND) with $(DOTNET) test $(E2E_TESTS) \
		--configuration $(CONFIG) --no-build --logger "console;verbosity=normal"

## Интеграционные и E2E-тесты на одном стенде после unit-тестов: unit -> integration -> e2e (после падения этапа следующие не запускаются)
test-stand-all: build test-offline
	$(STAND) with bash -c '$(DOTNET) test $(IT_TESTS) --configuration $(CONFIG) --no-build --logger "console;verbosity=normal" && $(DOTNET) test $(E2E_TESTS) --configuration $(CONFIG) --no-build --logger "console;verbosity=normal"'

## Все тесты в порядке unit -> integration -> e2e
test: test-stand-all

## Покрытие (строки + ветвления) unit-тестов: coverlet -> cobertura -> ReportGenerator (HTML+текст)
# Очистка/вывод файла идут через powershell.exe намеренно: это единственная программа,
# гарантированно присутствующая в PATH на любой Windows-машине вне зависимости от того,
# через cmd.exe или bash сам `make` выполняет строки рецепта (это и ломалось без powershell).
test-coverage: build
	powershell -NoProfile -Command "try { Remove-Item -Recurse -Force $(TESTRESULTS_DIR) -ErrorAction Stop } catch {}; exit 0"
	$(DOTNET) test $(BL_TESTS) \
		--collect:"XPlat Code Coverage" --settings $(COVERLET_SETTINGS) --results-directory ./$(TESTRESULTS_DIR)
	$(DOTNET) test $(DA_TESTS) \
		--collect:"XPlat Code Coverage" --settings $(COVERLET_SETTINGS) --results-directory ./$(TESTRESULTS_DIR)
	$(DOTNET) tool run reportgenerator \
		-reports:"$(TESTRESULTS_DIR)/**/coverage.cobertura.xml" \
		-targetdir:"$(COVERAGE_DIR)" -reporttypes:"Html;TextSummary"
	powershell -NoProfile -Command "Get-Content $(COVERAGE_DIR)/Summary.txt"

## Прогон тестов + генерация Allure-отчёта (требуется установленный allure CLI, см. TESTING.md)
test-allure: build
	powershell -NoProfile -Command "try { Remove-Item -Recurse -Force $(ALLURE_RESULTS_DIR),$(ALLURE_REPORT_DIR) -ErrorAction Stop } catch {}; exit 0"
	-$(DOTNET) test $(BL_TESTS) --no-build
	-$(DOTNET) test $(DA_TESTS) --no-build
	powershell -NoProfile -Command "New-Item -ItemType Directory -Force $(ALLURE_RESULTS_DIR) | Out-Null"
	powershell -NoProfile -Command "Copy-Item ATAP.BusinessLogic.Tests/bin/$(CONFIG)/net8.0/allure-results/*.json $(ALLURE_RESULTS_DIR)/"
	powershell -NoProfile -Command "Copy-Item ATAP.DataAccess.Tests/bin/$(CONFIG)/net8.0/allure-results/*.json $(ALLURE_RESULTS_DIR)/"
	allure generate $(ALLURE_RESULTS_DIR) --clean -o $(ALLURE_REPORT_DIR)
	@echo "Открыть отчёт: allure open $(ALLURE_REPORT_DIR)"

## Поднять PostgreSQL + Redis (нужно для run-blazor/run и test-integration)
db-up:
	docker compose -f db/docker-compose.yml up -d

## Остановить PostgreSQL + Redis
db-down:
	docker compose -f db/docker-compose.yml down

## Залить 100 демо-рейсов в уже поднятую БД (для показа карты — без этого она может быть пустой,
## см. TESTING.md про окно 15 минут у mart.current_flights_map)
db-seed:
	docker exec -i atap_postgres psql -U postgres -d atap_db < db/seed_demo_flights.sql

## Полная подготовка к демонстрации: поднять БД + засеять демо-рейсы.
## Дальше запускать отдельно: make run-blazor (блокирующий процесс, свой терминал)
demo-setup: db-up db-seed
	@echo "Готово. Дальше: make run-blazor, затем http://localhost:5075"

## Запуск ConsoleUI
run:
	$(DOTNET) run --project $(CONSOLE) --configuration $(CONFIG)

## Запуск REST API (http://localhost:5080, Swagger UI — /swagger; нужны БД и Redis)
run-api:
	$(DOTNET) run --project $(API) --configuration $(CONFIG) --launch-profile http

## Запуск BlazorUI
run-blazor:
	$(DOTNET) run --project $(BLAZOR) --configuration $(CONFIG)

## Публикация ConsoleUI в publish/console
publish:
	$(DOTNET) publish $(CONSOLE) \
		--configuration Release \
		--output $(PUBLISH) \
		--no-self-contained

help:
	@echo "Цели:"
	@echo "  build            Сборка решения (CONFIG=Debug|Release)"
	@echo "  release          Сборка в Release"
	@echo "  restore          Восстановление NuGet-пакетов"
	@echo "  clean            Удаление артефактов"
	@echo "  test             Сборка + все тесты (unit + integration)"
	@echo "  test-unit        Unit-тесты бизнес-логики (offline, mock/fake)"
	@echo "  test-dataaccess  Unit-тесты SqlQueryFactory (offline, без БД)"
	@echo "  test-offline     Все unit-тесты (offline, без сети/БД) — test-unit + test-dataaccess"
	@echo "  test-integration Интеграционные тесты (нужна поднятая PostgreSQL)"
	@echo "  test-coverage    Прогон unit-тестов с покрытием (строки+ветвления) -> TestResults/CoverageReport"
	@echo "  test-allure      Прогон unit-тестов + генерация Allure-отчёта -> allure-report"
	@echo "  db-up            Поднять PostgreSQL + Redis (docker compose)"
	@echo "  db-down          Остановить PostgreSQL + Redis"
	@echo "  db-seed          Залить 100 демо-рейсов в БД (для показа карты)"
	@echo "  demo-setup       db-up + db-seed одной командой — подготовка к демонстрации"
	@echo "  run              Запустить ConsoleUI"
	@echo "  run-blazor       Запустить BlazorUI"
	@echo "  publish          Публикация ConsoleUI -> publish/console"
	@echo "  api-lint         Линтер OpenAPI + проверка примеров по схемам (web_lr2/openapi.yaml)"
	@echo "  api-scenario     Многошаговые сценарии на mock-сервере Prism"
	@echo "  api-check        api-lint + api-scenario"

## Контракт REST API (WebLab#2): линтер OpenAPI + проверка примеров запросов и ответов по схемам (нужен Node.js 18+)
api-lint:
	$(REDOCLY) lint --config $(API_LINT_CFG) $(API_SPEC)

## Многошаговые сценарии на mock-сервере Prism по контракту (сам запускает и останавливает Prism, порт 4010)
api-scenario:
	node web_lr2/mock-scenario.mjs

## Вся проверка контракта: линтер, примеры по схемам, mock-сценарии
api-check: api-lint api-scenario
