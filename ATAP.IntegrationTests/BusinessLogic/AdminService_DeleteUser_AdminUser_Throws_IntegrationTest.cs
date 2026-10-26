using ATAP.BusinessLogic.Services;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class AdminService_DeleteUser_AdminUser_Throws_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public AdminService_DeleteUser_AdminUser_Throws_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task DeleteUserAsync_AdminUser_ThrowsAndLeavesUsersAndReportsUntouched()
    {
        // Arrange (FR-16: администратора удалить нельзя, в том числе самому себе)
        var service = new AdminService(_db.CreateUserRepository());
        var adminId = await _db.ScalarAsync<int>("SELECT user_id FROM dds.users WHERE username = 'admin'");
        var usersBefore = await _db.ScalarAsync<long>("SELECT COUNT(*) FROM dds.users");
        var reportsBefore = await _db.ScalarAsync<long>("SELECT COUNT(*) FROM dds.saved_reports");

        // Act
        var error = await Assert.ThrowsAsync<AdminDeletionForbiddenException>(() => service.DeleteUserAsync(adminId));

        // Assert
        Assert.Contains("admin", error.Message);
        Assert.Equal(usersBefore, await _db.ScalarAsync<long>("SELECT COUNT(*) FROM dds.users"));
        Assert.Equal(reportsBefore, await _db.ScalarAsync<long>("SELECT COUNT(*) FROM dds.saved_reports"));
        Assert.Equal(1L, await _db.ScalarAsync<long>("SELECT COUNT(*) FROM dds.users WHERE username = 'admin'"));
    }
}
