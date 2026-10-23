#!/usr/bin/env bash
# Демонстрационный сценарий E2E-теста, воспроизведённый вручную через curl (WebLab «Тестирование», п. 5).
#   scripts/e2e-curl.sh <адрес API>          например: scripts/e2e-curl.sh http://127.0.0.1:5080
# Для каждого запроса печатается ожидаемый и полученный код ответа; при расхождении скрипт завершается с ошибкой.
# Те же 15 запросов делает автоматический тест ATAP.E2ETests; здесь они повторены отдельным инструментом,
# чтобы снять трафик (scripts/capture-traffic.sh).
set -euo pipefail

BASE="${1:?укажите адрес API, например http://127.0.0.1:5080}"
USER_NAME="${ATAP_E2E_ANALYST:-analyst1}"
PASSWORD="${ATAP_E2E_ANALYST_PASSWORD:-Analyst#2026}"
PERIOD="airportId=1&dateFrom=2026-06-10&dateTo=2026-06-10"
BODY="$(mktemp)"; HEADERS="$(mktemp)"
trap 'rm -f "$BODY" "$HEADERS"' EXIT

STEP=0
TOKEN=""
# call <ожидаемый код> <метод> <путь> [curl-аргументы…]
call() {
  local expected="$1" method="$2" path="$3"; shift 3
  local auth=()
  [ -z "$TOKEN" ] || auth=(-H "Authorization: Bearer $TOKEN")
  STEP=$((STEP + 1))
  local code
  code="$(curl -sS -o "$BODY" -D "$HEADERS" -w '%{http_code}' -X "$method" "${auth[@]}" "$@" "$BASE$path")"
  local mark="ok"; [ "$code" = "$expected" ] || mark="ОШИБКА"
  printf '%2d. %-6s %-62s ожидался %s, получен %s  [%s]\n' "$STEP" "$method" "$path" "$expected" "$code" "$mark"
  [ "$code" = "$expected" ] || { echo "Тело ответа:"; cat "$BODY"; exit 1; }
}
json() { sed -n "s/.*\"$1\":\"\\{0,1\\}\\([^\",}]*\\).*/\\1/p" "$BODY" | head -1; }

# Гость: карта
call 200 GET  "/api/v1/airports?limit=100"
call 200 GET  "/api/v1/flights/current"
ETAG="$(sed -n 's/^[Ee][Tt]ag: *//p' "$HEADERS" | tr -d '\r')"
call 304 GET  "/api/v1/flights/current" -H "If-None-Match: $ETAG"
call 401 GET  "/api/v1/congestion?$PERIOD"

# Вход
call 401 POST "/api/v1/sessions" -H 'Content-Type: application/json' -d "{\"username\":\"$USER_NAME\",\"password\":\"wrong-password\"}"
call 201 POST "/api/v1/sessions" -H 'Content-Type: application/json' -d "{\"username\":\"$USER_NAME\",\"password\":\"$PASSWORD\"}"
TOKEN="$(json accessToken)"
[ -n "$TOKEN" ] || { echo "Токен не получен"; exit 1; }

# Аналитика
call 200 GET  "/api/v1/congestion?$PERIOD"
call 200 GET  "/api/v1/congestion?$PERIOD" -H 'Accept: text/csv'
call 400 GET  "/api/v1/congestion?airportId=1&dateFrom=2026-06-11&dateTo=2026-06-10"

# Отчёт: создать, найти, удалить
call 201 POST "/api/v1/reports" -H 'Content-Type: application/json' \
  -d '{"name":"curl: Sheremetyevo, 10 June","airportId":1,"dateFrom":"2026-06-10","dateTo":"2026-06-10"}'
REPORT_ID="$(json id)"
call 200 GET    "/api/v1/reports"
call 204 DELETE "/api/v1/reports/$REPORT_ID"
call 404 GET    "/api/v1/reports/$REPORT_ID"

# Выход: токен больше не принимается
call 204 DELETE "/api/v1/sessions/current"
call 401 GET    "/api/v1/reports"

echo "Сценарий выполнен: $STEP запросов, все коды совпали с ожидаемыми."
