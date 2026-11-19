#!/usr/bin/env bash
# Управление Jenkins для тестов ATAP.
#   ci/jenkins.sh up      собрать образ и запустить Jenkins (http://localhost:8085)
#   ci/jenkins.sh down    остановить Jenkins (данные и история отчётов сохраняются)
#   ci/jenkins.sh build   запустить обычную сборку atap-tests и дождаться результата
#   ci/jenkins.sh reset   удалить Jenkins вместе с данными и историей отчётов
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# Путь к репозиторию в том виде, в каком его понимает Docker на хосте (на Windows — C:/ATAP)
export HOST_REPO="$(cd "$ROOT" && (pwd -W 2>/dev/null || pwd))"
COMPOSE=(docker compose -p atap-ci -f "$ROOT/ci/docker-compose.jenkins.yml")
URL="http://localhost:8085"

case "${1:-}" in
  up)
    "${COMPOSE[@]}" up -d --build
    echo "Жду готовности Jenkins…" >&2
    for _ in $(seq 1 180); do
      curl -fs -o /dev/null "$URL/login" && { echo "Jenkins готов: $URL" >&2; exit 0; }
      sleep 2
    done
    echo "Jenkins не поднялся за 6 минут" >&2; exit 1
    ;;
  down)
    "${COMPOSE[@]}" down
    ;;
  build)
    jar="$(mktemp)"; headers="$(mktemp)"
    trap 'rm -f "$jar" "$headers"' EXIT
    crumb="$(curl -fs -c "$jar" "$URL/crumbIssuer/api/json")"
    field="$(printf '%s' "$crumb" | sed -n 's/.*"crumbRequestField":"\([^"]*\)".*/\1/p')"
    value="$(printf '%s' "$crumb" | sed -n 's/.*"crumb":"\([^"]*\)".*/\1/p')"
    endpoint=build
    # Совместимость с сохранённой конфигурацией задания до первого обновлённого запуска.
    job="$(curl -g -fs "$URL/job/atap-tests/api/json?tree=property[parameterDefinitions[name]]")"
    [[ "$job" != *'"parameterDefinitions"'* ]] || endpoint=buildWithParameters
    curl -fs -b "$jar" -X POST -H "$field: $value" -D "$headers" \
      "$URL/job/atap-tests/$endpoint" -o /dev/null
    queue="$(sed -n 's/^[Ll]ocation: *//p' "$headers" | tr -d '\r')"
    [ -n "$queue" ] || { echo 'Jenkins did not return a queue item' >&2; exit 1; }
    number=""
    for _ in $(seq 1 600); do
      info="$(curl -g -fs "${queue%/}/api/json?tree=cancelled,executable[number]" || true)"
      [[ "$info" != *'"cancelled":true'* ]] || { echo 'Build cancelled' >&2; exit 1; }
      number="$(printf '%s' "$info" | sed -n 's/.*"number":\([0-9]*\).*/\1/p')"
      [ -z "$number" ] || break
      sleep 2
    done
    [ -n "$number" ] || { echo 'Queue timeout' >&2; exit 1; }
    echo "Build #$number: $URL/job/atap-tests/$number/" >&2
    for _ in $(seq 1 600); do
      info="$(curl -fs "$URL/job/atap-tests/$number/api/json?tree=building,result" || true)"
      if [[ "$info" == *'"building":false'* ]]; then
        echo "$info" >&2
        echo "Allure: $URL/job/atap-tests/$number/artifact/ci-results/allure-report/index.html" >&2
        [[ "$info" == *'"result":"SUCCESS"'* ]] && exit 0 || exit 1
      fi
      sleep 5
    done
    echo 'Build timeout; check Jenkins' >&2; exit 1
    ;;
  reset)
    "${COMPOSE[@]}" down -v
    ;;
  *)
    sed -n '2,6p' "$0" >&2
    exit 2
    ;;
esac
