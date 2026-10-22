using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class UserRepository_Create_PersistsUser_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public UserRepository_Create_PersistsUser_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task CreateAsync_NewUser_IsStoredAndFoundByUsernameAndEmail()
    {
        // Arrange
        var repository = _db.CreateUserRepository();
        var newUser = new UserDb
        {
            Username = "created_user",
            Email = "created_user@test.com",
            PasswordHash = "hash-value",
            Role = "analyst"
        };

        // Act
        var userId = await repository.CreateAsync(newUser);

        // Assert
        Assert.True(userId > 0);
        var byUsername = await repository.GetByUsernameAsync("created_user");
        var byEmail = await repository.GetByEmailAsync("created_user@test.com");
        Assert.NotNull(byUsername);
        Assert.Equal(userId, byUsername.UserId);
        Assert.Equal("analyst", byUsername.Role);
        Assert.Equal("hash-value", byUsername.PasswordHash);
        Assert.NotNull(byEmail);
        Assert.Equal(userId, byEmail.UserId);
    }
}
