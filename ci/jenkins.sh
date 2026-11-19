#!/usr/bin/env bash
# Управление Jenkins для тестов ATAP.
#   ci/jenkins.sh up      собрать образ и запустить Jenkins (http://localhost:8085)
#   ci/jenkins.sh down    остановить Jenkins (данные и история отчётов сохраняются)
#   ci/jenkins.sh build   запустить сборку atap-tests и дождаться результата (Python 3)
#   ci/jenkins.sh parallel   пять независимых разработчиков в одной сборке
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
    cd "$ROOT"
    python -u ci/demo.py build
    ;;
  parallel)
    cd "$ROOT"
    python -u ci/demo.py parallel
    ;;
  reset)
    "${COMPOSE[@]}" down -v
    ;;
  *)
    sed -n '2,6p' "$0" >&2
    exit 2
    ;;
esac
