using ATAP.BusinessLogic.DTO;

namespace ATAP.BusinessLogic.Interfaces.Services;

public interface IAuthService
{
    Task<UserDto?> LoginAsync(string username, string password);
    Task<bool> RegisterAsync(string username, string email, string password);
    Task<UserDto?> GetCurrentUserAsync();
    Task LogoutAsync();  
}