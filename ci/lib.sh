#!/usr/bin/env bash
# Общие определения для скриптов этапов в Jenkins (подключается командой `. ci/lib.sh`).
# Имена зависят от номера сборки: её стенд отделён от других запусков и рабочих контейнеров.
# Git Bash на Windows иначе преобразует пути вида /work в Windows-пути
export MSYS_NO_PATHCONV=1
: "${BUILD_NUMBER:?переменная BUILD_NUMBER задаётся Jenkins}"
: "${HOST_REPO:?переменная HOST_REPO задаётся в docker-compose.jenkins.yml}"

PROJECT="atap-ci-${BUILD_NUMBER}"            # проект docker compose тестового стенда
IMAGE="atap-tests:ci-${BUILD_NUMBER}"        # отдельный тег образа тестов каждой сборки
HELPER_IMAGE="atap-jenkins:ci"               # готовый служебный образ, не пересобирается заданием
export ATAP_TEST_DB_IMAGE="atap-test-db:ci-${BUILD_NUMBER}"
export ATAP_TEST_API_IMAGE="atap-api:ci-${BUILD_NUMBER}"
RESULTS_VOL="atap-ci-results-${BUILD_NUMBER}" # результаты: Allure JSON, TRX, статусы этапов
WORK_VOL="atap-ci-work-${BUILD_NUMBER}"       # выкачанный и собранный репозиторий (общий для этапов)
NETWORK="${PROJECT}_default"                  # сеть стенда: контейнер с тестами подключается к ней
STAGES=(unit integration e2e)

# Адреса сервисов стенда внутри его сети (тесты обращаются к ним по именам сервисов)
STAND_PG="Host=postgres-test;Port=5432;Username=postgres;Password=test_pass;Pooling=true;Maximum Pool Size=20"
STAND_REDIS="redis-test:6379"
STAND_API="http://atap-api:8080"

# Запускает контейнер с тестами; если стенд поднят — подключает его к сети стенда
run_tests_container() {
  local network_args=()
  if docker network inspect "$NETWORK" >/dev/null 2>&1; then
    network_args=(--network "$NETWORK")
  fi
  docker run --rm \
    --name "${PROJECT}-tests-${1}" \
    --label "atap.test.project=$PROJECT" \
    --cpus 1 --memory 768m --pids-limit 256 \
    -v "${WORK_VOL}:/work" \
    -v "${RESULTS_VOL}:/results" \
    --mount "type=bind,source=${HOST_REPO},target=/repo,readonly" \
    -e REPO_URL=file:///repo \
    -e COMMIT="${GIT_COMMIT:-HEAD}" \
    -e DOTNET_PROCESSOR_COUNT=2 \
    -e ATAP_TEST_PG="$STAND_PG" \
    -e ATAP_TEST_REDIS="$STAND_REDIS" \
    -e ATAP_TEST_API="$STAND_API" \
    "${network_args[@]}" \
    "$IMAGE" "$@"
}

# Копирует результаты из тома в каталог рабочей области Jenkins (без bind-mount: Jenkins сам работает в контейнере)
export_results() {
  rm -rf ci-results && mkdir -p ci-results
  docker run --rm -v "${RESULTS_VOL}:/src:ro" --entrypoint tar "$HELPER_IMAGE" -C /src -cf - . | tar -C ci-results -xf -
}

# Если образ тестов не собрался, отмечаем непройденный этап без .NET и повторной сборки.
fallback_skipped() {
  docker run --rm -v "${RESULTS_VOL}:/results" --entrypoint bash "$HELPER_IMAGE" -c '
    stage="$1"; uuid="$(cat /proc/sys/kernel/random/uuid)"
    mkdir -p /results/allure-results /results/status
    printf "{\"uuid\":\"%s\",\"historyId\":\"ci-%s-not-executed\",\"name\":\"%s not executed\",\"status\":\"skipped\",\"stage\":\"finished\",\"labels\":[{\"name\":\"suite\",\"value\":\"CI\"}]}\n" \
      "$uuid" "$stage" "$stage" > "/results/allure-results/$uuid-result.json"
    echo skipped > "/results/status/$stage"
  ' -- "$1"
}
