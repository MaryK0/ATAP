using ATAP.BusinessLogic.Services;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

/// <summary>Критерий сценария 3: повторный запрос с теми же параметрами отвечает из кэша.</summary>
[Trait("Category", "Integration")]
public class AnalyticsService_GetCongestion_SecondCallServedFromRedis_IntegrationTest
    : IClassFixture<TestDatabaseFixture>, IClassFixture<TestRedisFixture>
{
    private readonly TestDatabaseFixture _db;
    private readonly TestRedisFixture _redis;

    public AnalyticsService_GetCongestion_SecondCallServedFromRedis_IntegrationTest(TestDatabaseFixture db, TestRedisFixture redis)
    {
        _db = db;
        _redis = redis;
    }

    [Fact]
    public async Task GetCongestionAsync_RepeatedRequest_ReturnsCachedReportEvenIfDatabaseChanged()
    {
        // Arrange
        var service = new AnalyticsService(_db.CreateAnalyticsRepository(), _redis.Cache);
        var start = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc);
        var first = await service.GetCongestionAsync(1, start, end);

        // Act: удаляем данные — повторный запрос не должен обращаться к БД
        await _db.ExecuteAsync("DELETE FROM mart.mart_airport_congestion");
        var second = await service.GetCongestionAsync(1, start, end);

        // Assert
        Assert.Equal(4, first.Entries.Count);
        Assert.Equal(4, second.Entries.Count);
        Assert.Equal(first.DepartureCount, second.DepartureCount);
        Assert.Equal(0L, await _db.ScalarAsync<long>("SELECT COUNT(*) FROM mart.mart_airport_congestion"));
    }
}
