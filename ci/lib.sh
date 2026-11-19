#!/usr/bin/env bash
# Общие определения для скриптов этапов в Jenkins (подключается командой `. ci/lib.sh`).
# Все имена зависят от номера сборки, поэтому параллельные и повторные сборки не мешают друг другу.
# Git Bash на Windows иначе преобразует пути вида /work в Windows-пути
export MSYS_NO_PATHCONV=1
: "${BUILD_NUMBER:?переменная BUILD_NUMBER задаётся Jenkins}"
: "${HOST_REPO:?переменная HOST_REPO задаётся в docker-compose.jenkins.yml}"

PROJECT="atap-ci-${BUILD_NUMBER}"            # проект docker compose тестового стенда
IMAGE="atap-tests:ci"                        # образ с тестами (ci/Dockerfile.tests)
RESULTS_VOL="atap-ci-results-${BUILD_NUMBER}" # результаты: Allure JSON, TRX, статусы этапов
WORK_VOL="atap-ci-work-${BUILD_NUMBER}"       # выкачанный и собранный репозиторий (общий для этапов)
NETWORK="${PROJECT}_default"                  # сеть стенда: контейнер с тестами подключается к ней
STAGES=(unit integration e2e)

# Адреса сервисов стенда внутри его сети (тесты обращаются к ним по именам сервисов)
STAND_PG="Host=postgres-test;Port=5432;Username=postgres;Password=test_pass;Pooling=true;Maximum Pool Size=20"
if [ "${FAULT_TARGET:-none}" = postgres ]; then
  STAND_PG+=";Timeout=2"
fi
STAND_REDIS="redis-test:6379"
STAND_API="http://atap-api:8080"

# Запускает контейнер с тестами; если стенд поднят — подключает его к сети стенда
run_tests_container() {
  local network_args=()
  if docker network inspect "$NETWORK" >/dev/null 2>&1; then
    network_args=(--network "$NETWORK")
  fi
  docker run --rm \
    -v "${WORK_VOL}:/work" \
    -v "${RESULTS_VOL}:/results" \
    --mount "type=bind,source=${HOST_REPO},target=/repo,readonly" \
    -e REPO_URL=file:///repo \
    -e COMMIT="${GIT_COMMIT:-HEAD}" \
    -e ATAP_TEST_PG="$STAND_PG" \
    -e ATAP_TEST_REDIS="$STAND_REDIS" \
    -e ATAP_TEST_API="$STAND_API" \
    "${network_args[@]}" \
    "$IMAGE" "$@"
}

# Копирует результаты из тома в каталог рабочей области Jenkins (без bind-mount: Jenkins сам работает в контейнере)
export_results() {
  rm -rf ci-results && mkdir -p ci-results
  docker run --rm -v "${RESULTS_VOL}:/src:ro" --entrypoint tar "$IMAGE" -C /src -cf - . | tar -C ci-results -xf -
  if [ -d ci-results/developers ]; then
    mkdir -p ci-results/allure-results ci-results/status
    for stage in integration e2e; do
      local code=0 developer
      for developer in dev-{1..5}; do
        [ "$(cat "ci-results/developers/$developer/status/$stage" 2>/dev/null)" = 0 ] || code=1
      done
      echo "$code" > "ci-results/status/$stage"
    done
    for dir in ci-results/developers/*/allure-results; do
      [ ! -d "$dir" ] || cp -r "$dir"/. ci-results/allure-results/
    done
  fi
}
