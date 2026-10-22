using ATAP.BusinessLogic.Services;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class AdminService_UpdateUser_ChangesRoleAndPassword_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public AdminService_UpdateUser_ChangesRoleAndPassword_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task UpdateUserAsync_NewRoleAndPassword_PersistsAndAllowsLoginWithNewPassword()
    {
        // Arrange
        var users = _db.CreateUserRepository();
        var admin = new AdminService(users);
        var auth = new AuthService(users);
        await auth.RegisterAsync("promoted", "promoted@atap.com", "OldPassw0rd!");
        var userId = (await users.GetByUsernameAsync("promoted"))!.UserId;

        // Act
        var updated = await admin.UpdateUserAsync(userId, "promoted", "promoted@atap.com", "NewPassw0rd!", "admin");

        // Assert
        Assert.True(updated);
        Assert.Null(await auth.LoginAsync("promoted", "OldPassw0rd!"));
        var loggedIn = await auth.LoginAsync("promoted", "NewPassw0rd!");
        Assert.NotNull(loggedIn);
        Assert.Equal("admin", loggedIn.Role);
    }
}
