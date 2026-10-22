using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class UserRepository_GetByUsername_ReturnsSeededAdmin_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public UserRepository_GetByUsername_ReturnsSeededAdmin_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task GetByUsernameAsync_ExistingUser_ReturnsUserWithRoleAndEmail()
    {
        // Arrange
        var repository = _db.CreateUserRepository();

        // Act
        var result = await repository.GetByUsernameAsync("admin");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("admin", result.Username);
        Assert.Equal("admin@atap.com", result.Email);
        Assert.Equal("admin", result.Role);
        Assert.True(result.UserId > 0);
    }
}
