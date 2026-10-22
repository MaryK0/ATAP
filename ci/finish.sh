#!/usr/bin/env bash
# Завершающий шаг каждой сборки (выполняется всегда, в том числе после падения):
#   1. этапы, которые не запускались, помечаются как skipped;
#   2. результаты копируются в рабочую область, строится отчёт Allure с историей запусков (тренды);
#   3. тестовый стенд и тома сборки удаляются — хранилище возвращается в исходное состояние.
set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
. ci/lib.sh

cleanup() {
  echo "[finish] Удаляю стенд и тома сборки $BUILD_NUMBER"
  ATAP_TEST_PROJECT="$PROJECT" bash scripts/test-stand.sh down || true
  docker volume rm -f "$RESULTS_VOL" "$WORK_VOL" >/dev/null 2>&1 || true
}
trap cleanup EXIT

# 1. Пропущенные этапы: у них нет файла статуса в томе результатов
export_results
for stage in "${STAGES[@]}"; do
  if [ ! -f "ci-results/status/$stage" ]; then
    echo "[finish] Этап $stage не запускался — помечаю как skipped"
    run_tests_container skip "$stage" || echo "[finish] не удалось пометить этап $stage"
  fi
done

# 2. Отчёт Allure с историей предыдущих запусков (том allure_history общий для всех сборок)
export_results
mkdir -p ci-results/allure-results
HISTORY_DIR="${ALLURE_HISTORY_DIR:-/allure-history}"
HISTORY="$HISTORY_DIR/history"
if [ -d "$HISTORY" ]; then
  rm -rf ci-results/allure-results/history
  cp -r "$HISTORY" ci-results/allure-results/history
fi
cat > ci-results/allure-results/environment.properties <<ENV
Сборка=${BUILD_NUMBER}
Коммит=${GIT_COMMIT:-неизвестен}
Стенд=PostgreSQL 15 + PostGIS, Redis 7, ATAP.Api (контейнеры)
ENV
allure generate ci-results/allure-results -o ci-results/allure-report --clean
if [ -d ci-results/allure-report/history ]; then
  rm -rf "$HISTORY" && mkdir -p "$HISTORY_DIR" && cp -r ci-results/allure-report/history "$HISTORY"
fi

# Сводка по этапам
echo "[finish] Статусы этапов:"
for stage in "${STAGES[@]}"; do
  echo "  $stage: $(cat "ci-results/status/$stage" 2>/dev/null || echo 'нет данных')"
done
