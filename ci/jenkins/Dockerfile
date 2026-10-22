# Jenkins для запуска тестов ATAP. Сборка из корня репозитория:
#   docker build -f ci/jenkins/Dockerfile -t atap-jenkins:ci .
FROM jenkins/jenkins:lts-jdk17

USER root

# Docker CLI и плагин compose: Jenkins поднимает тестовый стенд и запускает контейнер с тестами
# через сокет Docker хоста (см. ci/docker-compose.jenkins.yml)
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl gnupg unzip bash \
 && install -m 0755 -d /etc/apt/keyrings \
 && curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc \
 && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/debian $(. /etc/os-release && echo $VERSION_CODENAME) stable" \
      > /etc/apt/sources.list.d/docker.list \
 && apt-get update \
 && apt-get install -y --no-install-recommends docker-ce-cli docker-compose-plugin \
 && rm -rf /var/lib/apt/lists/*

# Allure CLI: из результатов тестов строится отчёт с историей запусков (тренды)
ARG ALLURE_VERSION=2.32.0
RUN curl -fsSL -o /tmp/allure.zip \
      "https://github.com/allure-framework/allure2/releases/download/${ALLURE_VERSION}/allure-${ALLURE_VERSION}.zip" \
 && unzip -q /tmp/allure.zip -d /opt \
 && ln -s "/opt/allure-${ALLURE_VERSION}/bin/allure" /usr/local/bin/allure \
 && rm /tmp/allure.zip \
 && git config --system --add safe.directory '*'

# Плагины: Pipeline (Jenkinsfile) и Git
COPY ci/jenkins/plugins.txt /usr/share/jenkins/ref/plugins.txt
RUN jenkins-plugin-cli --plugin-file /usr/share/jenkins/ref/plugins.txt

# Без мастера первоначальной настройки (локальный учебный стенд, порт открыт только на localhost);
# CSP снят, чтобы отчёт Allure (HTML с JavaScript) открывался из артефактов сборки
ENV JAVA_OPTS="-Djenkins.install.runSetupWizard=false -Dhudson.model.DirectoryBrowserSupport.CSP="

# Задание atap-tests создаётся автоматически: Pipeline из Jenkinsfile репозитория (file:///repo)
COPY ci/jenkins/jobs/ /usr/share/jenkins/ref/jobs/
