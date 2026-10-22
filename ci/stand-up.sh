#!/usr/bin/env bash
# Поднимает тестовый стенд (PostgreSQL, Redis, база E2E, API) под именем проекта этой сборки.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
. ci/lib.sh
ATAP_TEST_PROJECT="$PROJECT" bash scripts/test-stand.sh up > /dev/null
docker compose -p "$PROJECT" -f docker-compose.test.yml ps
