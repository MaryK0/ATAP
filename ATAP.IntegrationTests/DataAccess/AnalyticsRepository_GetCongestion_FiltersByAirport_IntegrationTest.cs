using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class AnalyticsRepository_GetCongestion_FiltersByAirport_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public AnalyticsRepository_GetCongestion_FiltersByAirport_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task GetCongestionAsync_SecondAirport_ReturnsOnlyItsOwnRecord()
    {
        // Arrange
        var repository = _db.CreateAnalyticsRepository();
        var start = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await repository.GetCongestionAsync(2, start, end, includeDetails: true);

        // Assert
        var entry = Assert.Single(result);
        Assert.Equal(2, entry.AirportId);
        Assert.Equal("DLH002", entry.Callsign);
        Assert.True(entry.IsArrival);
        Assert.Equal(300, entry.DurationSeconds);
    }
}
