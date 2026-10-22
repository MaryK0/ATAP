using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Services;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class SavedReportService_Save_InvalidInput_ThrowsAndStoresNothing_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public SavedReportService_Save_InvalidInput_ThrowsAndStoresNothing_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task SaveAsync_BlankNameOrInvertedPeriod_ThrowsArgumentExceptionAndWritesNothing()
    {
        // Arrange
        var service = new SavedReportService(_db.CreateSavedReportRepository());
        var userId = await _db.ScalarAsync<int>("SELECT user_id FROM dds.users WHERE username = 'admin'");
        var before = await _db.ScalarAsync<long>("SELECT COUNT(*) FROM dds.saved_reports");
        var blankName = new CreateSavedReportDto { Name = "   ", AirportId = 1, DateFrom = new DateOnly(2026, 6, 10), DateTo = new DateOnly(2026, 6, 10) };
        var inverted = new CreateSavedReportDto { Name = "Отчёт", AirportId = 1, DateFrom = new DateOnly(2026, 6, 11), DateTo = new DateOnly(2026, 6, 10) };

        // Act
        var nameError = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(userId, blankName));
        var periodError = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(userId, inverted));

        // Assert
        Assert.Contains("Название", nameError.Message);
        Assert.Contains("Дата начала", periodError.Message);
        Assert.Equal(before, await _db.ScalarAsync<long>("SELECT COUNT(*) FROM dds.saved_reports"));
    }
}
