// ЛР2 по тестированию: unit → integration → E2E, отчёт после ошибки и тренд Allure.
//
// Порядок этапов задаётся самим конвейером: этап, упавший с ошибкой, останавливает все следующие
// (Jenkins показывает их как пропущенные). Блок post { always } выполняется в любом случае:
// он помечает непройденные этапы в отчёте Allure как skipped, строит отчёт с историей запусков и
// удаляет тестовый стенд (PostgreSQL, Redis, API) вместе с данными — откат хранилища после прогона.
//
// Тесты запускаются в отдельном контейнере atap-tests (ci/Dockerfile.tests): он выкачивает репозиторий,
// собирает его и выполняет этап. Подробности — TESTING.md.
pipeline {
    agent any

    options {
        // Сборки независимы; число одновременно работающих задают executors Jenkins.
        quietPeriod(0)
        buildDiscarder(logRotator(numToKeepStr: '20'))
    }

    stages {
        stage('Образ с тестами') {
            // prepare.sh собирает Docker-образ с .NET и зависимостями, создаёт временные тома.
            steps { sh 'bash ci/prepare.sh' }
        }

        stage('Unit') {
            // stage-run.sh запускает контейнер тестов; run-tests.sh внутри выполняет unit-тесты без БД.
            steps { sh 'bash ci/stage-run.sh unit' }
        }

        stage('Стенд') {
            // stand-up.sh через test-stand.sh поднимает тестовые PostgreSQL, Redis и API, ждёт готовности.
            steps { sh 'bash ci/stand-up.sh' }
        }

        stage('Integration') {
            steps {
                // stage-run.sh запускает integration-тесты репозиториев и сервисов с реальными хранилищами.
                sh 'bash ci/stage-run.sh integration'
            }
        }

        stage('E2E') {
            steps {
                // stage-run.sh запускает E2E: полный пользовательский сценарий через HTTP API.
                sh 'bash ci/stage-run.sh e2e'
            }
        }
    }

    post {
        always {
            script {
                try {
                    // finish.sh формирует Allure, синхронизирует историю и очищает стенд.
                    sh 'bash ci/finish.sh'
                } finally {
                    // Сохраняем доступные результаты даже при ошибке формирования отчёта.
                    archiveArtifacts artifacts: 'ci-results/**', allowEmptyArchive: true
                }
            }
        }
    }
}
