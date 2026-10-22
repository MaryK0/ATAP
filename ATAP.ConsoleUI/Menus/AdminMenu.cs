using Microsoft.Extensions.Logging;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Services;
using ATAP.ConsoleUI.Helpers;

namespace ATAP.ConsoleUI.Menus;

public class AdminMenu : IMenu
{
    private readonly IAdminService _adminService;
    private readonly IAnalyticsService _analyticsService;
    private readonly IMapService _mapService;
    private readonly SessionContext _session;
    private readonly ILogger<AdminMenu> _logger;
    private readonly int _maxFlightsPreview;
    private readonly int _maxReportPreview;
    private CongestionReportDto? _lastReport;

    public AdminMenu(
        IAdminService adminService,
        IAnalyticsService analyticsService,
        IMapService mapService,
        SessionContext session,
        AppSettings appSettings,
        ILogger<AdminMenu> logger)
    {
        _adminService = adminService;
        _analyticsService = analyticsService;
        _mapService = mapService;
        _session = session;
        _logger = logger;
        _maxFlightsPreview = appSettings.MaxFlightsPreview;
        _maxReportPreview = appSettings.MaxReportPreview;
    }

    public async Task ShowAsync()
    {
        while (_session.IsLoggedIn && !_session.ExitRequested)
        {
            Console.Clear();
            ConsoleHelper.PrintHeader($"ATAP — Администратор: {_session.CurrentUser!.Username}");
            Console.WriteLine("  --- Управление пользователями ---");
            Console.WriteLine("  1. Список пользователей");
            Console.WriteLine("  2. Редактировать пользователя");
            Console.WriteLine("  3. Удалить пользователя");
            Console.WriteLine("  --- Аналитические инструменты ---");
            Console.WriteLine("  4. Список аэропортов");
            Console.WriteLine("  5. Анализ загруженности аэропорта");
            Console.WriteLine("  6. Выгрузить последний отчёт в CSV");
            Console.WriteLine("  7. Просмотр текущих рейсов");
            Console.WriteLine("  8. Сменить папку логирования");
            Console.WriteLine("  0. Выйти из аккаунта");
            Console.Write("\nВыберите действие: ");

            switch (Console.ReadLine()?.Trim())
            {
                case "1": await ListUsersAsync(); break;
                case "2": await EditUserAsync(); break;
                case "3": await DeleteUserAsync(); break;
                case "4": await ShowAirportsAsync(); break;
                case "5": await ShowCongestionReportAsync(); break;
                case "6": await ExportCsvAsync(); break;
                case "7": await ShowFlightsAsync(); break;
                case "8": ChangeLogFolder(); break;
                case "0":
                    _logger.LogInformation("Администратор {Username} вышел из системы",
                        _session.CurrentUser.Username);
                    _session.CurrentUser = null;
                    break;
                default:
                    _logger.LogDebug("Администратор {Username} ввёл неверный пункт меню",
                        _session.CurrentUser.Username);
                    ConsoleHelper.PrintError("Неверный выбор");
                    ConsoleHelper.Pause();
                    break;
            }
        }
    }

    private void ChangeLogFolder()
    {
        Console.Clear();
        ConsoleHelper.PrintHeader("Смена папки логирования");
        Console.WriteLine($"Текущий путь: {LogConfigurator.CurrentLogPath}\n");
        Console.Write("Новый путь к лог-файлу: ");
        var input = Console.ReadLine()?.Trim() ?? "";
        if (LogConfigurator.TryChange(input, out var error))
        {
            _logger.LogInformation("Администратор {Username} изменил путь к лог-файлу на {Path}",
                _session.CurrentUser!.Username, LogConfigurator.CurrentLogPath);
            ConsoleHelper.PrintSuccess($"Лог-файл: {LogConfigurator.CurrentLogPath}");
        }
        else
        {
            ConsoleHelper.PrintError(error);
        }
        ConsoleHelper.Pause();
    }

    private async Task ListUsersAsync()
    {
        Console.Clear();
        ConsoleHelper.PrintHeader("Список пользователей системы");
        _logger.LogInformation("Администратор {Username} запросил список пользователей",
            _session.CurrentUser!.Username);
        try
        {
            var users = await _adminService.GetAllUsersAsync();
            if (users.Count == 0)
            {
                Console.WriteLine("Пользователи не найдены.");
                _logger.LogWarning("Список пользователей пуст");
            }
            else
            {
                Console.WriteLine($"{"ID",-6} {"Логин",-20} {"Email",-32} {"Роль",-12} {"Дата регистрации"}");
                Console.WriteLine(new string('-', 86));
                foreach (var u in users)
                    Console.WriteLine($"{u.UserId,-6} {u.Username,-20} {u.Email,-32} {u.Role,-12} {u.CreatedAt:yyyy-MM-dd HH:mm}");
                Console.WriteLine($"\nВсего пользователей: {users.Count}");
                _logger.LogInformation("Администратору {Username} показан список из {Count} пользователей",
                    _session.CurrentUser!.Username, users.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке списка пользователей, запрос от {Username}",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError($"Ошибка: {ex.Message}");
        }
        ConsoleHelper.Pause();
    }

    private async Task EditUserAsync()
    {
        Console.Clear();
        ConsoleHelper.PrintHeader("Редактировать пользователя");

        Console.Write("ID пользователя: ");
        if (!int.TryParse(Console.ReadLine(), out var userId))
        {
            _logger.LogWarning("Администратор {Username} ввёл неверный ID пользователя при редактировании",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError("Неверный ID");
            ConsoleHelper.Pause();
            return;
        }

        List<UserDto> users;
        try
        {
            users = await _adminService.GetAllUsersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке пользователей для редактирования, запрос от {Username}",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError($"Ошибка загрузки пользователей: {ex.Message}");
            ConsoleHelper.Pause();
            return;
        }

        var user = users.FirstOrDefault(u => u.UserId == userId);
        if (user == null)
        {
            _logger.LogWarning("Администратор {Admin} пытается редактировать несуществующего пользователя ID={UserId}",
                _session.CurrentUser!.Username, userId);
            ConsoleHelper.PrintError($"Пользователь с ID {userId} не найден");
            ConsoleHelper.Pause();
            return;
        }

        Console.WriteLine($"\nТекущие данные пользователя #{userId}:");
        Console.WriteLine($"  Логин  : {user.Username}");
        Console.WriteLine($"  Email  : {user.Email}");
        Console.WriteLine($"  Роль   : {user.Role}");
        Console.WriteLine("\nВведите новые значения. Enter — оставить без изменений.\n");

        Console.Write($"Логин [{user.Username}]: ");
        var input = Console.ReadLine();
        var newUsername = string.IsNullOrWhiteSpace(input) ? user.Username : input.Trim();

        Console.Write($"Email [{user.Email}]: ");
        input = Console.ReadLine();
        var newEmail = string.IsNullOrWhiteSpace(input) ? user.Email : input.Trim();

        Console.Write("Новый пароль [Enter — не менять]: ");
        var newPassword = ConsoleHelper.ReadPassword();

        Console.WriteLine($"Роль [{user.Role}] (analyst / admin): ");
        Console.Write("Новая роль: ");
        input = Console.ReadLine()?.Trim().ToLower();
        string newRole;
        if (string.IsNullOrWhiteSpace(input))
        {
            newRole = user.Role;
        }
        else if (input == "analyst" || input == "admin")
        {
            newRole = input;
        }
        else
        {
            _logger.LogWarning("Администратор {Admin} ввёл недопустимую роль '{Role}' для пользователя ID={UserId}",
                _session.CurrentUser!.Username, input, userId);
            ConsoleHelper.PrintError("Недопустимая роль. Допустимые значения: analyst, admin");
            ConsoleHelper.Pause();
            return;
        }

        Console.WriteLine("\nИтоговые изменения:");
        Console.WriteLine($"  Логин  : {user.Username} → {newUsername}");
        Console.WriteLine($"  Email  : {user.Email} → {newEmail}");
        Console.WriteLine($"  Пароль : {(string.IsNullOrWhiteSpace(newPassword) ? "без изменений" : "будет обновлён")}");
        Console.WriteLine($"  Роль   : {user.Role} → {newRole}");
        Console.Write("\nПодтвердить изменения? (да/нет): ");
        if (Console.ReadLine()?.Trim().ToLower() != "да")
        {
            _logger.LogInformation("Администратор {Admin} отменил редактирование пользователя ID={UserId}",
                _session.CurrentUser!.Username, userId);
            Console.WriteLine("Отменено.");
            ConsoleHelper.Pause();
            return;
        }

        try
        {
            var success = await _adminService.UpdateUserAsync(
                userId,
                newUsername,
                newEmail,
                string.IsNullOrWhiteSpace(newPassword) ? null : newPassword,
                newRole);

            if (success)
            {
                _logger.LogInformation(
                    "Администратор {Admin} обновил пользователя ID={UserId}: логин={NewUsername}, роль={NewRole}",
                    _session.CurrentUser!.Username, userId, newUsername, newRole);
                ConsoleHelper.PrintSuccess($"Данные пользователя #{userId} успешно обновлены");
            }
            else
            {
                _logger.LogWarning("Не удалось обновить пользователя ID={UserId}, запрос от {Admin}",
                    userId, _session.CurrentUser!.Username);
                ConsoleHelper.PrintError("Не удалось обновить пользователя");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении пользователя ID={UserId}, запрос от {Admin}",
                userId, _session.CurrentUser!.Username);
            ConsoleHelper.PrintError($"Ошибка: {ex.Message}");
        }
        ConsoleHelper.Pause();
    }

    private async Task DeleteUserAsync()
    {
        Console.Clear();
        ConsoleHelper.PrintHeader("Удалить пользователя");
        Console.Write("ID пользователя: ");
        if (!int.TryParse(Console.ReadLine(), out var userId))
        {
            _logger.LogWarning("Администратор {Admin} ввёл неверный ID при удалении пользователя",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError("Неверный ID");
            ConsoleHelper.Pause();
            return;
        }

        List<UserDto> users;
        try
        {
            users = await _adminService.GetAllUsersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке пользователей для удаления, запрос от {Admin}",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError($"Ошибка загрузки пользователей: {ex.Message}");
            ConsoleHelper.Pause();
            return;
        }

        var target = users.FirstOrDefault(u => u.UserId == userId);
        if (target == null)
        {
            _logger.LogWarning("Администратор {Admin} пытается удалить несуществующего пользователя ID={UserId}",
                _session.CurrentUser!.Username, userId);
            ConsoleHelper.PrintError($"Пользователь с ID {userId} не найден");
            ConsoleHelper.Pause();
            return;
        }
        if (target.UserId == _session.CurrentUser!.UserId)
        {
            _logger.LogWarning("Администратор {Admin} попытался удалить самого себя", _session.CurrentUser.Username);
            ConsoleHelper.PrintError("Нельзя удалить самого себя");
            ConsoleHelper.Pause();
            return;
        }
        if (target.Role == "admin")
        {
            _logger.LogWarning("Администратор {Admin} попытался удалить другого администратора ID={UserId}",
                _session.CurrentUser.Username, userId);
            ConsoleHelper.PrintError("Нельзя удалить другого администратора");
            ConsoleHelper.Pause();
            return;
        }

        Console.Write($"Подтвердите удаление пользователя #{userId} ({target.Username}) (да/нет): ");
        if (Console.ReadLine()?.Trim().ToLower() != "да")
        {
            _logger.LogInformation("Администратор {Admin} отменил удаление пользователя ID={UserId}",
                _session.CurrentUser!.Username, userId);
            Console.WriteLine("Операция отменена.");
            ConsoleHelper.Pause();
            return;
        }

        try
        {
            var success = await _adminService.DeleteUserAsync(userId);
            if (success)
            {
                _logger.LogInformation("Администратор {Admin} удалил пользователя ID={UserId} ({TargetUsername})",
                    _session.CurrentUser!.Username, userId, target.Username);
                ConsoleHelper.PrintSuccess($"Пользователь #{userId} успешно удалён");
            }
            else
            {
                _logger.LogWarning("Не удалось удалить пользователя ID={UserId}, запрос от {Admin}",
                    userId, _session.CurrentUser!.Username);
                ConsoleHelper.PrintError($"Пользователь с ID {userId} не найден");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении пользователя ID={UserId}, запрос от {Admin}",
                userId, _session.CurrentUser!.Username);
            ConsoleHelper.PrintError($"Ошибка: {ex.Message}");
        }
        ConsoleHelper.Pause();
    }

    private async Task ShowAirportsAsync()
    {
        Console.Clear();
        ConsoleHelper.PrintHeader("Список аэропортов");
        Console.WriteLine("Загрузка...\n");
        _logger.LogInformation("Администратор {Username} запросил список аэропортов",
            _session.CurrentUser!.Username);
        try
        {
            var airports = await _mapService.GetAirportsAsync();
            if (airports.Count == 0)
            {
                Console.WriteLine("Аэропорты не найдены.");
            }
            else
            {
                Console.WriteLine($"{"ID",-6} {"ICAO",-8} {"Название",-40} {"Широта",-12} {"Долгота"}");
                Console.WriteLine(new string('-', 78));
                foreach (var a in airports)
                    Console.WriteLine($"{a.Id,-6} {a.IcaoCode,-8} {a.Name,-40} {a.Latitude,-12:F4} {a.Longitude:F4}");
                Console.WriteLine($"\nВсего аэропортов: {airports.Count}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке аэропортов, запрос от {Username}",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError($"Ошибка: {ex.Message}");
        }
        ConsoleHelper.Pause();
    }

    private async Task ShowCongestionReportAsync()
    {
        Console.Clear();
        ConsoleHelper.PrintHeader("Анализ загруженности аэропорта");

        Console.Write("ID аэропорта (см. список аэропортов): ");
        if (!int.TryParse(Console.ReadLine(), out var airportId))
        {
            _logger.LogWarning("Администратор {Username} ввёл неверный ID аэропорта",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError("Неверный ID аэропорта");
            ConsoleHelper.Pause();
            return;
        }

        Console.Write("Дата начала периода (гггг-мм-дд): ");
        if (!DateTime.TryParse(Console.ReadLine(), out var start))
        {
            ConsoleHelper.PrintError("Неверный формат даты. Используйте: 2024-01-01");
            ConsoleHelper.Pause();
            return;
        }

        Console.Write("Дата окончания периода (гггг-мм-дд): ");
        if (!DateTime.TryParse(Console.ReadLine(), out var end))
        {
            ConsoleHelper.PrintError("Неверный формат даты. Используйте: 2024-01-31");
            ConsoleHelper.Pause();
            return;
        }

        Console.WriteLine("\nЗапрос к витрине mart_airport_congestion...\n");
        _logger.LogInformation(
            "Администратор {Username} запросил отчёт загруженности: аэропорт {AirportId}, период {Start:yyyy-MM-dd}–{End:yyyy-MM-dd}",
            _session.CurrentUser!.Username, airportId, start, end);

        try
        {
            var report = await _analyticsService.GetCongestionAsync(airportId, start, end);
            _lastReport = report;

            Console.WriteLine($"{"Аэропорт ID:",-20} {report.AirportId}");
            Console.WriteLine($"{"Период:",-20} {start:yyyy-MM-dd} — {end:yyyy-MM-dd}");
            Console.WriteLine($"{"Вылеты:",-20} {report.DepartureCount}");
            Console.WriteLine($"{"Прилёты:",-20} {report.ArrivalCount}");
            Console.WriteLine($"{"Всего движений:",-20} {report.DepartureCount + report.ArrivalCount}");

            if (report.Entries.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"{"flight_id",-10} {"callsign",-12} {"entry_time",-20} {"exit_time",-20} {"dur_sec",8} {"is_dep",-7} {"is_arr",-7} {"max_alt",8} {"min_alt",8}");
                Console.WriteLine(new string('-', 106));
                foreach (var e in report.Entries.Take(_maxReportPreview))
                {
                    Console.WriteLine(
                        $"{e.FlightId,-10} {e.Callsign,-12} {e.EntryTime,-20:yyyy-MM-dd HH:mm} {e.ExitTime,-20:yyyy-MM-dd HH:mm} {e.DurationSeconds,8} {e.IsDeparture,-7} {e.IsArrival,-7} {e.MaxAltitudeInZone,8} {e.MinAltitudeInZone,8}");
                }
                if (report.Entries.Count > _maxReportPreview)
                    Console.WriteLine($"... и ещё {report.Entries.Count - _maxReportPreview} записей (все попадут в CSV)");
            }

            _logger.LogInformation(
                "Отчёт сформирован для {Username}: аэропорт {AirportId}, вылеты={Dep}, прилёты={Arr}",
                _session.CurrentUser!.Username, airportId, report.DepartureCount, report.ArrivalCount);
            ConsoleHelper.PrintSuccess("Отчёт сформирован. Используйте пункт 6 для выгрузки CSV.");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Некорректные параметры запроса отчёта от {Username}",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError($"Ошибка параметров: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при построении отчёта для {Username}, аэропорт {AirportId}",
                _session.CurrentUser!.Username, airportId);
            ConsoleHelper.PrintError($"Ошибка запроса к БД: {ex.Message}");
        }
        ConsoleHelper.Pause();
    }

    private async Task ExportCsvAsync()
    {
        if (_lastReport == null)
        {
            _logger.LogWarning("Администратор {Username} попытался выгрузить CSV без предварительного запроса отчёта",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError("Нет данных для выгрузки. Сначала выполните анализ (пункт 5).");
            ConsoleHelper.Pause();
            return;
        }

        try
        {
            var bytes = await _analyticsService.ExportToCsvAsync(_lastReport);
            var fileName = $"congestion_airport{_lastReport.AirportId}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            await File.WriteAllBytesAsync(fileName, bytes);
            var fullPath = Path.GetFullPath(fileName);
            _logger.LogInformation(
                "Администратор {Username} выгрузил CSV: аэропорт {AirportId}, файл {FileName}",
                _session.CurrentUser!.Username, _lastReport.AirportId, fullPath);
            ConsoleHelper.PrintSuccess($"Файл сохранён: {fullPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при экспорте CSV для {Username}", _session.CurrentUser!.Username);
            ConsoleHelper.PrintError($"Ошибка экспорта: {ex.Message}");
        }
        ConsoleHelper.Pause();
    }

    private async Task ShowFlightsAsync()
    {
        Console.Clear();
        ConsoleHelper.PrintHeader("Текущие рейсы");
        Console.WriteLine("Загрузка...\n");
        _logger.LogInformation("Администратор {Username} запросил список текущих рейсов",
            _session.CurrentUser!.Username);
        try
        {
            var flights = await _mapService.GetCurrentFlightsAsync();
            if (flights.Count == 0)
            {
                Console.WriteLine("Данные о рейсах отсутствуют.");
                _logger.LogWarning("В БД нет данных о текущих рейсах");
            }
            else
            {
                Console.WriteLine($"{"ID",-8} {"Позывной",-14} {"Широта",-12} {"Долгота",-12} {"Высота (м)",12}");
                Console.WriteLine(new string('-', 62));
                foreach (var f in flights.Take(_maxFlightsPreview))
                    Console.WriteLine($"{f.Id,-8} {f.Callsign,-14} {f.Latitude,-12:F4} {f.Longitude,-12:F4} {f.Altitude,12}");
                if (flights.Count > _maxFlightsPreview)
                    Console.WriteLine($"... и ещё {flights.Count - _maxFlightsPreview} рейсов");
                Console.WriteLine($"\nВсего активных рейсов: {flights.Count}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке рейсов для {Username}", _session.CurrentUser!.Username);
            ConsoleHelper.PrintError($"Ошибка: {ex.Message}");
        }
        ConsoleHelper.Pause();
    }
}
