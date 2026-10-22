using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class SavedReportRepository_Delete_ForeignUser_KeepsReport_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public SavedReportRepository_Delete_ForeignUser_KeepsReport_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task DeleteAsync_ReportOfAnotherUser_DoesNotDeleteIt()
    {
        // Arrange
        var repository = _db.CreateSavedReportRepository();
        var analystId = await _db.ScalarAsync<int>("SELECT user_id FROM dds.users WHERE username = 'analyst1'");
        var adminId = await _db.ScalarAsync<int>("SELECT user_id FROM dds.users WHERE username = 'admin'");
        var reportId = (await repository.GetByUserAsync(analystId)).First().Id;

        // Act
        await repository.DeleteAsync(reportId, adminId);

        // Assert
        Assert.Equal(2, (await repository.GetByUserAsync(analystId)).Count);
        Assert.Equal(1L, await _db.ScalarAsync<long>(
            "SELECT COUNT(*) FROM dds.saved_reports WHERE id = @id", ("@id", reportId)));
    }
}
