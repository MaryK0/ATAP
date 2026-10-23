#!/usr/bin/env bash
# Управление Jenkins для тестов ATAP.
#   ci/jenkins.sh up      собрать образ и запустить Jenkins (http://localhost:8085)
#   ci/jenkins.sh down    остановить Jenkins (данные и история отчётов сохраняются)
#   ci/jenkins.sh build [none|integration|e2e]  запустить сборку atap-tests (необязательно — имитировать падение этапа)
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
    # Jenkins защищён от CSRF даже без аутентификации: сначала получаем токен (crumb), затем запускаем сборку
    # Токен привязан к сессии Jenkins: cookie из запроса токена передаём и в запросе запуска
    jar="$(mktemp)"; trap 'rm -f "$jar"' EXIT
    crumb="$(curl -fs -c "$jar" "$URL/crumbIssuer/api/json")"
    field="$(printf '%s' "$crumb" | sed -n 's/.*"crumbRequestField":"\([^"]*\)".*/\1/p')"
    value="$(printf '%s' "$crumb" | sed -n 's/.*"crumb":"\([^"]*\)".*/\1/p')"
    number="$(curl -fs "$URL/job/atap-tests/api/json?tree=nextBuildNumber" | sed -n 's/.*"nextBuildNumber":\([0-9]*\).*/\1/p')"
    if [ -n "${2:-}" ]; then
      curl -fs -b "$jar" -X POST -H "$field: $value" "$URL/job/atap-tests/buildWithParameters?SIMULATE_FAILURE=$2" -o /dev/null
    else
      curl -fs -b "$jar" -X POST -H "$field: $value" "$URL/job/atap-tests/build" -o /dev/null
    fi
    echo "Сборка №$number запущена: $URL/job/atap-tests/$number/" >&2
    for _ in $(seq 1 600); do
      sleep 5
      info="$(curl -fs "$URL/job/atap-tests/$number/api/json?tree=building,result" || true)"
      case "$info" in
        *'"building":false'*) echo "$info" >&2; case "$info" in *'"result":"SUCCESS"'*) exit 0 ;; *) exit 1 ;; esac ;;
      esac
    done
    echo "Сборка не завершилась за 50 минут" >&2; exit 1
    ;;
  reset)
    "${COMPOSE[@]}" down -v
    ;;
  *)
    sed -n '2,6p' "$0" >&2
    exit 2
    ;;
esac
