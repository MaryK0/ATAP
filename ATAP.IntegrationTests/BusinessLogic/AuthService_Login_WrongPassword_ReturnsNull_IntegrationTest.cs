using ATAP.BusinessLogic.Services;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class AuthService_Login_WrongPassword_ReturnsNull_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public AuthService_Login_WrongPassword_ReturnsNull_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task LoginAsync_WrongPasswordOrUnknownUser_ReturnsNullAndNoSession()
    {
        // Arrange
        var service = new AuthService(_db.CreateUserRepository());
        await service.RegisterAsync("login_user", "login_user@atap.com", "S3cure!pass");

        // Act
        var wrongPassword = await service.LoginAsync("login_user", "wrong-password");
        var unknownUser = await service.LoginAsync("nobody", "S3cure!pass");

        // Assert
        Assert.Null(wrongPassword);
        Assert.Null(unknownUser);
        Assert.Null(await service.GetCurrentUserAsync());
    }
}
