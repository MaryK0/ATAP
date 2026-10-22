using Microsoft.Extensions.Logging;
using ATAP.BusinessLogic.Interfaces.Services;
using ATAP.ConsoleUI.Helpers;

namespace ATAP.ConsoleUI.Menus;

public class GuestMenu : IMenu
{
    private readonly IAuthService _authService;
    private readonly IMapService _mapService;
    private readonly SessionContext _session;
    private readonly ILogger<GuestMenu> _logger;
    private readonly int _maxFlightsPreview;

    public GuestMenu(
        IAuthService authService,
        IMapService mapService,
        SessionContext session,
        AppSettings appSettings,
        ILogger<GuestMenu> logger)
    {
        _authService = authService;
        _mapService = mapService;
        _session = session;
        _logger = logger;
        _maxFlightsPreview = appSettings.MaxFlightsPreview;
    }

    public async Task ShowAsync()
    {
        while (!_session.IsLoggedIn && !_session.ExitRequested)
        {
            Console.Clear();
            ConsoleHelper.PrintHeader("ATAP — Air Traffic Analytics Platform");
            Console.WriteLine("  Вы не авторизованы\n");
            Console.WriteLine("  1. Войти");
            Console.WriteLine("  2. Зарегистрироваться");
            Console.WriteLine("  3. Просмотр текущих рейсов (гость)");
            Console.WriteLine("  4. Сменить папку логирования");
            Console.WriteLine("  0. Выход");
            Console.Write("\nВыберите действие: ");

            switch (Console.ReadLine()?.Trim())
            {
                case "1": await LoginAsync(); break;
                case "2": await RegisterAsync(); break;
                case "3": await ViewFlightsAsync(); break;
                case "4": ChangeLogFolder(); break;
                case "0":
                    _session.ExitRequested = true;
                    _logger.LogInformation("Гость завершил работу с приложением");
                    break;
                default:
                    _logger.LogDebug("Гость ввёл неверный пункт меню");
                    ConsoleHelper.PrintError("Неверный выбор");
                    ConsoleHelper.Pause();
                    break;
            }
        }
    }

    private async Task LoginAsync()
    {
        Console.Clear();
        ConsoleHelper.PrintHeader("Авторизация");
        Console.Write("Логин: ");
        var username = Console.ReadLine() ?? string.Empty;
        Console.Write("Пароль: ");
        var password = ConsoleHelper.ReadPassword();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Попытка входа с пустым логином или паролем");
            ConsoleHelper.PrintError("Логин и пароль не могут быть пустыми");
            ConsoleHelper.Pause();
            return;
        }

        try
        {
            var user = await _authService.LoginAsync(username, password);
            if (user != null)
            {
                _session.CurrentUser = user;
                _logger.LogInformation("Пользователь {Username} успешно вошёл в систему (роль: {Role})",
                    user.Username, user.Role);
                ConsoleHelper.PrintSuccess($"Добро пожаловать, {user.Username}! Роль: {user.Role}");
                ConsoleHelper.Pause();
            }
            else
            {
                _logger.LogWarning("Неудачная попытка входа для пользователя {Username}", username);
                ConsoleHelper.PrintError("Неверный логин или пароль");
                ConsoleHelper.Pause();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при попытке входа пользователя {Username}", username);
            ConsoleHelper.PrintError($"Ошибка подключения к БД: {ex.Message}");
            ConsoleHelper.Pause();
        }
    }

    private async Task RegisterAsync()
    {
        Console.Clear();
        ConsoleHelper.PrintHeader("Регистрация нового пользователя");
        Console.Write("Логин: ");
        var username = Console.ReadLine() ?? string.Empty;
        Console.Write("Email: ");
        var email = Console.ReadLine() ?? string.Empty;
        Console.Write("Пароль: ");
        var password = ConsoleHelper.ReadPassword();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Попытка регистрации с незаполненными полями");
            ConsoleHelper.PrintError("Все поля обязательны для заполнения");
            ConsoleHelper.Pause();
            return;
        }

        try
        {
            var success = await _authService.RegisterAsync(username, email, password);
            if (success)
            {
                _logger.LogInformation("Зарегистрирован новый пользователь {Username} ({Email})", username, email);
                ConsoleHelper.PrintSuccess("Регистрация прошла успешно! Теперь вы можете войти.");
            }
            else
            {
                _logger.LogWarning("Регистрация отклонена — логин или email уже занят: {Username}, {Email}",
                    username, email);
                ConsoleHelper.PrintError("Пользователь с таким логином или email уже существует.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при регистрации пользователя {Username}", username);
            ConsoleHelper.PrintError($"Ошибка: {ex.Message}");
        }
        ConsoleHelper.Pause();
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
            _logger.LogInformation("Путь к лог-файлу изменён на {Path}", LogConfigurator.CurrentLogPath);
            ConsoleHelper.PrintSuccess($"Лог-файл: {LogConfigurator.CurrentLogPath}");
        }
        else
        {
            ConsoleHelper.PrintError(error);
        }
        ConsoleHelper.Pause();
    }

    private async Task ViewFlightsAsync()
    {
        Console.Clear();
        ConsoleHelper.PrintHeader("Текущие рейсы (карта)");
        Console.WriteLine("Загрузка данных из БД...\n");
        _logger.LogInformation("Гость запросил список текущих рейсов");

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
                _logger.LogInformation("Гостю показано {Shown} из {Total} рейсов",
                    Math.Min(flights.Count, _maxFlightsPreview), flights.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке списка рейсов для гостя");
            ConsoleHelper.PrintError($"Ошибка загрузки данных: {ex.Message}");
        }
        ConsoleHelper.Pause();
    }
}
