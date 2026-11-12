#!/usr/bin/env bash
# Run inside scripts/test-stand.sh with (postgres-test, redis-test, db-e2e).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
dotnet build ATAP.Api/ATAP.Api.csproj --no-restore --nologo > /dev/null
PORT="$(node -e 'const s=require("net").createServer();s.listen(0,"127.0.0.1",()=>{console.log(s.address().port);s.close()})')"
export ATAP_TEST_API="http://127.0.0.1:$PORT"
mkdir -p TestResults/lr3
dotnet ATAP.Api/bin/Debug/net8.0/ATAP.Api.dll --urls "$ATAP_TEST_API" \
  --ConnectionStrings:DefaultConnection "${ATAP_TEST_PG};Database=atap_e2e" \
  --Jwt:Key "${ATAP_TEST_JWT_KEY:-atap-test-only-signing-key-0123456789abcdefghijklmnopqrstuvwxyz}" \
  --ConnectionStrings:Redis "${ATAP_TEST_REDIS},abortConnect=false" > TestResults/lr3/api.log 2>&1 &
API_PID=$!
trap 'kill "$API_PID" 2>/dev/null || true; wait "$API_PID" 2>/dev/null || true' EXIT
for _ in $(seq 1 60); do
  if curl -fsS "$ATAP_TEST_API/openapi/openapi.yaml" > /dev/null 2>&1; then
    bash scripts/test-api-contract.sh
    exit $?
  fi
  kill -0 "$API_PID" 2>/dev/null || { cat TestResults/lr3/api.log; exit 1; }
  sleep 1
done
cat TestResults/lr3/api.log
echo 'API startup timed out' >&2
exit 1
