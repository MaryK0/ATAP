using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

/// <summary>GetAllCongestionAsync (все аэропорты) отбирает записи, целиком лежащие в периоде: entry_time &gt;= start И exit_time &lt;= end.</summary>
[Trait("Category", "Integration")]
public class AnalyticsRepository_GetAllCongestion_EntryAndExitInsidePeriod_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public AnalyticsRepository_GetAllCongestion_EntryAndExitInsidePeriod_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task GetAllCongestionAsync_FullDayPeriod_ReturnsOnlyRecordsFullyInsidePeriodForAllAirports()
    {
        // Arrange
        var repository = _db.CreateAnalyticsRepository();
        var start = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await repository.GetAllCongestionAsync(start, end);

        // Assert: запись с входом до начала и записи, выходящие за конец периода, не попадают
        Assert.Equal(4, result.Count);
        // сортировка по времени входа, сначала новые: 12:30 (аэр. 1), 10:00 (аэр. 2), 07:06 и 00:00 (аэр. 1)
        Assert.Equal(new[] { 1, 2, 1, 1 }, result.Select(r => r.AirportId).ToArray());
        Assert.All(result, r => Assert.True(r.EntryTime >= start && r.ExitTime <= end));
        Assert.Equal(new[] { 1, 2 }, result.Select(r => r.AirportId).Distinct().OrderBy(x => x).ToArray());
    }
}
