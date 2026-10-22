using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Services;
using ATAP.DataAccess;
using ATAP.DataAccess.Factories;
using ATAP.DataAccess.Repositories;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class MapService_GetAirports_SecondCallServedFromRedis_IntegrationTest
    : IClassFixture<TestDatabaseFixture>, IClassFixture<TestRedisFixture>
{
    private readonly TestDatabaseFixture _db;
    private readonly TestRedisFixture _redis;

    public MapService_GetAirports_SecondCallServedFromRedis_IntegrationTest(TestDatabaseFixture db, TestRedisFixture redis)
    {
        _db = db;
        _redis = redis;
    }

    [Fact]
    public async Task GetAirportsAsync_FirstCallFillsCache_InvalidateClearsIt()
    {
        // Arrange
        var repository = new PostgresMapRepository(_db.ConnectionString, new SqlQueryFactory(), _redis.Cache, new CacheSettings());
        var service = new MapService(repository, _redis.Cache);

        // Act
        var first = await service.GetAirportsAsync();
        var cachedAfterFirst = await _redis.Cache.GetAsync<List<AirportDto>>("airports:all");
        await service.InvalidateAirportsCacheAsync();
        var cachedAfterInvalidate = await _redis.Cache.GetAsync<List<AirportDto>>("airports:all");

        // Assert
        Assert.Equal(3, first.Count);
        Assert.Equal(new[] { "UUDD", "UUEE", "UUWW" }, first.Select(a => a.IcaoCode).OrderBy(c => c).ToArray());
        Assert.NotNull(cachedAfterFirst);
        Assert.Equal(3, cachedAfterFirst.Count);
        Assert.Null(cachedAfterInvalidate);
    }
}
