using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class UserRepository_Delete_CascadesSavedReports_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public UserRepository_Delete_CascadesSavedReports_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task DeleteAsync_UserWithReports_RemovesUserAndTheirReports()
    {
        // Arrange
        var repository = _db.CreateUserRepository();
        var user = await repository.GetByUsernameAsync("analyst1");
        Assert.NotNull(user);
        var reportsBefore = await _db.ScalarAsync<long>(
            "SELECT COUNT(*) FROM dds.saved_reports WHERE user_id = @id", ("@id", user.UserId));
        Assert.Equal(2, reportsBefore);

        // Act
        var deleted = await repository.DeleteAsync(user.UserId);

        // Assert
        Assert.True(deleted);
        Assert.Null(await repository.GetByUsernameAsync("analyst1"));
        var reportsAfter = await _db.ScalarAsync<long>(
            "SELECT COUNT(*) FROM dds.saved_reports WHERE user_id = @id", ("@id", user.UserId));
        Assert.Equal(0, reportsAfter);
    }
}
