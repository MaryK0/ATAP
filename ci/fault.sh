#!/usr/bin/env bash
# Реальный отказ компонента только уникального стенда текущей сборки.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
. ci/lib.sh
target="${FAULT_TARGET:-none}"
case "$1:$target" in
  integration:postgres) service=postgres-test ;;
  e2e:api) service=atap-api ;;
  *) exit 0 ;;
esac
echo "[fault] build=$BUILD_NUMBER project=$PROJECT stopping=$service"
docker compose -p "$PROJECT" -f docker-compose.test.yml stop "$service"
