#!/usr/bin/env bash
# Demonstrates an actual incorrect HTTP response, not a mock or a changed test expectation.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
TARGET=ATAP.Api/Dtos/ApiDtos.cs
BACKUP="$(mktemp)"
cp "$TARGET" "$BACKUP"
restore() { cp "$BACKUP" "$TARGET"; rm -f "$BACKUP"; }
trap restore EXIT
trap 'exit 130' INT TERM
mkdir -p TestResults/lr3
node <<'JS'
const fs = require('node:fs');
const file = 'ATAP.Api/Dtos/ApiDtos.cs';
const source = fs.readFileSync(file, 'utf8');
const pattern = /(record AircraftResponse\(\s*int Id,\s*)(string Icao24)/;
if (!pattern.test(source)) throw new Error('Cannot find the expected Aircraft DTO; source was not changed');
fs.writeFileSync(file, source.replace(pattern, '$1[property: JsonIgnore] $2'));
JS
set +e
bash scripts/test-stand.sh with bash scripts/test-api-contract.sh > TestResults/lr3/mutation-failed.log 2>&1
RESULT=$?
set -e
[ "$RESULT" -ne 0 ] || { echo 'ERROR: test did not detect the mutation'; exit 1; }
grep -q "must have required property 'icao24'" TestResults/lr3/mutation-failed.log || {
  tail -40 TestResults/lr3/mutation-failed.log; echo 'Failed for a different reason'; exit 1;
}
restore
trap - EXIT
echo 'PASS: missing icao24 in the real API response was detected; original DTO restored.'
bash scripts/test-stand.sh with bash scripts/test-api-contract.sh > TestResults/lr3/mutation-restored.log 2>&1
tail -1 TestResults/lr3/mutation-restored.log
grep 'PASS:' TestResults/lr3/mutation-restored.log
