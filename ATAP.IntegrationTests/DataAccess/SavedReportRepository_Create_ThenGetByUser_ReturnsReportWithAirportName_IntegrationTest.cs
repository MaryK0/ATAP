using ATAP.BusinessLogic.DTO;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class SavedReportRepository_Create_ThenGetByUser_ReturnsReportWithAirportName_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public SavedReportRepository_Create_ThenGetByUser_ReturnsReportWithAirportName_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task CreateAsync_NewReport_IsListedWithAirportNameAndPeriod()
    {
        // Arrange
        var repository = _db.CreateSavedReportRepository();
        var userId = await _db.ScalarAsync<int>("SELECT user_id FROM dds.users WHERE username = 'admin'");
        var dto = new CreateSavedReportDto
        {
            Name = "Домодедово, 10 июня",
            AirportId = 2,
            DateFrom = new DateOnly(2026, 6, 10),
            DateTo = new DateOnly(2026, 6, 10)
        };

        // Act
        var id = await repository.CreateAsync(userId, dto);
        var reports = await repository.GetByUserAsync(userId);

        // Assert
        Assert.True(id > 0);
        var report = Assert.Single(reports);
        Assert.Equal(id, report.Id);
        Assert.Equal("Домодедово, 10 июня", report.Name);
        Assert.Equal(2, report.AirportId);
        Assert.Equal("Domodedovo", report.AirportName);
        Assert.Equal(new DateOnly(2026, 6, 10), report.DateFrom);
        Assert.Equal(new DateOnly(2026, 6, 10), report.DateTo);
    }
}
