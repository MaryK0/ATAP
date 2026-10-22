using ATAP.BusinessLogic.DTO;

namespace ATAP.ConsoleUI;

public class SessionContext
{
    public UserDto? CurrentUser { get; set; }
    public bool ExitRequested { get; set; }
    public bool IsLoggedIn => CurrentUser != null;
}
