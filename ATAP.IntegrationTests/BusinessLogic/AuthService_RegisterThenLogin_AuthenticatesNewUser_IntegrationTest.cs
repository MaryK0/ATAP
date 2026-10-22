using ATAP.BusinessLogic.Services;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class AuthService_RegisterThenLogin_AuthenticatesNewUser_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public AuthService_RegisterThenLogin_AuthenticatesNewUser_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task RegisterAsync_ThenLoginAsync_StoresHashedPasswordWithAnalystRoleAndAuthenticates()
    {
        // Arrange
        var service = new AuthService(_db.CreateUserRepository());

        // Act
        var registered = await service.RegisterAsync("new_user", "new_user@atap.com", "S3cure!pass");
        var loggedIn = await service.LoginAsync("new_user", "S3cure!pass");

        // Assert
        Assert.True(registered);
        Assert.NotNull(loggedIn);
        Assert.Equal("new_user", loggedIn.Username);
        Assert.Equal("analyst", loggedIn.Role);
        var storedHash = await _db.ScalarAsync<string>("SELECT password_hash FROM dds.users WHERE username = 'new_user'");
        Assert.NotEqual("S3cure!pass", storedHash);
        Assert.Equal("new_user", (await service.GetCurrentUserAsync())?.Username);
    }
}
