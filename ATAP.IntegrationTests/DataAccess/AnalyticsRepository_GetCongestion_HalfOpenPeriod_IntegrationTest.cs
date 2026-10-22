using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

/// <summary>
/// Период запроса витрины полуоткрытый: entry_time &gt;= start И entry_time &lt; end.
/// Запись на самой границе начала входит, на границе конца — нет.
/// Преобразование «обе даты включаются» в такой интервал выполняет вызывающий слой (API/сервис).
/// </summary>
[Trait("Category", "Integration")]
public class AnalyticsRepository_GetCongestion_HalfOpenPeriod_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public AnalyticsRepository_GetCongestion_HalfOpenPeriod_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task GetCongestionAsync_FullDayPeriod_ReturnsRecordsInsideAndExcludesBoundaryOutside()
    {
        // Arrange
        var repository = _db.CreateAnalyticsRepository();
        var start = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await repository.GetCongestionAsync(1, start, end, includeDetails: true);

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal(1, result.Count(r => r.IsDeparture));
        Assert.Equal(3, result.Count(r => r.IsArrival));
        Assert.Equal(new[] { "AFL123", "BAW001", "DLH002", "AFL123" }, result.Select(r => r.Callsign).ToArray());
        Assert.Equal(start, result.First().EntryTime);
        Assert.Equal(new DateTime(2026, 6, 10, 23, 59, 59, DateTimeKind.Utc), result.Last().EntryTime);
        Assert.DoesNotContain(result, r => r.EntryTime < start || r.EntryTime >= end);
    }
}
