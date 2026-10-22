using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Services;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class SavedReportService_Save_ThenGetByUser_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public SavedReportService_Save_ThenGetByUser_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task SaveAsync_ValidReport_AppearsInListAndCanBeDeletedByOwner()
    {
        // Arrange
        var service = new SavedReportService(_db.CreateSavedReportRepository());
        var userId = await _db.ScalarAsync<int>("SELECT user_id FROM dds.users WHERE username = 'admin'");
        var dto = new CreateSavedReportDto
        {
            Name = "Шереметьево, 10 июня",
            AirportId = 1,
            DateFrom = new DateOnly(2026, 6, 10),
            DateTo = new DateOnly(2026, 6, 10)
        };

        // Act
        await service.SaveAsync(userId, dto);
        var listed = await service.GetByUserAsync(userId);
        await service.DeleteAsync(listed.Single().Id, userId);
        var afterDelete = await service.GetByUserAsync(userId);

        // Assert
        var report = Assert.Single(listed);
        Assert.Equal("Шереметьево, 10 июня", report.Name);
        Assert.Equal("Sheremetyevo", report.AirportName);
        Assert.Empty(afterDelete);
    }
}
