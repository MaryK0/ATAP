using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class AnalyticsRepository_GetCongestion_AirportWithoutData_ReturnsEmpty_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public AnalyticsRepository_GetCongestion_AirportWithoutData_ReturnsEmpty_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task GetCongestionAsync_AirportWithoutRecords_ReturnsEmptyList()
    {
        // Arrange
        var repository = _db.CreateAnalyticsRepository();
        var start = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc);

        // Act (аэропорт 3 — Внуково: в тестовых данных записей нет)
        var result = await repository.GetCongestionAsync(3, start, end, includeDetails: true);

        // Assert
        Assert.Empty(result);
    }
}
