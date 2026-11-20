#!/usr/bin/env bash
# Завершающий шаг каждой сборки (выполняется всегда, в том числе после падения):
#   1. этапы, которые не запускались, помечаются как skipped;
#   2. результаты копируются в рабочую область, строится отчёт Allure с историей запусков (тренды);
#   3. тестовый стенд и тома сборки удаляются — хранилище возвращается в исходное состояние.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
. ci/lib.sh

cleanup() {
  local runners=()
  mapfile -t runners < <(docker ps -aq --filter "label=atap.test.project=$PROJECT")
  if [ "${#runners[@]}" -gt 0 ]; then docker rm -f "${runners[@]}" >/dev/null 2>&1 || true; fi
  echo "[finish] Удаляю стенд и тома сборки $BUILD_NUMBER"
  ATAP_TEST_PROJECT="$PROJECT" bash scripts/test-stand.sh down || true
  docker volume rm -f "$RESULTS_VOL" "$WORK_VOL" >/dev/null 2>&1 || true
  # Удаляем только теги этой сборки; слои и образы других сборок не затрагиваются.
  docker image rm "$IMAGE" "$ATAP_TEST_DB_IMAGE" "$ATAP_TEST_API_IMAGE" >/dev/null 2>&1 || true
}
trap cleanup EXIT

# 1. Пропущенные этапы: у них нет файла статуса в томе результатов
export_results
for stage in "${STAGES[@]}"; do
  if [ ! -f "ci-results/status/$stage" ]; then
    echo "[finish] Этап $stage не запускался — помечаю как skipped"
    if docker image inspect "$IMAGE" >/dev/null 2>&1; then
      run_tests_container skip "$stage" || fallback_skipped "$stage"
    else
      fallback_skipped "$stage"
    fi
  fi
done

# 2. Отчёт Allure с историей предыдущих запусков (том allure_history общий для всех сборок)
export_results
mkdir -p ci-results/allure-results
HISTORY_DIR="${ALLURE_HISTORY_DIR:-/allure-history}"
HISTORY="$HISTORY_DIR/history"
mkdir -p "$HISTORY_DIR"
# Одна блокировка на общем томе: чтение → генерация → запись истории неделимы.
# Дескриптор освобождается при выходе, в том числе после ошибки.
exec 9>"$HISTORY_DIR/.lock"
echo "[allure-history] build=$BUILD_NUMBER waiting for lock"
flock -x 9
echo "[allure-history] build=$BUILD_NUMBER lock acquired"
if [ -d "$HISTORY" ]; then
  rm -rf ci-results/allure-results/history
  cp -r "$HISTORY" ci-results/allure-results/history
fi
cat > ci-results/allure-results/environment.properties <<ENV
Сборка=${BUILD_NUMBER}
Коммит=${GIT_COMMIT:-неизвестен}
Стенд=PostgreSQL 15 + PostGIS, Redis 7, ATAP.Api (контейнеры)
ENV
cat > ci-results/allure-results/executor.json <<JSON
{"name":"Jenkins","type":"jenkins","buildName":"${JOB_NAME:-atap-tests} #${BUILD_NUMBER}","buildOrder":${BUILD_NUMBER},"buildUrl":"${BUILD_URL:-}","reportName":"ATAP #${BUILD_NUMBER}","reportUrl":"${BUILD_URL:-}artifact/ci-results/allure-report/index.html"}
JSON
allure generate ci-results/allure-results -o ci-results/allure-report --clean
if [ -d ci-results/allure-report/history ]; then
  rm -rf "$HISTORY" && mkdir -p "$HISTORY_DIR" && cp -r ci-results/allure-report/history "$HISTORY"
fi
echo "[allure-history] build=$BUILD_NUMBER history saved"

# Сводка по этапам
echo "[finish] Статусы этапов:"
for stage in "${STAGES[@]}"; do
  echo "  $stage: $(cat "ci-results/status/$stage" 2>/dev/null || echo 'нет данных')"
done
