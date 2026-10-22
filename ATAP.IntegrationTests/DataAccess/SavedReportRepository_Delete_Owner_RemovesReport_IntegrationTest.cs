using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class SavedReportRepository_Delete_Owner_RemovesReport_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public SavedReportRepository_Delete_Owner_RemovesReport_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task DeleteAsync_OwnReport_RemovesOnlyThatReport()
    {
        // Arrange
        var repository = _db.CreateSavedReportRepository();
        var analystId = await _db.ScalarAsync<int>("SELECT user_id FROM dds.users WHERE username = 'analyst1'");
        var reports = await repository.GetByUserAsync(analystId);
        var toDelete = reports.First();
        var toKeep = reports.Last();

        // Act
        await repository.DeleteAsync(toDelete.Id, analystId);

        // Assert
        var remaining = await repository.GetByUserAsync(analystId);
        var left = Assert.Single(remaining);
        Assert.Equal(toKeep.Id, left.Id);
    }
}
