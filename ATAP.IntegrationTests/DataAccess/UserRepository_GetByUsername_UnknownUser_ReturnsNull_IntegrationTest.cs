using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class UserRepository_GetByUsername_UnknownUser_ReturnsNull_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public UserRepository_GetByUsername_UnknownUser_ReturnsNull_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task GetByUsernameAsync_UnknownUser_ReturnsNull()
    {
        // Arrange
        var repository = _db.CreateUserRepository();

        // Act
        var result = await repository.GetByUsernameAsync("no_such_user");

        // Assert
        Assert.Null(result);
    }
}
