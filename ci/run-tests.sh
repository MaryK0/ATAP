#!/usr/bin/env bash
# Запуск одного этапа тестов внутри контейнера atap-tests.
#
#   run-tests.sh unit          unit-тесты бизнес-логики и доступа к данным (без БД и сети)
#   run-tests.sh integration   интеграционные тесты (нужны ATAP_TEST_PG и ATAP_TEST_REDIS)
#   run-tests.sh e2e           E2E-тест демонстрационного сценария (нужен ATAP_TEST_API)
#   run-tests.sh skip <этап>   пометить тесты этапа как пропущенные (Allure-статус skipped)
#
# Переменные окружения:
#   REPO_URL     откуда выкачать репозиторий (по умолчанию file:///repo — каталог, смонтированный в контейнер)
#   COMMIT       коммит или ветка для проверки (по умолчанию HEAD)
#   RESULTS_DIR  куда писать результаты (по умолчанию /results)
#
# Код выхода — код выхода dotnet test этапа (0 — все тесты прошли).
set -euo pipefail

MODE="${1:?укажите этап: unit | integration | e2e | skip <этап>}"
REPO_URL="${REPO_URL:-file:///repo}"
COMMIT="${COMMIT:-HEAD}"
RESULTS_DIR="${RESULTS_DIR:-/results}"
WORK=/work/src
ALLURE_OUT="$RESULTS_DIR/allure-results"
mkdir -p "$ALLURE_OUT" "$RESULTS_DIR/trx" "$RESULTS_DIR/status"

declare -A PROJECTS=(
  [integration]="ATAP.IntegrationTests/ATAP.IntegrationTests.csproj"
  [e2e]="ATAP.E2ETests/ATAP.E2ETests.csproj"
)
UNIT_PROJECTS=("ATAP.BusinessLogic.Tests/ATAP.BusinessLogic.Tests.csproj" "ATAP.DataAccess.Tests/ATAP.DataAccess.Tests.csproj")

log() { echo "[run-tests] $*" >&2; }

# Выкачивает репозиторий и собирает решение один раз на весь прогон (рабочий том переиспользуется этапами)
prepare() {
  if [ ! -d "$WORK/.git" ]; then
    log "Выкачиваю репозиторий $REPO_URL"
    git clone --quiet "$REPO_URL" "$WORK"
  fi
  cd "$WORK"
  git checkout --quiet "$COMMIT"
  log "Коммит: $(git log -1 --format='%h %s')"
  if [ ! -f .built ] || [ "$(cat .built)" != "$(git rev-parse HEAD)" ]; then
    log "Собираю тестовые проекты"
    for project in "${UNIT_PROJECTS[@]}" "${PROJECTS[integration]}" "${PROJECTS[e2e]}"; do
      dotnet build "$project" --configuration Release --nologo -v quiet
    done
    # Тестовые проекты e2e и integration зависят от API только по HTTP; сам API собирается в отдельном образе
    git rev-parse HEAD > .built
  fi
}

# Забирает Allure-результаты из каталогов сборки и убирает их, чтобы следующий этап не дублировал записи
collect_allure() {
  local dir
  while IFS= read -r dir; do
    cp -r "$dir"/. "$ALLURE_OUT"/ 2>/dev/null || true
    rm -rf "$dir"
  done < <(find "$WORK" -type d -name allure-results -path "*/bin/*" 2>/dev/null)
}

run_project() {
  local stage="$1" project="$2"
  dotnet test "$project" --configuration Release --no-build --nologo \
    --logger "trx;LogFileName=${stage}-$(basename "$project" .csproj).trx" \
    --results-directory "$RESULTS_DIR/trx" \
    --logger "console;verbosity=normal"
}

# Allure-результаты пропущенных тестов: этап не запускался, потому что предыдущий этап упал
write_skipped() {
  local stage="$1" full_name="$2" now suffix uuid
  now=$(( $(date +%s) * 1000 ))
  uuid="$(cat /proc/sys/kernel/random/uuid)"
  suffix="$(printf '%s' "$full_name" | md5sum | cut -d' ' -f1)"
  cat > "$ALLURE_OUT/${uuid}-result.json" <<JSON
{"uuid":"${uuid}","historyId":"${suffix}","name":"${full_name##*.}","fullName":"${full_name}","status":"skipped","stage":"finished","start":${now},"stop":${now},"statusDetails":{"message":"Этап ${stage} не запускался: предыдущий этап завершился с ошибкой"},"labels":[{"name":"framework","value":"xunit"},{"name":"suite","value":"${stage}"},{"name":"parentSuite","value":"CI"}]}
JSON
}

mark_skipped() {
  local stage="$1" project names count=0
  if [ "$stage" = "unit" ]; then
    project="${UNIT_PROJECTS[*]}"
  else
    project="${PROJECTS[$stage]:?неизвестный этап $stage}"
  fi
  # Перечень тестов получаем из собранных сборок; если сборки нет (упал этап сборки), пишем один условный пропуск
  if prepare 2>/dev/null; then
    for item in $project; do
      names="$(dotnet test "$item" --configuration Release --no-build --list-tests --nologo 2>/dev/null | sed -n 's/^    //p' || true)"
      while IFS= read -r name; do
        [ -n "$name" ] || continue
        write_skipped "$stage" "$(basename "$item" .csproj).${name}"; count=$((count + 1))
      done <<< "$names"
    done
  fi
  if [ "$count" -eq 0 ]; then
    write_skipped "$stage" "ATAP.${stage}.NotExecuted"
    count=1
  fi
  echo "skipped" > "$RESULTS_DIR/status/$stage"
  log "Этап $stage помечен как пропущенный ($count тестов)"
}

case "$MODE" in
  unit)
    prepare
    code=0
    for project in "${UNIT_PROJECTS[@]}"; do run_project unit "$project" || code=$?; done
    collect_allure
    echo "$code" > "$RESULTS_DIR/status/unit"
    exit "$code"
    ;;
  integration)
    : "${ATAP_TEST_PG:?не задан ATAP_TEST_PG}" "${ATAP_TEST_REDIS:?не задан ATAP_TEST_REDIS}"
    prepare
    code=0
    run_project integration "${PROJECTS[integration]}" || code=$?
    collect_allure
    echo "$code" > "$RESULTS_DIR/status/integration"
    exit "$code"
    ;;
  e2e)
    : "${ATAP_TEST_API:?не задан ATAP_TEST_API}"
    prepare
    code=0
    run_project e2e "${PROJECTS[e2e]}" || code=$?
    collect_allure
    echo "$code" > "$RESULTS_DIR/status/e2e"
    exit "$code"
    ;;
  skip)
    mark_skipped "${2:?укажите этап}"
    ;;
  *)
    echo "Неизвестный этап: $MODE" >&2
    exit 2
    ;;
esac
