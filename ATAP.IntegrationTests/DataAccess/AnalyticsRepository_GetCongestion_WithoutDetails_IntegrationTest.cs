using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class AnalyticsRepository_GetCongestion_WithoutDetails_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public AnalyticsRepository_GetCongestion_WithoutDetails_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task GetCongestionAsync_WithoutDetails_ReturnsCountsAndTypesButNoCallsignOrTimes()
    {
        // Arrange
        var repository = _db.CreateAnalyticsRepository();
        var start = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await repository.GetCongestionAsync(1, start, end, includeDetails: false);

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal(1, result.Count(r => r.IsDeparture));
        Assert.Equal(3, result.Count(r => r.IsArrival));
        Assert.All(result, r => Assert.Equal(string.Empty, r.Callsign));
        Assert.All(result, r => Assert.Equal(default, r.EntryTime));
    }
}
