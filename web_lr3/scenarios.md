# WebLab#3 — сценарии и изменения контракта

Источник истины — `web_lr2/openapi.yaml`, единственный API `/api/v1`.
По решению автора черновая попытка ЛР3 заменена полной реализацией без отдельной v2.
Вход/выход — POST login/logout, подписанный JWT в Authorization Bearer.

| Сценарий | Операции |
|---|---|
| Регистрация | createUser → login |
| Карта | listAirports → listCurrentFlights; фильтры и ETag/304 |
| Аналитика | login → listAirports → getCongestion JSON/CSV |
| Сохранённые отчёты | createReport → listReports → getReport → getReportCongestion → deleteReport |
| Администрирование | login admin → listUsers → createUser → getUser → replaceUser → updateUser → deleteUser |
| Внешняя интеграция | listAircraft/getAircraft → listFlights/getFlight → listFlightPositions |
| Фоновая выгрузка | createExport (202, Location, Retry-After) → getExport до completed → downloadExport (CSV) |
| Выход | logout → защищённый запрос с прежним токеном получает 401 |

Состояние задания проверяется polling-запросами. 202 означает «принято», а не «готово».
При failed клиент получает состояние с описанием ошибки, файл не доступен.
Задания других пользователей скрыты как 404. Сохранённый отчёт хранит параметры,
задание выгрузки хранит состояние и конкретный сформированный CSV.

Три слоя моделей: DataAccess/Models → BusinessLogic/Domain → Api/Dtos.
SQL не входит в контроллеры; домен не зависит от HTTP или Npgsql.
ConsoleUI переводится на HTTP; Blazor Server сохраняется.
