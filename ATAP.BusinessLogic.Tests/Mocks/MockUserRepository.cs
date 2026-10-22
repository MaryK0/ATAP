using ATAP.BusinessLogic.Interfaces.Repositories;

namespace ATAP.BusinessLogic.Tests.Mocks;

public class MockUserRepository : IUserRepository
{
    private readonly List<UserDb> _users = new();

    public Task<UserDb?> GetByUsernameAsync(string username)
    {
        var user = _users.FirstOrDefault(u => u.Username == username);
        return Task.FromResult(user);
    }

    public Task<UserDb?> GetByEmailAsync(string email)
    {
        var user = _users.FirstOrDefault(u => u.Email == email);
        return Task.FromResult(user);
    }

    public Task<int> CreateAsync(UserDb user)
    {
        user.UserId = _users.Count + 1;
        _users.Add(user);
        return Task.FromResult(user.UserId);
    }

    public Task<bool> UpdateAsync(UserDb user)
    {
        var existing = _users.FirstOrDefault(u => u.UserId == user.UserId);
        if (existing == null) return Task.FromResult(false);
        existing.Username = user.Username;
        existing.Email = user.Email;
        existing.Role = user.Role;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int userId)
    {
        var user = _users.FirstOrDefault(u => u.UserId == userId);
        if (user == null) return Task.FromResult(false);
        _users.Remove(user);
        return Task.FromResult(true);
    }

    public Task<List<UserDb>> GetAllAsync()
    {
        return Task.FromResult(new List<UserDb>(_users));
    }

    public void AddUser(UserDb user) => _users.Add(user);
}
