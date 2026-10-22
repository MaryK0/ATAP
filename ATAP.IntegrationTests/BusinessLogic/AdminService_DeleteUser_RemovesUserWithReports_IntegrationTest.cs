using ATAP.BusinessLogic.Services;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class AdminService_DeleteUser_RemovesUserWithReports_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public AdminService_DeleteUser_RemovesUserWithReports_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task DeleteUserAsync_UserWithSavedReports_RemovesUserAndReportsAndKeepsAdmin()
    {
        // Arrange
        var service = new AdminService(_db.CreateUserRepository());
        var analystId = await _db.ScalarAsync<int>("SELECT user_id FROM dds.users WHERE username = 'analyst1'");

        // Act
        var deleted = await service.DeleteUserAsync(analystId);

        // Assert
        Assert.True(deleted);
        var remaining = await service.GetAllUsersAsync();
        Assert.Equal(new[] { "admin" }, remaining.Select(u => u.Username).ToArray());
        Assert.Equal(0L, await _db.ScalarAsync<long>("SELECT COUNT(*) FROM dds.saved_reports"));
    }
}
