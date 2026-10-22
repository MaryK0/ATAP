using ATAP.BusinessLogic.Interfaces.Repositories;

namespace ATAP.BusinessLogic.Tests.TestData.Builders;

/// <summary>
/// Data Builder (fluent) для <see cref="UserDb"/>. В отличие от Object Mother (см. <see cref="Mothers.Users"/>),
/// не задаёт "типовые" именованные объекты, а даёт пошагово собрать произвольный вариант под конкретный тест.
/// </summary>
public class UserDbBuilder
{
    private string _username = "user";
    private string _email = "user@test.local";
    private string _passwordHash = "hash";
    private string _role = "analyst";
    private int _userId;
    private DateTime _createdAt = DateTime.UtcNow;

    public UserDbBuilder WithUsername(string username) { _username = username; return this; }
    public UserDbBuilder WithEmail(string email) { _email = email; return this; }
    public UserDbBuilder WithPasswordHash(string hash) { _passwordHash = hash; return this; }
    public UserDbBuilder WithRole(string role) { _role = role; return this; }
    public UserDbBuilder WithUserId(int id) { _userId = id; return this; }
    public UserDbBuilder AsAdmin() { _role = "admin"; return this; }
    public UserDbBuilder AsAnalyst() { _role = "analyst"; return this; }

    public UserDb Build() => new()
    {
        UserId = _userId,
        Username = _username,
        Email = _email,
        PasswordHash = _passwordHash,
        Role = _role,
        CreatedAt = _createdAt
    };

    public static implicit operator UserDb(UserDbBuilder builder) => builder.Build();
}
