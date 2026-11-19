#!/usr/bin/env bash
# Один разработчик: собственный стенд, тома, integration/E2E и HTTP-журнал.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
developer="${1:?developer}"
[[ "$developer" =~ ^dev-[1-5]$ ]] || exit 2
parent="$BUILD_NUMBER"
export BUILD_NUMBER="${parent}-${developer}"
. ci/lib.sh
parent_results="atap-ci-results-${parent}"
parent_work="atap-ci-work-${parent}"
cleanup() {
  docker compose -p "$PROJECT" -f docker-compose.test.yml logs --no-color atap-api 2>&1 | \
    docker run --rm -i -v "$RESULTS_VOL:/results" --entrypoint bash "$IMAGE" -c 'cat > /results/api.log' || true
  docker run --rm -v "$RESULTS_VOL:/src" -v "$parent_results:/dst" --entrypoint bash "$IMAGE" \
    -c 'mkdir -p "/dst/developers/$1"; cp -a /src/. "/dst/developers/$1/"' -- "$developer" || true
  ATAP_TEST_PROJECT="$PROJECT" bash scripts/test-stand.sh down || true
  docker volume rm -f "$WORK_VOL" "$RESULTS_VOL" >/dev/null 2>&1 || true
}
trap cleanup EXIT
docker volume create "$WORK_VOL" >/dev/null
docker volume create "$RESULTS_VOL" >/dev/null
# Собранный коммит одинаков для всех; каждый получает собственную изменяемую копию.
docker run --rm -v "$parent_work:/src:ro" -v "$WORK_VOL:/dst" --entrypoint bash "$IMAGE" -c 'cp -a /src/. /dst/'
ATAP_TEST_PROJECT="$PROJECT" ATAP_STAND_NO_BUILD=1 bash scripts/test-stand.sh up > "/tmp/${PROJECT}.env"
api_port="$(docker compose -p "$PROJECT" -f docker-compose.test.yml port atap-api 8080)"
echo "[developer] developer=$developer project=$PROJECT api=http://$api_port network=$NETWORK"
docker run --rm -v "$RESULTS_VOL:/results" --entrypoint bash "$IMAGE" \
  -c 'printf "developer=%s\nproject=%s\napi=http://%s\n" "$1" "$2" "$3" > /results/developer.txt' -- "$developer" "$PROJECT" "$api_port"
docker run --rm -v "$parent_results:/sync" --entrypoint bash "$IMAGE" -c '
  mkdir -p /sync/ready; touch "/sync/ready/$1"
  for i in {1..120}; do
    if [ "$(find /sync/ready -type f | wc -l)" = 5 ]; then exit 0; fi
    sleep 1
  done
  echo "Not all five developer stands became ready" >&2; exit 1' -- "$developer"
echo "[developer] developer=$developer all-five-ready tests-start=$(date -u +%FT%TZ)"
code=0
run_tests_container integration || code=$?
if [ "$code" = 0 ]; then
  run_tests_container e2e || code=$?
else
  run_tests_container skip e2e || true
fi
if [ "$code" = 0 ]; then
  ATAP_TEST_PROJECT="$PROJECT" ATAP_CAPTURE_EXISTING=1 ATAP_DEVELOPER="$developer" \
    ATAP_CAPTURE_IN_CONTAINER=1 ATAP_TRAFFIC_VOLUME="$RESULTS_VOL" WORK_VOL="$WORK_VOL" IMAGE="$IMAGE" \
    bash scripts/capture-traffic.sh || code=$?
fi
docker run --rm -v "$WORK_VOL:/work" -v "$RESULTS_VOL:/results" --entrypoint python3 "$IMAGE" \
  /work/src/ci/label-results.py /results/allure-results "$developer"
echo "[developer] developer=$developer tests-end=$(date -u +%FT%TZ) result=$code"
exit "$code"
