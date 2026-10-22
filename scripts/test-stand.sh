#!/usr/bin/env bash
# Управление тестовым стендом ATAP (отдельные PostgreSQL и Redis для тестов).
#
#   scripts/test-stand.sh up              поднять стенд, напечатать строки подключения (export ...)
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

stand_up() {
  compose up -d --build --wait --wait-timeout 180 >&2
  local pg redis
  pg="$(compose port postgres-test 5432)"
  redis="$(compose port redis-test 6379)"
  # ${pg##*:} — номер порта из «127.0.0.1:55001»
  export ATAP_TEST_PROJECT="$PROJECT"
  export ATAP_TEST_PG="Host=127.0.0.1;Port=${pg##*:};Username=postgres;Password=test_pass;Pooling=true;Maximum Pool Size=20"
  export ATAP_TEST_REDIS="127.0.0.1:${redis##*:}"
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
    echo "Стенд $PROJECT: PostgreSQL ${ATAP_TEST_PG%%;Username*}, Redis $ATAP_TEST_REDIS" >&2
    "$@"
    ;;
  *)
    sed -n '2,9p' "$0" >&2
    exit 2
    ;;
esac
