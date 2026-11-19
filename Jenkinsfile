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
        choice(name: 'FAULT_TARGET', choices: ['none', 'postgres', 'api'], description: 'Остановить компонент только тестового стенда перед соответствующим этапом')
        booleanParam(name: 'PARALLEL_DEMO', defaultValue: false, description: 'Пять независимых разработчиков: integration и E2E параллельно')
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
            when { expression { !params.PARALLEL_DEMO } }
            steps {
                sh 'bash ci/fault.sh integration'
                sh 'bash ci/stage-run.sh integration'
            }
        }

        stage('E2E') {
            when { expression { !params.PARALLEL_DEMO } }
            steps {
                sh 'bash ci/fault.sh e2e'
                sh 'bash ci/stage-run.sh e2e'
            }
        }

        stage('Пять разработчиков') {
            when { expression { params.PARALLEL_DEMO } }
            steps {
                script {
                    if (params.FAULT_TARGET != 'none') error('Параллельный показ и остановку компонентов запускайте отдельно')
                    def developers = [:]
                    for (int i = 1; i <= 5; i++) {
                        def developer = "dev-${i}"
                        developers[developer] = { sh "bash ci/parallel-run.sh ${developer}" }
                    }
                    parallel developers
                }
            }
        }
    }

    post {
        always {
            sh 'bash ci/finish.sh'
            archiveArtifacts artifacts: 'ci-results/**', allowEmptyArchive: true
        }
    }
}
