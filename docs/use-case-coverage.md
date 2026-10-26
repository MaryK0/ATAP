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
| 1 | **Регистрация** → «Ввод почты и пароля, создание логина» (гость) | FR-01 | `AuthServiceTests.RegisterAsync_WithNewUser_ShouldSucceed`<br>`AuthServiceTests.RegisterAsync_WithExistingUsername_ShouldFail`<br>`AuthServiceTests.RegisterAsync_WithExistingEmail_ShouldFail`<br>`AuthServiceMoqTests.RegisterAsync_WithNewUser_ShouldCallCreateAsyncWithHashedPasswordAndAnalystRole`<br>`AuthServiceMoqTests.RegisterAsync_WithExistingUsername_ShouldNeverCallCreateAsync`<br>`RegisterViewModelTests.RegisterAsync_WithEmptyUsername_ShouldShowLoginErrorAndNotCallService`<br>`RegisterViewModelTests.RegisterAsync_WithEmptyEmail_ShouldShowEmailErrorAndNotCallService`<br>`RegisterViewModelTests.RegisterAsync_WithEmptyPassword_ShouldShowPasswordErrorAndNotCallService`<br>`RegisterViewModelTests.RegisterAsync_WithAllFieldsEmpty_ShouldReportFirstMissingFieldFirst`<br>`RegisterViewModelTests.RegisterAsync_WithValidData_ShouldSucceedAndPassFieldsToService`<br>`RegisterViewModelTests.RegisterAsync_WhenLoginOrEmailTaken_ShouldShowDuplicateMessage` | `AuthService_RegisterThenLogin_AuthenticatesNewUser_IntegrationTest`<br>`AuthService_Register_DuplicateUsername_ReturnsFalse_IntegrationTest`<br>`UserRepository_Create_PersistsUser_IntegrationTest`<br>`UserRepository_Create_DuplicateUsername_Throws_IntegrationTest` | — (регистрации нет в срезе API) | ✅ пустые поля формы (`RegisterViewModel`) проверены: сообщение о первом незаполненном поле, сервис не вызывается | `RegisterAsync` / `RegisterViewModel` |
| 2 | **Авторизация** → «Ввод логина и пароля» (гость) | FR-02 | `AuthServiceTests.LoginAsync_WithValidCredentials_ShouldSucceed`<br>`AuthServiceTests.LoginAsync_WithInvalidPassword_ShouldFail`<br>`AuthServiceTests.LoginAsync_WithNonExistentUser_ShouldFail`<br>`AuthServiceTests.GetCurrentUserAsync_AfterLogin_ShouldReturnUser`<br>`AuthServiceMoqTests.LoginAsync_WithNonExistentUser_ShouldOnlyQueryRepositoryOnce`<br>`LoginViewModelTests.LoginAsync_WithEmptyUsername_ShouldShowErrorAndNotCallService`<br>`LoginViewModelTests.LoginAsync_WithEmptyPassword_ShouldShowErrorAndNotCallService`<br>`LoginViewModelTests.LoginAsync_WithWrongCredentials_ShouldShowSingleMessageForAnyCause` | `AuthService_RegisterThenLogin_AuthenticatesNewUser_IntegrationTest`<br>`AuthService_Login_WrongPassword_ReturnsNull_IntegrationTest`<br>`UserRepository_GetByUsername_ReturnsSeededAdmin_IntegrationTest`<br>`UserRepository_GetByUsername_UnknownUser_ReturnsNull_IntegrationTest` | `DemoScenario_AnalystAnalyzesCongestionAndSavesReport_E2ETest` (шаги 3–4: неверный и верный вход по HTTP) | ✅ | `LoginAsync` / `LoginViewModel` |
| 3 | **Задать фильтры на карте** (аналитик, админ) | FR-04, FR-05, FR-06 | `FlightFilterTests` (область, высота, страна, сочетание, меридиан 180°)<br>`MapServiceTests.GetCurrentFlightsAsync_WithCountryFilter_ShouldReturnOnlyThatCountry`<br>`MapServiceTests.GetCurrentFlightsAsync_WithAltitudeFilter_ShouldReturnFlightsInRange`<br>`MapServiceTests.GetCurrentFlightsAsync_WithAreaFilter_ShouldReturnFlightsInsideVisibleArea`<br>`MapServiceTests.GetCurrentFlightsAsync_WithInvertedAltitudeRange_ShouldThrowArgumentException`<br>`MapServiceTests.GetCurrentFlightsAsync_ShouldReturnFlights`<br>`MapServiceTests.GetAirportsAsync_WithCache_ShouldReturnFromCache` | `MapService_GetCurrentFlights_FilterByCountry_IntegrationTest`<br>`MapService_GetCurrentFlights_FilterByAltitudeRange_IntegrationTest`<br>`MapService_GetCurrentFlights_FilterByVisibleArea_IntegrationTest`<br>`MapService_GetCurrentFlights_CombinedFilters_IntegrationTest`<br>`MapRepository_GetCurrentFlights_ReturnsFreshPositions_IntegrationTest`<br>`MapRepository_GetAirports_ReadsDatabaseThenCache_IntegrationTest` | `DemoScenario_AnalystAnalyzesCongestionAndSavesReport_E2ETest` (шаг 1: карта, счётчик рейсов, `bbox`, страна, диапазон высот, их сочетание, ошибка диапазона) | ⚠️ фильтры (область, высота, страна) **реализованы в бизнес-логике и API и покрыты тестами всех трёх уровней; в интерфейсе Blazor их нет** (FR-06 в README — ⚠️) | `MapService` / `FlightFilter` |
| 4 | **Задать фильтры в витрине** (аналитик, админ) → период и аэропорт | FR-07, FR-08, FR-09 | `AnalyticsServiceTests.GetCongestionAsync_ShouldReturnReport`<br>`AnalyticsServiceTests.GetCongestionAsync_WhenStartDateAfterEndDate_ShouldThrowException`<br>`AnalyticsServiceTests.GetCongestionAsync_WhenStartEqualsEnd_ShouldSucceed`<br>`AnalyticsServiceTests.GetCongestionAsync_WithEmptyData_ShouldReturnZeroCounts`<br>`AnalyticsServiceTests.GetCongestionAsync_WithCacheHit_ShouldReturnCachedReportWithoutTouchingRepository`<br>`SqlQueryFactoryTests` (запросы витрины) | `AnalyticsRepository_GetCongestion_HalfOpenPeriod_IntegrationTest`<br>`AnalyticsRepository_GetCongestion_FiltersByAirport_IntegrationTest`<br>`AnalyticsRepository_GetCongestion_AirportWithoutData_ReturnsEmpty_IntegrationTest`<br>`AnalyticsRepository_GetCongestion_WithoutDetails_IntegrationTest`<br>`AnalyticsRepository_GetAllCongestion_EntryAndExitInsidePeriod_IntegrationTest`<br>`AnalyticsService_GetCongestion_CountsDeparturesAndArrivals_IntegrationTest`<br>`AnalyticsService_GetCongestion_SecondCallServedFromRedis_IntegrationTest` | `DemoScenario_AnalystAnalyzesCongestionAndSavesReport_E2ETest` (шаг 5 — витрина и счётчики; шаг 7 — ошибки периода и аэропорта) | ✅ | `Analytics` |
| 5 | **Выгрузить CSV-отчёт** (из «Задать фильтры в витрине») | FR-10 | `AnalyticsServiceTests.ExportToCsvAsync_WithEntries_ShouldSerializeEachEntryAsCsvRow`<br>`AnalyticsServiceTests.ExportToCsvAsync_WithEmptyEntries_ShouldReturnOnlyHeaderRow`<br>`AnalyticsServiceTests.ExportToCsvAsync_WithNullReport_ShouldThrowException` | `AnalyticsService_ExportToCsv_RowCountEqualsEntries_IntegrationTest` | `DemoScenario_AnalystAnalyzesCongestionAndSavesReport_E2ETest` (шаг 6: строк CSV столько же, сколько записей) | ✅ | `ExportToCsv` |
| 6 | **Выйти из аккаунта** (аналитик, админ) | FR-03 | `AuthServiceTests.LogoutAsync_AfterLogin_ShouldClearCurrentSession`<br>`AuthServiceTests.LogoutAsync_WithoutActiveSession_ShouldNotThrow`<br>`AuthServiceTests.GetCurrentUserAsync_WithoutLogin_ShouldReturnNull` | `AuthService_Logout_ClearsCurrentUser_IntegrationTest` | `DemoScenario_AnalystAnalyzesCongestionAndSavesReport_E2ETest` (шаг 11: токен после выхода отклоняется) | ✅ | `Logout` |
| 7 | **Открыть таблицу с пользователями** (админ) | FR-13 | `AdminServiceTests.GetAllUsersAsync_WithSeededUsers_ShouldReturnMappedDtos`<br>`AdminServiceTests.GetAllUsersAsync_WithNoUsers_ShouldReturnEmptyList` | `UserRepository_GetAll_ReturnsSeededUsers_IntegrationTest` | — (операция `listUsers` вне среза API) | ✅ | `GetAllUsers` |
| 8 | **Редактировать пользователя** (админ) | FR-15 | `AdminServiceTests.UpdateUserAsync_WithExistingUser_ShouldPersistChangesAndReturnTrue`<br>`AdminServiceTests.UpdateUserAsync_WithNewPassword_ShouldStoreDifferentHashThanPlainPassword`<br>`AdminServiceTests.UpdateUserAsync_WithNonExistentUser_ShouldReturnFalse` | `AdminService_UpdateUser_ChangesRoleAndPassword_IntegrationTest`<br>`UserRepository_Update_ChangesRoleAndEmail_IntegrationTest` | — | ✅ | `UpdateUser` |
| 9 | **Удалить пользователя** (админ) | FR-16 | `AdminServiceTests.DeleteUserAsync_WithExistingUser_ShouldRemoveAndReturnTrue`<br>`AdminServiceTests.DeleteUserAsync_WithNonExistentUser_ShouldReturnFalse`<br>`AdminServiceTests.DeleteUserAsync_WithAdminUser_ShouldThrowAndKeepUser`<br>`AdminServiceTests.DeleteUserAsync_WithAnalystWhileAdminExists_ShouldRemoveOnlyAnalyst` | `AdminService_DeleteUser_RemovesUserWithReports_IntegrationTest`<br>`AdminService_DeleteUser_AdminUser_Throws_IntegrationTest`<br>`UserRepository_Delete_CascadesSavedReports_IntegrationTest`<br>`UserRepository_Delete_UnknownUser_ReturnsFalse_IntegrationTest` | — | ✅ правило «нельзя удалить `admin` и себя» реализовано в `AdminService`: удалять пользователей может только администратор, поэтому удаление себя — тот же случай | `DeleteUser` |
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
| ✅ покрыты | 8 (№ 1, 2, 4, 5, 6, 7, 8, 9) |
| ⚠️ покрыты частично (причина указана) | 2 (№ 3, 10) |
| ⛔ вне границ системы | 3 (№ 11–13) |

Оба «частичных» — расхождения между диаграммой и реализацией, а не пробелы в тестах: фильтры карты (FR-06) есть в бизнес-логике и API,
но не в интерфейсе; отдельного метода «добавить пользователя» нет (интерфейс регистрирует пользователя и затем меняет роль, оба шага проверены).
