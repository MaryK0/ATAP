using System.Security.Cryptography;
using System.Text;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.BusinessLogic.Interfaces.Services;

namespace ATAP.BusinessLogic.Services;

public class AdminService : IAdminService
{
    private readonly IUserRepository _userRepo;

    public AdminService(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    public async Task<List<UserDto>> GetAllUsersAsync()
    {
        var users = await _userRepo.GetAllAsync();
        return users.Select(u => new UserDto
        {
            UserId = u.UserId,
            Username = u.Username,
            Email = u.Email,
            Role = u.Role,
            CreatedAt = u.CreatedAt
        }).ToList();
    }

    public async Task<bool> UpdateUserAsync(int userId, string username, string email, string? newPassword, string role)
    {
        var users = await _userRepo.GetAllAsync();
        var user = users.FirstOrDefault(u => u.UserId == userId);
        if (user == null) return false;

        user.Username = username;
        user.Email = email;
        user.Role = role;
        if (!string.IsNullOrWhiteSpace(newPassword))
            user.PasswordHash = HashPassword(newPassword);

        return await _userRepo.UpdateAsync(user);
    }

    /// <summary>
    /// Удаляет пользователя (FR-16). Пользователя с ролью <c>admin</c> удалить нельзя — в том числе самому себе,
    /// так как удалять пользователей может только администратор. Несуществующий пользователь — <c>false</c>.
    /// </summary>
    /// <exception cref="AdminDeletionForbiddenException">Удаляемый пользователь — администратор.</exception>
    public async Task<bool> DeleteUserAsync(int userId)
    {
        var users = await _userRepo.GetAllAsync();
        var user = users.FirstOrDefault(u => u.UserId == userId);
        if (user == null) return false;

        if (user.Role == "admin")
            throw new AdminDeletionForbiddenException();

        return await _userRepo.DeleteAsync(userId);
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        return Convert.ToBase64String(sha256.ComputeHash(bytes));
    }
}
