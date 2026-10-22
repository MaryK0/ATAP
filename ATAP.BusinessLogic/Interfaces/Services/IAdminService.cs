using ATAP.BusinessLogic.DTO;

namespace ATAP.BusinessLogic.Interfaces.Services;

public interface IAdminService
{
    Task<List<UserDto>> GetAllUsersAsync();
    Task<bool> UpdateUserAsync(int userId, string username, string email, string? newPassword, string role);
    Task<bool> DeleteUserAsync(int userId);
}
