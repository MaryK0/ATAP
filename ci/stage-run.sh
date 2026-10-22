#!/usr/bin/env bash
# Один этап тестов в контейнере: ci/stage-run.sh unit | integration | e2e
# Код выхода — код выхода этапа; Jenkins по нему останавливает дальнейшие этапы.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
. ci/lib.sh
run_tests_container "${1:?укажите этап}"
