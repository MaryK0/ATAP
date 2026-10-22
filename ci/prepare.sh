#!/usr/bin/env bash
# Подготовка сборки: образ с тестами и тома для выкачанного кода и результатов.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
. ci/lib.sh
docker build -f ci/Dockerfile.tests -t "$IMAGE" .
docker volume create "$RESULTS_VOL" >/dev/null
docker volume create "$WORK_VOL" >/dev/null
echo "Тома сборки $BUILD_NUMBER созданы: $RESULTS_VOL, $WORK_VOL"
