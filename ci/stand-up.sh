#!/usr/bin/env bash
# Поднимает тестовый стенд (PostgreSQL, Redis, база E2E, API) под именем проекта этой сборки.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
. ci/lib.sh
ATAP_TEST_PROJECT="$PROJECT" bash scripts/test-stand.sh up > /dev/null
docker compose -p "$PROJECT" -f docker-compose.test.yml ps
api_address="$(docker compose -p "$PROJECT" -f docker-compose.test.yml port atap-api 8080)"
metadata="build=$BUILD_NUMBER project=$PROJECT api=http://$api_address"
echo "[stand] $metadata"
printf '%s\n' "$metadata" | docker run --rm -i -v "$RESULTS_VOL:/results" \
  --entrypoint bash "$HELPER_IMAGE" -c 'cat > /results/stand.txt'
