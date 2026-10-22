using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class UserRepository_GetAll_ReturnsSeededUsers_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public UserRepository_GetAll_ReturnsSeededUsers_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task GetAllAsync_SeededDatabase_ReturnsAdminAndAnalystOrderedById()
    {
        // Arrange
        var repository = _db.CreateUserRepository();

        // Act
        var users = await repository.GetAllAsync();

        // Assert
        Assert.Equal(new[] { "admin", "analyst1" }, users.Select(u => u.Username).ToArray());
        Assert.Equal(new[] { "admin", "analyst" }, users.Select(u => u.Role).ToArray());
        Assert.True(users[0].UserId < users[1].UserId);
    }
}
