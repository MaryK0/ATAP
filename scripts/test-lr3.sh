#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
mkdir -p TestResults/lr3
node web_lr3/contract-tests.mjs | tee TestResults/lr3/contract.log
for project in ATAP.BusinessLogic.Tests ATAP.DataAccess.Tests ATAP.BlazorUI.Tests ATAP.IntegrationTests ATAP.E2ETests; do
  dotnet test "$project" --no-build --nologo --logger "trx;LogFileName=lr3.trx" \
    --results-directory "TestResults/lr3/$project"
done
# Serve the legacy Blazor UI while the API and the ConsoleUI HTTP adapter are running.
GUI_PORT="$(node -e 'const s=require("net").createServer();s.listen(0,"127.0.0.1",()=>{console.log(s.address().port);s.close()})')"
GUI_URL="http://127.0.0.1:$GUI_PORT"
dotnet run --project ATAP.BlazorUI --no-build --no-launch-profile -- \
  --urls "$GUI_URL" --StorageBackend postgres \
  --ConnectionStrings:DefaultConnection "${ATAP_TEST_PG};Database=atap_e2e" \
  --ConnectionStrings:Redis "${ATAP_TEST_REDIS},abortConnect=false" > TestResults/lr3/blazor.log 2>&1 &
GUI_PID=$!
trap 'kill "$GUI_PID" 2>/dev/null || true; wait "$GUI_PID" 2>/dev/null || true' EXIT
READY=0
for _ in $(seq 1 60); do
  if curl -fsS "$GUI_URL/" > TestResults/lr3/blazor-home.html; then READY=1; break; fi
  sleep 1
done
[ "$READY" = 1 ] || { cat TestResults/lr3/blazor.log; exit 1; }
curl -fsS "$GUI_URL/map" > TestResults/lr3/blazor-map.html
curl -fsS "$ATAP_TEST_API/swagger/index.html" > TestResults/lr3/swagger.html
curl -fsS "$ATAP_TEST_API/openapi/openapi.yaml" > TestResults/lr3/served-openapi.yaml
cmp web_lr2/openapi.yaml TestResults/lr3/served-openapi.yaml
export ATAP_API_URL="$ATAP_TEST_API/api/v1/"
dotnet run --project ATAP.ConsoleUI --no-build -- --smoke | tee TestResults/lr3/tui.log
echo "PASS: E2E, API contract, TUI HTTP scenario and Blazor HTTP pages. API and Blazor were running during TUI requests."
