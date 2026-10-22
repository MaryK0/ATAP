using System.Security.Cryptography;
using System.Text;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Services;
using ATAP.BusinessLogic.Interfaces.Repositories;

namespace ATAP.BusinessLogic.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private UserDto? _currentUser;

    public AuthService(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    public async Task<UserDto?> LoginAsync(string username, string password)
    {
        var user = await _userRepo.GetByUsernameAsync(username);
        if (user == null)
            return null;

        if (!VerifyPassword(password, user.PasswordHash))
            return null;

        _currentUser = new UserDto
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };
        
        return _currentUser;
    }

    public async Task<bool> RegisterAsync(string username, string email, string password)
    {
        var existing = await _userRepo.GetByUsernameAsync(username);
        if (existing != null)
            return false;

        var existingByEmail = await _userRepo.GetByEmailAsync(email);
        if (existingByEmail != null)
            return false;

        var passwordHash = HashPassword(password);

        var newUser = new UserDb
        {
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            Role = "analyst"
        };
        
        var userId = await _userRepo.CreateAsync(newUser);
        return userId > 0;
    }

    public async Task LogoutAsync()
    {
        _currentUser = null;
        await Task.CompletedTask;
    }

    public Task<UserDto?> GetCurrentUserAsync()
    {
        return Task.FromResult(_currentUser);
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var hash = HashPassword(password);
        return hash == storedHash;
    }
}