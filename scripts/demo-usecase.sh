#!/usr/bin/env bash
# Живой показ тестов по вариантам использования с диаграммы (web_lr1/diagrams/use_case.puml).
#
#   bash scripts/demo-usecase.sh list        список вариантов (номера как на диаграмме)
#   bash scripts/demo-usecase.sh 2           запустить тесты одного варианта использования
#   bash scripts/demo-usecase.sh 1 2 6       несколько подряд
#   bash scripts/demo-usecase.sh 16          тесты на обработку исключений (первая лабораторная)
#   bash scripts/demo-usecase.sh all         все варианты по порядку
#
# Для каждого варианта печатается название, затем запускаются его unit-, интеграционные и E2E-тесты,
# выводятся имена пройденных тестов и итог. Интеграционные и E2E-тесты идут на тестовом стенде в Docker:
#   - если стенд не поднят, скрипт сам поднимает его на время запуска (около 20 секунд);
#   - чтобы не ждать при каждом варианте, поднимите стенд один раз:
#         eval "$(bash scripts/test-stand.sh up)"
#     запускайте варианты, в конце:  bash scripts/test-stand.sh down
set -uo pipefail
export DOTNET_CLI_UI_LANGUAGE=en VSLANG=1033   # скрипт разбирает английский вывод dotnet test
cd "$(dirname "${BASH_SOURCE[0]}")/.."

BL=ATAP.BusinessLogic.Tests
DA=ATAP.DataAccess.Tests
UI=ATAP.BlazorUI.Tests
IT=ATAP.IntegrationTests
E2E=ATAP.E2ETests

# ── Описание вариантов: UNIT/DA/INTEG — подстроки имён тестов через «|», E2E=1 — прогнать E2E-тест ──────
define() {
  TITLE="" ; UNIT="" ; DA_F="" ; UI_F="" ; INTEG="" ; E2E_RUN=0 ; FILES="" ; NOTE=""
  case "$1" in
    1) TITLE="Регистрация → ввод почты и пароля, создание логина (гость, FR-01)"
       UNIT="RegisterAsync"; UI_F="RegisterViewModel"; INTEG="AuthService_Register|UserRepository_Create"
       FILES="ATAP.BusinessLogic.Tests/AuthServiceTests.cs, ATAP.BlazorUI.Tests/RegisterViewModelTests.cs (форма: пустые поля), ATAP.IntegrationTests/BusinessLogic/AuthService_RegisterThenLogin_*"
       NOTE="Регистрации нет в срезе API, поэтому E2E её не затрагивает; проверка пустых полей формы — unit-тесты RegisterViewModel." ;;
    2) TITLE="Авторизация → ввод логина и пароля (гость, FR-02)"
       UNIT="LoginAsync|GetCurrentUserAsync"; UI_F="LoginViewModel"; INTEG="AuthService_Login|AuthService_RegisterThenLogin|UserRepository_GetByUsername"; E2E_RUN=1
       FILES="ATAP.BusinessLogic.Tests/AuthServiceTests.cs, ATAP.BlazorUI.Tests/LoginViewModelTests.cs, ATAP.E2ETests/DemoScenario_*.cs (шаги 3–4)" ;;
    3) TITLE="Задать фильтры на карте (аналитик, админ, FR-04..FR-06)"
       UNIT="MapService|FlightFilter"; INTEG="Map"; E2E_RUN=1
       FILES="ATAP.BusinessLogic.Tests/FlightFilterTests.cs, MapServiceTests.cs, ATAP.IntegrationTests/BusinessLogic/MapService_GetCurrentFlights_*, ATAP.E2ETests/DemoScenario_*.cs (шаг 1)"
       NOTE="Фильтры (область, высота, страна) есть в бизнес-логике и API и покрыты тестами; в интерфейсе Blazor их нет (веб-интерфейс работает без API)." ;;
    4) TITLE="Задать фильтры в витрине: период и аэропорт (аналитик, админ, FR-07..FR-09)"
       UNIT="AnalyticsServiceTests.GetCongestion|AnalyticsServiceTests.GetAllCongestion"; DA_F="GetCongestionQuery"
       INTEG="AnalyticsRepository|AnalyticsService_GetCongestion"; E2E_RUN=1
       FILES="ATAP.BusinessLogic.Tests/AnalyticsServiceTests.cs, ATAP.IntegrationTests/DataAccess/AnalyticsRepository_*, ATAP.E2ETests/DemoScenario_*.cs (шаги 5, 7)" ;;
    5) TITLE="Выгрузить CSV-отчёт (FR-10)"
       UNIT="ExportToCsv"; INTEG="ExportToCsv"; E2E_RUN=1
       FILES="ATAP.BusinessLogic.Tests/AnalyticsServiceTests.cs, ATAP.E2ETests/DemoScenario_*.cs (шаг 6)" ;;
    6) TITLE="Выйти из аккаунта (аналитик, админ, FR-03)"
       UNIT="LogoutAsync|GetCurrentUserAsync_WithoutLogin"; INTEG="AuthService_Logout"; E2E_RUN=1
       FILES="ATAP.BusinessLogic.Tests/AuthServiceTests.cs, ATAP.E2ETests/DemoScenario_*.cs (шаг 11: токен отозван)" ;;
    7) TITLE="Открыть таблицу с пользователями (админ, FR-13)"
       UNIT="GetAllUsersAsync"; INTEG="UserRepository_GetAll"
       FILES="ATAP.BusinessLogic.Tests/AdminServiceTests.cs" ;;
    8) TITLE="Редактировать пользователя (админ, FR-15)"
       UNIT="UpdateUserAsync"; INTEG="AdminService_UpdateUser|UserRepository_Update"
       FILES="ATAP.BusinessLogic.Tests/AdminServiceTests.cs, ATAP.IntegrationTests/BusinessLogic/AdminService_UpdateUser_*" ;;
    9) TITLE="Удалить пользователя (админ, FR-16)"
       UNIT="DeleteUserAsync"; INTEG="AdminService_DeleteUser|UserRepository_Delete"
       FILES="ATAP.BusinessLogic.Tests/AdminServiceTests.cs, ATAP.IntegrationTests/BusinessLogic/AdminService_DeleteUser_*, DataAccess/UserRepository_Delete_*"
       NOTE="Правило «нельзя удалить admin и себя» реализовано в AdminService: удалять пользователей может только администратор, поэтому удаление себя — тот же случай, что удаление администратора." ;;
    10) TITLE="Добавить пользователя (админ, FR-14)"
       UNIT="RegisterAsync_WithNewUser|UpdateUserAsync_WithExistingUser"; INTEG="AdminService_UpdateUser"
       FILES="ATAP.IntegrationTests/BusinessLogic/AdminService_UpdateUser_ChangesRoleAndPassword_IntegrationTest.cs"
       NOTE="Отдельного метода «добавить пользователя» нет: интерфейс регистрирует пользователя и затем меняет роль — проверены оба шага." ;;
    11|12|13) case "$1" in 11) TITLE="Авторизация в Airflow (админ)";; 12) TITLE="Управлять параметрами Airflow (админ)";; 13) TITLE="Перезапустить DAG (админ)";; esac
       NOTE="Вне границ ATAP: это родной интерфейс Airflow (README §3, AGENTS §3); DAG спроектированы, в этом репозитории их нет. Тестов нет." ;;
    14) TITLE="[нет на диаграмме] Сохранить отчёт (аналитик, FR-11)"
       UNIT="SaveAsync"; INTEG="SavedReportService_Save|SavedReportRepository_Create"; E2E_RUN=1
       FILES="ATAP.BusinessLogic.Tests/SavedReportServiceTests.cs, ATAP.E2ETests/DemoScenario_*.cs (шаг 8)" ;;
    15) TITLE="[нет на диаграмме] Мои отчёты: список и удаление (аналитик, FR-12)"
       UNIT="GetByUserAsync|DeleteAsync"; INTEG="SavedReportRepository_GetByUser|SavedReportRepository_Delete"; E2E_RUN=1
       FILES="ATAP.BusinessLogic.Tests/SavedReportServiceTests.cs, ATAP.E2ETests/DemoScenario_*.cs (шаги 9–10)" ;;
    16) TITLE="[первая лабораторная, требование 2] Тесты на обработку исключений"
       UNIT="ShouldThrow"; INTEG="Throws|ViolatesCheck|InvalidInput"
       FILES="ATAP.BusinessLogic.Tests/AnalyticsServiceTests.cs, SavedReportServiceTests.cs, MapServiceTests.cs, AdminServiceTests.cs, ATAP.IntegrationTests/*_Throws_*, *_ViolatesCheck_*, *_InvalidInput_*"
       NOTE="Исключения: ArgumentException (пустое имя отчёта, период и диапазон высот наоборот), ArgumentNullException (CSV от пустого отчёта, пустой фильтр), AdminDeletionForbiddenException (удаление администратора); в интеграционных — PostgresException: уникальность логина (23505) и CHECK периода отчёта (23514)." ;;
    *) return 1 ;;
  esac
}

# «a|b» → «FullyQualifiedName~a|FullyQualifiedName~b»
make_filter() { printf '%s' "$1" | sed 's/[^|][^|]*/FullyQualifiedName~&/g'; }

run_project() {  # проект, подстроки через |, метка
  local project="$1" subs="$2" label="$3" out code
  [ -n "$subs" ] || return 0
  echo "  ── $label ──"
  out="$(dotnet test "$project" --no-build --filter "$(make_filter "$subs")" --logger "console;verbosity=normal" 2>&1)"; code=$?
  printf '%s\n' "$out" | grep -E '^\s+(Passed|Failed|Skipped) ' | sed 's/^ *//; s/^/    /' | sed -E 's/ATAP\.[A-Za-z]+\.//' | cut -c1-150
  printf '%s\n' "$out" | grep -E 'Total tests:|^\s+Passed:|^\s+Failed:|No test matches' | sed 's/^ */    /'
  return $code
}

run_usecase() {
  define "$1" || { echo "Нет варианта использования № $1 (список: list)"; return 2; }
  echo; echo "════════════════════════════════════════════════════════════════════════"
  echo " Вариант использования № $1: $TITLE"
  echo "════════════════════════════════════════════════════════════════════════"
  [ -z "$FILES" ] || echo " Код тестов: $FILES"
  [ -z "$NOTE" ] || echo " Важно: $NOTE"
  local fail=0
  run_project "$BL" "$UNIT" "unit: бизнес-логика" || fail=1
  run_project "$DA" "$DA_F" "unit: SQL-запросы" || fail=1
  run_project "$UI" "$UI_F" "unit: формы интерфейса" || fail=1
  if [ -n "$INTEG" ]; then run_project "$IT" "$INTEG" "интеграционные: PostgreSQL и Redis" || fail=1; fi
  if [ "$E2E_RUN" = "1" ]; then run_project "$E2E" "DemoScenario" "E2E: сценарий по HTTP (11 шагов)" || fail=1; fi
  if [ -z "$UNIT$DA_F$UI_F$INTEG" ] && [ "$E2E_RUN" = "0" ]; then
    echo " Итог: вариант вне границ системы — тестов нет (причина выше)"
    return 0
  fi
  [ "$fail" -eq 0 ] && echo " Итог: все тесты варианта пройдены" || echo " Итог: ЕСТЬ ПАДЕНИЯ"
  return $fail
}

list_usecases() {
  echo "Варианты использования (номера как в docs/use-case-coverage.md):"
  for n in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16; do define "$n"; printf '  %2d. %s\n' "$n" "$TITLE"; done
}

needs_stand() {
  for n in "$@"; do
    define "$n" 2>/dev/null || continue
    [ -n "$INTEG" ] || [ "$E2E_RUN" = "1" ] && return 0
  done
  return 1
}

case "${1:-}" in
  ""|-h|--help) sed -n '2,15p' "$0"; exit 2 ;;
  list) list_usecases; exit 0 ;;
  all) set -- 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 ;;
esac

# Интеграционные и E2E-тесты требуют стенд: если он не поднят — поднимаем на время показа
if [ -z "${ATAP_TEST_PG:-}" ] && needs_stand "$@"; then
  echo "Тестовый стенд не поднят — поднимаю на время показа и удалю после (для повторных запусков: eval \"\$(bash scripts/test-stand.sh up)\")"
  exec bash scripts/test-stand.sh with bash "$0" "$@"
fi

failed=0
for n in "$@"; do run_usecase "$n" || failed=1; done
echo
[ "$failed" -eq 0 ] && echo "Все показанные варианты использования: падений нет." || echo "Есть непройденные тесты."
exit $failed
