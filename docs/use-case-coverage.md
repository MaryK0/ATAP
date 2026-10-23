# Покрытие вариантов использования тестами

Источник вариантов использования — диаграмма [`web_lr1/diagrams/use_case.puml`](../web_lr1/diagrams/use_case.puml)
([`README.md` §4](../README.md#4-use-case-диаграмма)). Для каждого варианта указаны тесты трёх уровней:

| Уровень | Проект | Что проверяет | Запуск |
|---|---|---|---|
| **unit** | `ATAP.BusinessLogic.Tests`, `ATAP.DataAccess.Tests` | сервисы и SQL-запросы без БД | `make test-offline` |
| **integration** | `ATAP.IntegrationTests` | сервисы и репозитории на реальных PostgreSQL и Redis | `make test-integration` |
| **E2E** | `ATAP.E2ETests` | демонстрационный сценарий по HTTP на стенде | `make test-e2e` |

Показать тесты одного варианта использования: подставьте фильтр из последнего столбца.

```bash
dotnet test ATAP.BusinessLogic.Tests --filter "FullyQualifiedName~AuthService"                              # unit
scripts/test-stand.sh with dotnet test ATAP.IntegrationTests --filter "FullyQualifiedName~AuthService"     # integration
scripts/test-stand.sh with dotnet test ATAP.E2ETests                                                       # E2E
```

Проверка, что все перечисленные тесты существуют: `bash scripts/check-usecase-coverage.sh`.

Условные обозначения: ✅ покрыт; ⚠️ покрыт частично (причина указана); ⛔ вне границ системы.

## Варианты использования с диаграммы

| № | Вариант использования (актор) | Требование | Unit | Integration | E2E | Статус | Фильтр |
|---|---|---|---|---|---|---|---|
| 1 | **Регистрация** → «Ввод почты и пароля, создание логина» (гость) | FR-01 | `AuthServiceTests.RegisterAsync_WithNewUser_ShouldSucceed`<br>`AuthServiceTests.RegisterAsync_WithExistingUsername_ShouldFail`<br>`AuthServiceTests.RegisterAsync_WithExistingEmail_ShouldFail`<br>`AuthServiceMoqTests.RegisterAsync_WithNewUser_ShouldCallCreateAsyncWithHashedPasswordAndAnalystRole`<br>`AuthServiceMoqTests.RegisterAsync_WithExistingUsername_ShouldNeverCallCreateAsync` | `AuthService_RegisterThenLogin_AuthenticatesNewUser_IntegrationTest`<br>`AuthService_Register_DuplicateUsername_ReturnsFalse_IntegrationTest`<br>`UserRepository_Create_PersistsUser_IntegrationTest`<br>`UserRepository_Create_DuplicateUsername_Throws_IntegrationTest` | — (регистрации нет в срезе API) | ⚠️ проверка пустых полей формы (`RegisterViewModel`) тестами не покрыта | `RegisterAsync` / `AuthService` |
| 2 | **Авторизация** → «Ввод логина и пароля» (гость) | FR-02 | `AuthServiceTests.LoginAsync_WithValidCredentials_ShouldSucceed`<br>`AuthServiceTests.LoginAsync_WithInvalidPassword_ShouldFail`<br>`AuthServiceTests.LoginAsync_WithNonExistentUser_ShouldFail`<br>`AuthServiceTests.GetCurrentUserAsync_AfterLogin_ShouldReturnUser`<br>`AuthServiceMoqTests.LoginAsync_WithNonExistentUser_ShouldOnlyQueryRepositoryOnce` | `AuthService_RegisterThenLogin_AuthenticatesNewUser_IntegrationTest`<br>`AuthService_Login_WrongPassword_ReturnsNull_IntegrationTest`<br>`UserRepository_GetByUsername_ReturnsSeededAdmin_IntegrationTest`<br>`UserRepository_GetByUsername_UnknownUser_ReturnsNull_IntegrationTest` | `DemoScenario_AnalystAnalyzesCongestionAndSavesReport_E2ETest` (шаги 3–4: неверный и верный вход) | ✅ | `LoginAsync` / `AuthService` |
| 3 | **Задать фильтры на карте** (аналитик, админ) | FR-04, FR-05, FR-06 | `MapServiceTests.GetCurrentFlightsAsync_ShouldReturnFlights`<br>`MapServiceTests.GetAirportsAsync_ShouldReturnAirports`<br>`MapServiceTests.GetAirportsAsync_WithCache_ShouldReturnFromCache`<br>`MapServiceMoqTests.GetAirportsAsync_WhenCacheHit_ShouldNeverQueryRepository` | `MapRepository_GetCurrentFlights_ReturnsFreshPositions_IntegrationTest`<br>`MapRepository_GetAirports_ReadsDatabaseThenCache_IntegrationTest`<br>`MapService_GetCurrentFlights_ReturnsFreshFlights_IntegrationTest`<br>`MapService_GetAirports_SecondCallServedFromRedis_IntegrationTest` | `DemoScenario_AnalystAnalyzesCongestionAndSavesReport_E2ETest` (шаг 1: карта, счётчик рейсов, `bbox`) | ⚠️ **фильтры карты (FR-06: видимая область, высота, страна) в приложении не реализованы** — проверена сама карта и область `bbox` на уровне API; фильтров по высоте и стране нет | `MapService` / `MapRepository` |
| 4 | **Задать фильтры в витрине** (аналитик, админ) → период и аэропорт | FR-07, FR-08, FR-09 | `AnalyticsServiceTests.GetCongestionAsync_ShouldReturnReport`<br>`AnalyticsServiceTests.GetCongestionAsync_WhenStartDateAfterEndDate_ShouldThrowException`<br>`AnalyticsServiceTests.GetCongestionAsync_WhenStartEqualsEnd_ShouldSucceed`<br>`AnalyticsServiceTests.GetCongestionAsync_WithEmptyData_ShouldReturnZeroCounts`<br>`AnalyticsServiceTests.GetCongestionAsync_WithCacheHit_ShouldReturnCachedReportWithoutTouchingRepository`<br>`SqlQueryFactoryTests` (запросы витрины) | `AnalyticsRepository_GetCongestion_HalfOpenPeriod_IntegrationTest`<br>`AnalyticsRepository_GetCongestion_FiltersByAirport_IntegrationTest`<br>`AnalyticsRepository_GetCongestion_AirportWithoutData_ReturnsEmpty_IntegrationTest`<br>`AnalyticsRepository_GetCongestion_WithoutDetails_IntegrationTest`<br>`AnalyticsRepository_GetAllCongestion_EntryAndExitInsidePeriod_IntegrationTest`<br>`AnalyticsService_GetCongestion_CountsDeparturesAndArrivals_IntegrationTest`<br>`AnalyticsService_GetCongestion_SecondCallServedFromRedis_IntegrationTest` | `DemoScenario_AnalystAnalyzesCongestionAndSavesReport_E2ETest` (шаг 5 — витрина и счётчики; шаг 7 — ошибки периода и аэропорта) | ✅ | `Analytics` |
| 5 | **Выгрузить CSV-отчёт** (из «Задать фильтры в витрине») | FR-10 | `AnalyticsServiceTests.ExportToCsvAsync_WithEntries_ShouldSerializeEachEntryAsCsvRow`<br>`AnalyticsServiceTests.ExportToCsvAsync_WithEmptyEntries_ShouldReturnOnlyHeaderRow`<br>`AnalyticsServiceTests.ExportToCsvAsync_WithNullReport_ShouldThrowException` | `AnalyticsService_ExportToCsv_RowCountEqualsEntries_IntegrationTest` | `DemoScenario_AnalystAnalyzesCongestionAndSavesReport_E2ETest` (шаг 6: строк CSV столько же, сколько записей) | ✅ | `ExportToCsv` |
| 6 | **Выйти из аккаунта** (аналитик, админ) | FR-03 | `AuthServiceTests.LogoutAsync_AfterLogin_ShouldClearCurrentSession`<br>`AuthServiceTests.LogoutAsync_WithoutActiveSession_ShouldNotThrow`<br>`AuthServiceTests.GetCurrentUserAsync_WithoutLogin_ShouldReturnNull` | `AuthService_Logout_ClearsCurrentUser_IntegrationTest` | `DemoScenario_AnalystAnalyzesCongestionAndSavesReport_E2ETest` (шаг 11: токен после выхода отклоняется) | ✅ | `Logout` |
| 7 | **Открыть таблицу с пользователями** (админ) | FR-13 | `AdminServiceTests.GetAllUsersAsync_WithSeededUsers_ShouldReturnMappedDtos`<br>`AdminServiceTests.GetAllUsersAsync_WithNoUsers_ShouldReturnEmptyList` | `UserRepository_GetAll_ReturnsSeededUsers_IntegrationTest` | — (операция `listUsers` вне среза API) | ✅ | `GetAllUsers` |
| 8 | **Редактировать пользователя** (админ) | FR-15 | `AdminServiceTests.UpdateUserAsync_WithExistingUser_ShouldPersistChangesAndReturnTrue`<br>`AdminServiceTests.UpdateUserAsync_WithNewPassword_ShouldStoreDifferentHashThanPlainPassword`<br>`AdminServiceTests.UpdateUserAsync_WithNonExistentUser_ShouldReturnFalse` | `AdminService_UpdateUser_ChangesRoleAndPassword_IntegrationTest`<br>`UserRepository_Update_ChangesRoleAndEmail_IntegrationTest` | — | ✅ | `UpdateUser` |
| 9 | **Удалить пользователя** (админ) | FR-16 | `AdminServiceTests.DeleteUserAsync_WithExistingUser_ShouldRemoveAndReturnTrue`<br>`AdminServiceTests.DeleteUserAsync_WithNonExistentUser_ShouldReturnFalse` | `AdminService_DeleteUser_RemovesUserWithReports_IntegrationTest`<br>`UserRepository_Delete_CascadesSavedReports_IntegrationTest`<br>`UserRepository_Delete_UnknownUser_ReturnsFalse_IntegrationTest` | — | ⚠️ **правило «нельзя удалить `admin` и себя» в сервисе не реализовано** (оно только в интерфейсах), поэтому проверить его тестом сервиса нельзя; найдено при подготовке тестов (AI_REVIEW, T-2) | `DeleteUser` |
| 10 | **Добавить пользователя** (админ) | FR-14 | `AuthServiceTests.RegisterAsync_WithNewUser_ShouldSucceed`<br>`AdminServiceTests.UpdateUserAsync_WithExistingUser_ShouldPersistChangesAndReturnTrue` | `AdminService_UpdateUser_ChangesRoleAndPassword_IntegrationTest` (регистрация, затем назначение роли `admin`) | — | ⚠️ отдельного метода «добавить пользователя» в сервисе нет: интерфейс создаёт пользователя регистрацией и затем меняет роль — проверены оба шага | `RegisterAsync` / `UpdateUser` |
| 11 | **Авторизация в Airflow** (админ) | FR-17 | — | — | — | ⛔ вне границ ATAP: вход в родной интерфейс Airflow (`README.md` §3, `AGENTS.md` §3) | — |
| 12 | **Управлять параметрами Airflow** (админ) | FR-17 | — | — | — | ⛔ вне границ ATAP | — |
| 13 | **Перезапустить DAG** (админ) | FR-17 | — | — | — | ⛔ вне границ ATAP; DAG спроектированы, в этом репозитории их нет | — |

## Что есть в системе, но отсутствует на диаграмме

На диаграмме нет вариантов использования «Сохранить отчёт» и «Мои отчёты» (FR-11, FR-12), хотя они в приложении реализованы
(`README.md` §6, сценарий 3). Они покрыты тестами:

| Вариант использования | Unit | Integration | E2E |
|---|---|---|---|
| Сохранить отчёт (аналитик) | `SavedReportServiceTests.SaveAsync_WithValidDto_ShouldPersistReportForUser`<br>`SavedReportServiceTests.SaveAsync_WithBlankName_ShouldThrowArgumentException`<br>`SavedReportServiceTests.SaveAsync_WithInvertedPeriod_ShouldThrowArgumentException`<br>`SavedReportServiceTests.SaveAsync_WithSingleDayPeriod_ShouldSucceed` | `SavedReportService_Save_ThenGetByUser_IntegrationTest`<br>`SavedReportService_Save_InvalidInput_ThrowsAndStoresNothing_IntegrationTest`<br>`SavedReportRepository_Create_ThenGetByUser_ReturnsReportWithAirportName_IntegrationTest`<br>`SavedReportRepository_Create_InvertedPeriod_ViolatesCheck_IntegrationTest` | `DemoScenario_AnalystAnalyzesCongestionAndSavesReport_E2ETest` (шаг 8) |
| Мои отчёты: список и удаление (аналитик) | `SavedReportServiceTests.GetByUserAsync_WithSavedReports_ShouldReturnOnlyThatUsersReports`<br>`SavedReportServiceTests.DeleteAsync_WithOwnedReport_ShouldRemoveIt`<br>`SavedReportServiceTests.DeleteAsync_WithForeignReportId_ShouldNotRemoveAnything` | `SavedReportRepository_GetByUser_ReturnsOnlyOwnReports_IntegrationTest`<br>`SavedReportRepository_Delete_Owner_RemovesReport_IntegrationTest`<br>`SavedReportRepository_Delete_ForeignUser_KeepsReport_IntegrationTest` | `DemoScenario_AnalystAnalyzesCongestionAndSavesReport_E2ETest` (шаги 9–10) |

## Итог

| | Вариантов |
|---|---|
| Всего на диаграмме | 13 |
| ✅ покрыты | 6 (№ 2, 4, 5, 6, 7, 8) |
| ⚠️ покрыты частично (причина указана) | 4 (№ 1, 3, 9, 10) |
| ⛔ вне границ системы | 3 (№ 11–13) |

Три из четырёх «частичных» — это не пробелы в тестах, а расхождения между диаграммой и реализацией: фильтры карты (FR-06) не сделаны,
правило удаления администратора не вынесено в сервис, отдельного метода «добавить пользователя» нет. Четвёртое (№ 1) — реальный
пробел: проверка пустых полей формы регистрации (`RegisterViewModel`) тестами не покрыта.
