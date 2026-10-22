#!/usr/bin/env bash
# Управление тестовым стендом ATAP (отдельные PostgreSQL и Redis для тестов).
#
#   scripts/test-stand.sh up              поднять стенд (PostgreSQL, Redis, API), напечатать строки подключения (export ...)
#   scripts/test-stand.sh down            остановить и удалить стенд вместе с данными
#   scripts/test-stand.sh with <команда>  поднять стенд, выполнить команду, ВСЕГДА удалить стенд
#
# Каждый запуск получает уникальное имя проекта и свободные порты, поэтому несколько запусков
# (разработчики на одной машине, параллельные задания CI) не мешают друг другу.
# Имя проекта можно задать вручную: ATAP_TEST_PROJECT=atap-test-ci-42
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="$ROOT/docker-compose.test.yml"
PROJECT="${ATAP_TEST_PROJECT:-atap-test-$(date +%s)-$RANDOM}"

compose() { docker compose -p "$PROJECT" -f "$COMPOSE_FILE" "$@"; }

# Ждёт, пока контейнер сервиса станет healthy; если сервис не запускался — пропускает его
wait_healthy() {
  local service="$1" id status
  id="$(compose ps -q "$service" 2>/dev/null || true)"
  [ -n "$id" ] || return 0
  for _ in $(seq 1 ${ATAP_STAND_TIMEOUT:-600}); do
    status="$(docker inspect -f '{{.State.Health.Status}}' "$id" 2>/dev/null || echo missing)"
    [ "$status" = "healthy" ] && return 0
    [ "$status" = "unhealthy" ] && { echo "Сервис $service нездоров" >&2; compose logs --tail 30 "$service" >&2; return 1; }
    sleep 1
  done
  echo "Сервис $service не стал healthy за отведённое время" >&2
  return 1
}

# Ждёт завершения разового контейнера и проверяет код выхода 0
wait_completed() {
  local service="$1" id state code
  id="$(compose ps -a -q "$service" 2>/dev/null || true)"
  [ -n "$id" ] || return 0
  for _ in $(seq 1 ${ATAP_STAND_TIMEOUT:-600}); do
    state="$(docker inspect -f '{{.State.Status}}' "$id")"
    if [ "$state" = "exited" ]; then
      code="$(docker inspect -f '{{.State.ExitCode}}' "$id")"
      [ "$code" = "0" ] && return 0
      echo "Разовый сервис $service завершился с кодом $code" >&2
      compose logs --tail 30 "$service" >&2
      return 1
    fi
    sleep 1
  done
  echo "Разовый сервис $service не завершился за отведённое время" >&2
  return 1
}

stand_up() {
  # ATAP_STAND_SERVICES — список сервисов через пробел (по умолчанию весь стенд, включая контейнер API)
  # shellcheck disable=SC2086
  compose up -d --build ${ATAP_STAND_SERVICES:-} >&2
  # --wait не подходит: разовый контейнер db-e2e завершается кодом 0, и compose считает это ошибкой
  wait_healthy postgres-test
  wait_healthy redis-test
  wait_completed db-e2e
  wait_healthy atap-api
  local pg redis api
  pg="$(compose port postgres-test 5432)"
  redis="$(compose port redis-test 6379)"
  api="$(compose port atap-api 8080 2>/dev/null || true)"
  # ${pg##*:} — номер порта из «127.0.0.1:55001»
  export ATAP_TEST_PROJECT="$PROJECT"
  export ATAP_TEST_PG="Host=127.0.0.1;Port=${pg##*:};Username=postgres;Password=test_pass;Pooling=true;Maximum Pool Size=20"
  export ATAP_TEST_REDIS="127.0.0.1:${redis##*:}"
  if [ -n "$api" ]; then
    export ATAP_TEST_API="http://127.0.0.1:${api##*:}"
  fi
}

stand_down() {
  compose down -v --remove-orphans >&2 || true
}

case "${1:-}" in
  up)
    stand_up
    echo "export ATAP_TEST_PROJECT='$ATAP_TEST_PROJECT'"
    echo "export ATAP_TEST_PG='$ATAP_TEST_PG'"
    echo "export ATAP_TEST_REDIS='$ATAP_TEST_REDIS'"
    [ -z "${ATAP_TEST_API:-}" ] || echo "export ATAP_TEST_API='$ATAP_TEST_API'"
    ;;
  down)
    PROJECT="${ATAP_TEST_PROJECT:?укажите ATAP_TEST_PROJECT}"
    stand_down
    ;;
  with)
    shift
    [ "$#" -gt 0 ] || { echo "укажите команду после 'with'" >&2; exit 2; }
    # Стенд удаляется при любом завершении, в том числе при падении тестов или прерывании (Ctrl+C)
    trap stand_down EXIT
    stand_up
    echo "Стенд $PROJECT: PostgreSQL ${ATAP_TEST_PG%%;Username*}, Redis $ATAP_TEST_REDIS, API ${ATAP_TEST_API:-не запущен}" >&2
    "$@"
    ;;
  *)
    sed -n '2,9p' "$0" >&2
    exit 2
    ;;
esac
