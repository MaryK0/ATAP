using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class SavedReportRepository_GetByUser_ReturnsOnlyOwnReports_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public SavedReportRepository_GetByUser_ReturnsOnlyOwnReports_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task GetByUserAsync_UserWithTwoReports_ReturnsThemAndOtherUserSeesNone()
    {
        // Arrange
        var repository = _db.CreateSavedReportRepository();
        var analystId = await _db.ScalarAsync<int>("SELECT user_id FROM dds.users WHERE username = 'analyst1'");
        var adminId = await _db.ScalarAsync<int>("SELECT user_id FROM dds.users WHERE username = 'admin'");

        // Act
        var analystReports = await repository.GetByUserAsync(analystId);
        var adminReports = await repository.GetByUserAsync(adminId);

        // Assert
        Assert.Equal(2, analystReports.Count);
        Assert.Contains(analystReports, r => r.Name == "Шереметьево, 10 июня" && r.AirportName == "Sheremetyevo");
        Assert.Contains(analystReports, r => r.Name == "Шереметьево, неделя"
            && r.DateFrom == new DateOnly(2026, 6, 4) && r.DateTo == new DateOnly(2026, 6, 10));
        Assert.Empty(adminReports);
    }
}
