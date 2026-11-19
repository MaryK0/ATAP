# WebLab#4 — веб-сервер NGINX

NGINX — единая точка входа в ATAP: проксирует API, Blazor и pgAdmin,
отдаёт статические страницы и документацию. Актуальный контракт —
[`web_lr2/openapi.yaml`](../web_lr2/openapi.yaml), версия API — `/api/v1`.

## Запуск и проверка

Нужны работающий Docker Desktop с Linux-контейнерами, Docker Compose v2
с поддержкой `up --wait` и Python 3.9+. Команды — из корня `C:\ATAP`.
.NET SDK и Node.js на хосте для этого стенда не требуются.

Запуск, включая первый запуск без локальных настроек:

```powershell
python web_lr4/start.py
```

Скрипт создаёт отсутствующие локальные секреты, собирает образы, запускает
контейнеры и ждёт готовности. При повторном запуске сохраняет настройки
и данные. На первом запуске нужен доступ к реестрам образов, NuGet и PyPI.
Открыть **http://localhost:8088/management**.

Все HTTP-проверки одной командой:

```powershell
python -u web_lr4/check-routes.py
```

Код выхода 0 означает успех; нарушение ожиданий останавливает скрипт
с ненулевым кодом и сообщением. Сейчас выполняются 64 HTTP-проверки.

Если порт занят, при первом запуске: `python web_lr4/start.py --port 8089`.
Для существующего стенда изменить WEB_PORT в локальном `.env`.
Отдельный экземпляр можно запускать из своей копии проекта с другим портом
и аргументом `--project atap-web-other`.

## Состав стенда и файлы

Compose поднимает шесть контейнеров в общей Docker-сети:

| Сервис | Внутренний адрес | Назначение |
|---|---|---|
| nginx | nginx:80 | Маршрутизация, статика, служебные страницы |
| api | api:8090 | REST API, Swagger, фоновая выгрузка CSV |
| blazor | blazor:8080 | Прежний интерфейс под `/legacy` |
| postgres | postgres:5432 | PostgreSQL + PostGIS, база atap_db |
| redis | redis:6379 | Кэш |
| pgadmin | pgadmin:80 | Администрирование PostgreSQL |

Снаружи опубликован только NGINX: `127.0.0.1:8088` (либо WEB_PORT).
Остальные сервисы не публикуют порты хоста. У этого развёртывания свои
постоянные тома; рабочая БД на 5433 и базы интеграционных тестов не используются.

| Файл | Для чего |
|---|---|
| `compose.yaml` | Контейнеры, сеть, зависимости, тома и порты |
| `nginx.conf` | Правила маршрутизации и ограничения доступа |
| `Dockerfile` | Образ NGINX с подготовленными страницами и картинками |
| `start.py` | Единственная команда запуска; создаёт настройки при первом старте |
| `check-routes.py` | Проверки реальных HTTP-ответов через NGINX |
| `render-docs.py` | Внутренний скрипт сборки: README → HTML; вручную не запускается |
| `static/index.html`, `static/site.css` | Приветственная страница и стили |
| `management.html` | Страница навигации |
| `pgadmin-servers.json` | Подключение pgAdmin к БД приложения, без пароля |

Эскизы и диаграммы берутся из `web_lr1/diagrams/` при сборке образа.
API и Blazor собираются по Dockerfile в папках соответствующих проектов.

## Настройки и вход

`start.py` создаёт локальные `.env`, `secrets/htpasswd` и `secrets/access.txt`.
Они исключены из Git и Docker build context. В `access.txt` — данные для входа,
в `htpasswd` — bcrypt-хеш пароля защиты NGINX. Частично созданная конфигурация
не перезаписывается: скрипт сообщает о недостающих файлах.

| Переменная `.env` | Назначение |
|---|---|
| WEB_PORT | Внешний порт NGINX, по умолчанию 8088 |
| DB_USER, DB_PASSWORD | Подключение API, Blazor и pgAdmin к PostgreSQL |
| NGINX_USER | Логин защиты `/admin` и `/status` |
| JWT_KEY | Случайный ключ подписи JWT, минимум 32 байта |
| PGADMIN_EMAIL, PGADMIN_PASSWORD | Учётная запись pgAdmin |

На текущем локальном стенде логин NGINX, PostgreSQL и ATAP — `admin`,
pgAdmin — `admin@example.com`. Актуальные пароли находятся только в
`secrets/access.txt`. При создании нового стенда секреты генерируются заново.

Для `/status` нужен только вход NGINX. Для `/admin/`: вход NGINX → вход
в pgAdmin → подключение к серверу **ATAP PostgreSQL**. Хост БД — `postgres`,
порт — 5432, база — `atap_db`, пользователь — `admin`; пароль — из access.txt.
Смена значений `.env` сама по себе не меняет пароли уже созданных аккаунтов.

## Таблица маршрутов

| Внешний путь | Целевой компонент | Префикс / обработка | Ограничения доступа |
|---|---|---|---|
| `/api/v1/...` | API, api:8090 | Путь сохраняется; документация обрабатывается отдельным правилом | Публичные операции либо JWT и роли по OpenAPI |
| `/api/v1/docs` | NGINX | 308 на `/api/v1/docs/` | Публично |
| `/api/v1/docs/` и ресурсы | Swagger UI в API | Внутренний путь `/swagger/`; внешний префикс сохраняется | UI публичен, Execute подчиняется правам API |
| `/api/v1/docs/openapi.yaml` | API | Внутренний `/openapi/openapi.yaml` | Публичное чтение |
| `/legacy` | NGINX | 308 на `/legacy/` | Публично |
| `/legacy/...` | Blazor, blazor:8080 | PathBase `/legacy`, включая ресурсы и SignalR/WebSocket | Правила доступа существующего интерфейса |
| `/documentation` | HTML из README | Файл documentation.html | Публично |
| `/documentation/assets/...` | Диаграммы README | Только разрешённые файлы документации | Публично |
| `/` | static/index.html | Точное совпадение | Публично |
| `/static/...` | Файлы статики | Соответствующий файл или 404 | Публично, без листинга |
| `/reserved/` | static/index.html | Тот же файл, что для `/`, без смены адреса | Публично |
| `/reserved` | NGINX | 308 на `/reserved/` | Публично |
| `/admin` | NGINX | После проверки пароля 308 на `/admin/` | Basic-аутентификация |
| `/admin/...` | pgAdmin, pgadmin:80 | Префикс `/admin` учитывается через SCRIPT_NAME | Basic-вход NGINX, затем собственный вход pgAdmin |
| `/status` | NGINX stub_status | Счётчики соединений и запросов | Basic-аутентификация |
| `/health` | NGINX | 200 alive: процесс принимает HTTP-запросы | Минимальный публичный ответ |
| `/ready` | API: /internal/ready | Проверяет PostgreSQL через SELECT 1 и доступность Blazor | Минимальный публичный ответ |
| `/management` | management.html | Ссылки на все обязательные разделы | Ссылки не обходят защиту |

Swagger имеет более длинный префикс, чем API, и не перекрывает его операции.
Путь `/legacy/admin` относится к Blazor, `/admin/` — к админке БД.
Неизвестные пути и похожие префиксы (`/api/v10`, `/legacy-other`) дают 404.
`absolute_redirect off` сохраняет внешний порт при редиректах.

При работающих зависимостях `/ready` возвращает 200 ready, при их сбое —
503 not ready; если недоступен сам API, возможен 502/504 от NGINX.
Отсутствие необязательного Redis само по себе не делает readiness неуспешной.

## Проверки и результаты

`check-routes.py` проверяет через внешний NGINX:

- 200 и содержимое страниц, CSS и всех изображений главной и документации;
- одинаковые байты `/` и `/reserved/`;
- 308 и правильный Location с сохранением внешнего адреса и порта;
- Swagger, его ресурсы и точное совпадение YAML с каноническим контрактом;
- передачу query-параметров API, 401 Problem Details для защищённой операции;
- префиксы Blazor и pgAdmin, negotiate 200 и WebSocket upgrade 101;
- `/health`, `/ready`, обязательные ссылки меню;
- 401 без пароля и с неверным паролем; доступ к pgAdmin и статусу с верным;
- 404 для неизвестных путей, отсутствующей статики и локальных секретов.

64 проверки прошли на рабочем стенде, до и после изменения порта API,
а также в чистой копии без секретов с новыми томами БД. Повторный запуск
сохранил существующие локальные настройки. Кэш образов при проверке не удалялся.
После смены локальных аккаунтов 19.11.2026 повторный прогон прошёл;
в отдельном браузере подтверждён вход в pgAdmin с загрузкой Object Explorer.
После объединения скриптов 19.11.2026 повторно подтверждены запуск с нуля,
64 проверки на новом хранилище и сохранение паролей при обычном запуске.

Набор не проверяет все кнопки Blazor и не имитирует отключение зависимостей.
Привязка входного порта к loopback подтверждается Compose, а не имитацией
запросов с внешнего компьютера. Полные локальные логи находятся в
`TestResults/lr4/` и исключены из Git.

## Проверенное изменение порта

Внутренний адрес API изменён **api:8080 → api:8090**, внешний адрес
`localhost:8088` и пути `/api/v1/...` сохранены. Изменены ASPNETCORE_URLS
сервиса api и пять назначений NGINX: операции, Swagger, OpenAPI и readiness.
В Dockerfile API добавлен аргумент HTTP_PORT; его значение по умолчанию 8080
сохранено для других запусков и тестового стенда.

После проверки diff пересобраны и пересозданы API и NGINX, `nginx -t`
успешен, лог API подтвердил `Now listening on: http://[::]:8090`.
Те же 64 HTTP-проверки прошли без изменения ожидаемых результатов.

<details>
<summary>Фактический diff конфигурации для изменения порта</summary>

```diff
--- before/web_lr4/compose.yaml
+++ after/web_lr4/compose.yaml
@@ -29,8 +29,10 @@
     build:
       context: ..
       dockerfile: ATAP.Api/Dockerfile
+      args:
+        HTTP_PORT: "8090"
     environment:
-      ASPNETCORE_URLS: http://+:8080
+      ASPNETCORE_URLS: http://+:8090
       ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=atap_db;Username=postgres;Password=${DB_PASSWORD:?Set DB_PASSWORD}
       ConnectionStrings__Redis: redis:6379,abortConnect=false,connectTimeout=2000
       Jwt__Key: ${JWT_KEY:?Set JWT_KEY}
--- before/web_lr4/nginx.conf
+++ after/web_lr4/nginx.conf
@@ -21,14 +21,14 @@

     location = /api/v1/docs { return 308 /api/v1/docs/; }
     location = /api/v1/docs/openapi.yaml {
-        proxy_pass http://api:8080/openapi/openapi.yaml;
+        proxy_pass http://api:8090/openapi/openapi.yaml;
     }
     location ^~ /api/v1/docs/ {
-        proxy_pass http://api:8080/swagger/;
+        proxy_pass http://api:8090/swagger/;
         proxy_redirect /swagger/ /api/v1/docs/;
     }
-    location = /api/v1 { proxy_pass http://api:8080; }
-    location /api/v1/ { proxy_pass http://api:8080; }
+    location = /api/v1 { proxy_pass http://api:8090; }
+    location /api/v1/ { proxy_pass http://api:8090; }

     location = /legacy { return 308 /legacy/; }
     location /legacy/ {
@@ -77,6 +77,6 @@
         default_type text/plain;
         return 200 "alive\n";
     }
-    location = /ready { proxy_pass http://api:8080/internal/ready; }
+    location = /ready { proxy_pass http://api:8090/internal/ready; }
     location / { return 404; }
 }
--- before/ATAP.Api/Dockerfile
+++ after/ATAP.Api/Dockerfile
@@ -19,6 +19,7 @@
 COPY --from=build /app .

 # Адрес и подключения задаются переменными окружения (ConnectionStrings__DefaultConnection, ...)
-ENV ASPNETCORE_URLS=http://+:8080
-EXPOSE 8080
+ARG HTTP_PORT=8080
+ENV ASPNETCORE_URLS=http://+:${HTTP_PORT}
+EXPOSE ${HTTP_PORT}
 ENTRYPOINT ["dotnet", "ATAP.Api.dll"]
```

</details>

## Команды для демонстрации и остановки

Проверить синтаксис и состояние:

```powershell
docker compose --env-file web_lr4/.env -f web_lr4/compose.yaml config --quiet
docker compose --env-file web_lr4/.env -f web_lr4/compose.yaml exec nginx nginx -t
docker compose --env-file web_lr4/.env -f web_lr4/compose.yaml ps
```

Наблюдать запросы и применить изменённую конфигурацию NGINX:

```powershell
docker compose --env-file web_lr4/.env -f web_lr4/compose.yaml logs -f --tail 10 nginx api
docker compose --env-file web_lr4/.env -f web_lr4/compose.yaml exec nginx nginx -s reload
```

Ctrl+C останавливает просмотр логов, а не контейнеры. После изменения README
или статики повторить `python web_lr4/start.py`, чтобы пересобрать образ.

Остановить стенд с сохранением данных:

```powershell
docker compose --env-file web_lr4/.env -f web_lr4/compose.yaml down
```

При обычной остановке не добавлять `-v`: этот параметр удаляет тома с данными.

## Передача в WebLab#5 и WebLab#8

Исходное состояние — этот README, compose.yaml, nginx.conf и check-routes.py.
Изменения маршрутов сопровождаются обновлением таблицы и повторным прогоном.
API внутри сети слушает **8090**, Blazor — **8080**.

В WebLab#8 приветственная страница `/` заменяется SPA. Клиент обращается к
относительным `/api/v1/...` того же origin, а не к внутреннему api:8090.
Сохранить документацию, legacy, служебные маршруты, обработку 404 и ограничения
доступа; поведение `/reserved/` согласовать при замене главной страницы.
Blazor использует собственные сервисы, а TUI обращается к REST API.

Реестр активных JWT-сессий находится в памяти API: после его пересоздания
требуется новый вход. Текущий стенд предназначен для локального HTTP-показа;
для внешнего размещения отдельно настраиваются HTTPS и доступ к админке.
