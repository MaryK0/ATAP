#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
docker compose -p "${ATAP_TEST_PROJECT:?Use the isolated stand}" -f docker-compose.test.yml stop redis-test
node web_lr3/check-no-redis.mjs
