using Microsoft.Extensions.Logging;
using ATAP.ConsoleUI.Menus;
using ATAP.BusinessLogic.Interfaces.Services;

namespace ATAP.ConsoleUI;

public class Application
{
    private readonly SessionContext _session;
    private readonly GuestMenu _guestMenu;
    private readonly AnalystMenu _analystMenu;
    private readonly AdminMenu _adminMenu;
    private readonly ILogger<Application> _logger;
    private readonly IAuthService _auth;

    public Application(
        SessionContext session,
        GuestMenu guestMenu,
        AnalystMenu analystMenu,
        AdminMenu adminMenu,
        ILogger<Application> logger, IAuthService auth)
    {
        _session = session;
        _guestMenu = guestMenu;
        _analystMenu = analystMenu;
        _adminMenu = adminMenu;
        _logger = logger;
        _auth = auth;
    }

    public async Task RunAsync()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("Запуск ATAP Технологический UI...\n");
        _logger.LogInformation("Сессия пользователя начата");

        while (!_session.ExitRequested)
        {
            var menu = ResolveMenu();
            var wasLoggedIn = _session.IsLoggedIn;
            await menu.ShowAsync();
            if (wasLoggedIn && !_session.IsLoggedIn)
            {
                try { await _auth.LogoutAsync(); }
                catch (Exception ex) { _logger.LogWarning(ex, "Не удалось подтвердить выход через API"); }
            }
        }

        _logger.LogInformation("Сессия пользователя завершена");
        Console.WriteLine("\nДо свидания!");
    }

    private IMenu ResolveMenu()
    {
        if (!_session.IsLoggedIn)
            return _guestMenu;

        return _session.CurrentUser!.Role switch
        {
            "admin" => _adminMenu,
            _ => _analystMenu
        };
    }
}
