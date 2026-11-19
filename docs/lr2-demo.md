# Демонстрация ЛР №2 по тестированию

## Запуск

Нужны Docker Desktop (Linux-контейнеры), Git Bash и Python 3.
Команды выполняются из корня репозитория. Код должен быть закоммичен:
Jenkins клонирует Git и проверяет конкретный коммит, а не рабочую папку.

```bash
bash ci/jenkins.sh up
python -u ci/demo.py build
```

Jenkins: http://localhost:8085/job/atap-tests/.
В конце команды печатаются номер сборки, результат и ссылка на Allure.
Консоль, JSON сборки, сводка Allure и тренд сохраняются в
`TestResults/lr2/jenkins-<номер>/` (локально, не в Git).

## Что показать по заданию

| Пункт | Файл / демонстрация |
|---|---|
| Стенд | `docker-compose.test.yml`: отдельные PostgreSQL, Redis, API и разовый db-e2e |
| Инициализация | `db/test/Dockerfile`, `db/create_tables.sql`, `db/test/03_seed_test_data.sql` |
| Контейнер тестов | `ci/Dockerfile.tests`, `ci/run-tests.sh`: clone, checkout, build, test |
| Integration | `ATAP.IntegrationTests/DataAccess/UserRepository_Create_PersistsUser_IntegrationTest.cs`, `ATAP.IntegrationTests/BusinessLogic/AnalyticsService_GetCongestion_CountsDeparturesAndArrivals_IntegrationTest.cs` |
| Изоляция | `ATAP.IntegrationTests/Infrastructure/TestDatabaseFixture.cs`, `TestRedisFixture.cs` |
| E2E | `ATAP.E2ETests/DemoScenario_AnalystAnalyzesCongestionAndSavesReport_E2ETest.cs` |
| Порядок тестов | `Jenkinsfile`: unit → integration → e2e |
| Отчёт при ошибке | `post { always }`, `ci/finish.sh`, `ci/run-tests.sh` (режим skip) |
| Тренды | Allure → Overview → Trend; `ci/finish.sh` переносит history между сборками |
| Трафик | `scripts/e2e-curl.sh`, `scripts/capture-traffic.sh`, `scripts/traffic-addon.py` |

Сказать: «Тесты работают с отдельным стендом. Интеграционные тесты проверяют
репозитории и сервисы с реальным хранилищем. E2E проходит сценарий пользователя
через HTTP API; GUI по заданию включать не требуется».

## Воспроизведение E2E и захват HTTP

```bash
bash scripts/capture-traffic.sh
```

Создаётся отдельный стенд, перед API запускается mitmproxy и выполняются
те же 27 запросов, что в E2E: карта и фильтры → отказы доступа → вход →
аналитика JSON/CSV и ошибки → создание/чтение/удаление отчёта → выход.
Curl сверяет коды ответов; автоматический E2E дополнительно проверяет тела,
заголовки и конкретные значения тестовых данных.

Файлы: `TestResults/lr2/traffic/e2e-traffic.log` и `e2e.flow`.
Flow открывается через `mitmproxy -r <путь>` или `mitmweb -r <путь>`.
В сохраняемых копиях скрываются пароли, accessToken, Authorization и cookie;
живые запросы и ответы не изменяются. Это захват реального HTTP средством
mitmproxy, а не pcap для Wireshark. Стенд и прокси удаляются через trap.

## Реальный отказ PostgreSQL

```bash
python -u ci/demo.py build --fault postgres --expect-failure
```

Unit проходит; после запуска стенда `ci/fault.sh` останавливает только
postgres-test этой сборки. Integration завершается с ошибками, E2E пропускается.
В Allure должны быть ошибки и skipped; в Jenkins сборка FAILURE.
`--expect-failure` означает, что управляющий скрипт ожидает такой исход,
но не превращает результат Jenkins в SUCCESS.

## Реальный отказ API

```bash
python -u ci/demo.py build --fault api --expect-failure
```

Unit и integration проходят; перед E2E останавливается atap-api текущей сборки.
E2E падает на реальном HTTP-запросе. Allure формируется, стенд удаляется.
Рабочие контейнеры приложения и WebLab#4 не останавливаются.

## Пять разработчиков параллельно

```bash
python -u ci/demo.py parallel
```

Это один Jenkins build: unit выполняется один раз, затем пять параллельных
веток dev-1…dev-5. У каждой свой Compose-проект, PostgreSQL, Redis, API,
свободные порты, тома результатов и копия собранного кода одного коммита.
После готовности всех пяти стендов одновременно запускаются integration,
затем E2E внутри каждой ветки, затем curl через собственный mitmproxy.

В Console Output найти `[developer]`: имя разработчика, проект, URL API,
время начала/конца. В артефактах каждой ветки:
`ci-results/developers/dev-N/developer.txt`, `api.log`, `e2e-traffic.log`,
`e2e.flow`, `developer.pcap`, `status/`, `trx/`, `allure-results/`.
В HTTP-журнале есть `developer=dev-N`, в запросе — `X-ATAP-Developer`.
Allure различает одинаковые тесты по параметру developer и historyId.

Для Wireshark скачать `developer.pcap` из артефактов нужной ветки.
В нём один гостевой запрос к API (запись остановлена до логина).
Для порта 8888 выбрать Decode As → HTTP, затем фильтр `http.request`:
в заголовках виден `X-ATAP-Developer: dev-N`. Полный сценарий хранится
в redacted-формате mitmproxy, отдельно от этого короткого pcap.

Обычные сборки задания по-прежнему последовательны (`disableConcurrentBuilds`),
поэтому запись общей истории Allure не конфликтует. Параллелизм выполняется
внутри специального режима, а не пятью независимыми сборками одного задания.

## Очистка и финальный успешный прогон

`ci/finish.sh` выполняется при успешном и неуспешном завершении. Удаляются
тестовые контейнеры, сети и временные тома; активные сессии API исчезают
с его экземпляром. Постоянные рабочие БД и история Jenkins сохраняются.

После показов отказов снова выполнить:

```bash
python -u ci/demo.py build
```

В Allure останутся и неуспешные, и успешные точки истории. Точные номера
проверенных сборок и результаты — ниже, после фактического прогона.
