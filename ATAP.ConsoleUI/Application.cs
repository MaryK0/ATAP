using Microsoft.Extensions.Logging;
using ATAP.ConsoleUI.Menus;

namespace ATAP.ConsoleUI;

public class Application
{
    private readonly SessionContext _session;
    private readonly GuestMenu _guestMenu;
    private readonly AnalystMenu _analystMenu;
    private readonly AdminMenu _adminMenu;
    private readonly ILogger<Application> _logger;

    public Application(
        SessionContext session,
        GuestMenu guestMenu,
        AnalystMenu analystMenu,
        AdminMenu adminMenu,
        ILogger<Application> logger)
    {
        _session = session;
        _guestMenu = guestMenu;
        _analystMenu = analystMenu;
        _adminMenu = adminMenu;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("Запуск ATAP Технологический UI...\n");
        _logger.LogInformation("Сессия пользователя начата");

        while (!_session.ExitRequested)
        {
            var menu = ResolveMenu();
            await menu.ShowAsync();
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
