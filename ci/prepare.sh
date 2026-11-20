#!/usr/bin/env bash
# Подготовка сборки: образ с тестами и тома для выкачанного кода и результатов.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
. ci/lib.sh
echo "[build] number=$BUILD_NUMBER commit=${GIT_COMMIT:-HEAD} project=$PROJECT image=$IMAGE"
# Сохраняемый тег удерживает слои с пакетами после удаления тегов сборок.
# Подготовка образа общая, выполнение тестов остаётся параллельным.
cache_dir="${JENKINS_HOME:-/var/jenkins_home}/caches/atap"
mkdir -p "$cache_dir"
exec 8>"$cache_dir/test-image.lock"
flock -x 8
cache_args=()
for candidate in atap-tests:cache atap-tests:ci; do
  if docker image inspect "$candidate" >/dev/null 2>&1; then
    cache_args+=(--cache-from "$candidate")
  fi
done
docker build "${cache_args[@]}" -f ci/Dockerfile.tests -t "$IMAGE" -t atap-tests:cache .
flock -u 8
docker volume create "$RESULTS_VOL" >/dev/null
docker volume create "$WORK_VOL" >/dev/null
echo "Тома сборки $BUILD_NUMBER созданы: $RESULTS_VOL, $WORK_VOL"
