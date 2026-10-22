# Образ тестовой БД ATAP: PostgreSQL + PostGIS с готовой структурой и тестовыми данными.
# Образ собирается один раз и при каждом запуске даёт одинаковое состояние.
# Сборка из корня репозитория:  docker build -f db/test/Dockerfile -t atap-test-db .
#
# Скрипты из /docker-entrypoint-initdb.d выполняются при первом старте контейнера в порядке имён,
# в базе POSTGRES_DB (atap_db). Эта база остаётся нетронутой и служит шаблоном: каждый тестовый
# класс создаёт свою копию командой CREATE DATABASE ... TEMPLATE atap_db и удаляет её по окончании.
FROM postgis/postgis:15-3.4

ENV POSTGRES_USER=postgres \
    POSTGRES_PASSWORD=test_pass \
    POSTGRES_DB=atap_db

COPY db/create_tables.sql        /docker-entrypoint-initdb.d/01_create_tables.sql
COPY db/test/03_seed_test_data.sql /docker-entrypoint-initdb.d/03_seed_test_data.sql

# Тестовой БД не нужны данные между перезапусками: хранилище — анонимный том контейнера,
# при удалении контейнера (docker rm -v) состояние исчезает.
# Проверка идёт по TCP (127.0.0.1): во время выполнения скриптов инициализации временный сервер
# слушает только сокет, поэтому по TCP база «готова» лишь после окончания всех скриптов.
HEALTHCHECK --interval=2s --timeout=3s --retries=90 \
  CMD pg_isready -h 127.0.0.1 -U postgres -d atap_db || exit 1
