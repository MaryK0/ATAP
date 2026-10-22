namespace ATAP.BusinessLogic.Interfaces.Repositories;

public interface IUserRepository
{
    Task<UserDb?> GetByUsernameAsync(string username);
    Task<UserDb?> GetByEmailAsync(string email);  // ← добавлен
    Task<int> CreateAsync(UserDb user);
    Task<bool> UpdateAsync(UserDb user);          // ← добавлен
    Task<bool> DeleteAsync(int userId); 
    Task<List<UserDb>> GetAllAsync();           // ← добавлен
}

public class UserDb
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "client";
    public DateTime CreatedAt { get; set; }       // ← добавлен
    public DateTime? SubscriptionExpiresAt { get; set; } // ← добавлен
}