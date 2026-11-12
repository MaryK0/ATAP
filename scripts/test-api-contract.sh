#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
node web_lr3/contract-tests.mjs
if [ "${ATAP_CHECK_NO_REDIS:-0}" = 1 ]; then
  bash scripts/test-api-no-redis.sh
fi
