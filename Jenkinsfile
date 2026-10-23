// Конвейер тестов ATAP: unit → integration → e2e (WebLab «Тестирование», требования 5, 8, 9, 10, 12).
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
        disableConcurrentBuilds()
        buildDiscarder(logRotator(numToKeepStr: '20'))
    }

    parameters {
        // Только для демонстрации: этап завершается с ошибкой после прогона тестов — следующие этапы
        // не запускаются, в отчёте они помечены как skipped, стенд всё равно удаляется
        choice(name: 'SIMULATE_FAILURE', choices: ['none', 'integration', 'e2e'],
               description: 'Имитировать падение этапа (проверка требований 8 и 10)')
    }

    stages {
        stage('Образ с тестами') {
            steps { sh 'bash ci/prepare.sh' }
        }

        stage('Unit') {
            steps { sh 'bash ci/stage-run.sh unit' }
        }

        stage('Стенд') {
            steps { sh 'bash ci/stand-up.sh' }
        }

        stage('Integration') {
            steps { sh 'bash ci/stage-run.sh integration' }
        }

        stage('E2E') {
            steps { sh 'bash ci/stage-run.sh e2e' }
        }
    }

    post {
        always {
            sh 'bash ci/finish.sh'
            archiveArtifacts artifacts: 'ci-results/**', allowEmptyArchive: true
        }
    }
}
