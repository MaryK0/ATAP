using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class UserRepository_Update_ChangesRoleAndEmail_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public UserRepository_Update_ChangesRoleAndEmail_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task UpdateAsync_ExistingUser_PersistsNewRoleAndEmail()
    {
        // Arrange
        var repository = _db.CreateUserRepository();
        var user = await repository.GetByUsernameAsync("analyst1");
        Assert.NotNull(user);
        user.Role = "admin";
        user.Email = "analyst1.new@atap.com";

        // Act
        var updated = await repository.UpdateAsync(user);

        // Assert
        Assert.True(updated);
        var reloaded = await repository.GetByUsernameAsync("analyst1");
        Assert.NotNull(reloaded);
        Assert.Equal("admin", reloaded.Role);
        Assert.Equal("analyst1.new@atap.com", reloaded.Email);
    }
}
