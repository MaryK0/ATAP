using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class UserRepository_Delete_UnknownUser_ReturnsFalse_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public UserRepository_Delete_UnknownUser_ReturnsFalse_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task DeleteAsync_NonexistentId_ReturnsFalseAndKeepsOtherUsers()
    {
        // Arrange
        var repository = _db.CreateUserRepository();
        var usersBefore = await _db.ScalarAsync<long>("SELECT COUNT(*) FROM dds.users");

        // Act
        var deleted = await repository.DeleteAsync(999_999);

        // Assert
        Assert.False(deleted);
        Assert.Equal(usersBefore, await _db.ScalarAsync<long>("SELECT COUNT(*) FROM dds.users"));
    }
}
