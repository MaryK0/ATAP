#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
mkdir -p TestResults/lr3
ATAP_API_STAGE=airports node web_lr3/contract-tests.mjs | tee TestResults/lr3/stage-1-airports.log
ATAP_API_STAGE=auth node web_lr3/contract-tests.mjs | tee TestResults/lr3/stage-2-auth.log
ATAP_API_STAGE=all bash scripts/test-lr3.sh
