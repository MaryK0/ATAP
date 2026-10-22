using ATAP.BusinessLogic.Services;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class AuthService_Logout_ClearsCurrentUser_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public AuthService_Logout_ClearsCurrentUser_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task LogoutAsync_AfterLogin_ClearsCurrentUser()
    {
        // Arrange
        var service = new AuthService(_db.CreateUserRepository());
        await service.RegisterAsync("logout_user", "logout_user@atap.com", "S3cure!pass");
        await service.LoginAsync("logout_user", "S3cure!pass");
        Assert.NotNull(await service.GetCurrentUserAsync());

        // Act
        await service.LogoutAsync();

        // Assert
        Assert.Null(await service.GetCurrentUserAsync());
    }
}
