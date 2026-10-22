using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.DataAccess;
using ATAP.DataAccess.Factories;
using ATAP.DataAccess.Repositories;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class MapRepository_GetAirports_ReadsDatabaseThenCache_IntegrationTest
    : IClassFixture<TestDatabaseFixture>, IClassFixture<TestRedisFixture>
{
    private readonly TestDatabaseFixture _db;
    private readonly TestRedisFixture _redis;

    public MapRepository_GetAirports_ReadsDatabaseThenCache_IntegrationTest(TestDatabaseFixture db, TestRedisFixture redis)
    {
        _db = db;
        _redis = redis;
    }

    [Fact]
    public async Task GetAirportsAsync_FirstCallReadsDatabaseAndFillsCache_SecondCallServedFromRedis()
    {
        // Arrange
        var repository = new PostgresMapRepository(_db.ConnectionString, new SqlQueryFactory(), _redis.Cache, new CacheSettings());
        Assert.Null(await _redis.Cache.GetAsync<List<AirportDb>>("airports:all:db"));

        // Act
        var first = await repository.GetAirportsAsync();
        await _db.ExecuteAsync("DELETE FROM mart.mart_airport_congestion");
        await _db.ExecuteAsync("DELETE FROM dds.saved_reports");
        await _db.ExecuteAsync("DELETE FROM dds.airports WHERE icao_code = 'UUWW'");
        var second = await repository.GetAirportsAsync();

        // Assert: данные из БД; после удаления строки из БД вторая выдача всё равно полная — она из кэша
        Assert.Equal(new[] { "UUDD", "UUEE", "UUWW" }, first.Select(a => a.IcaoCode).OrderBy(c => c).ToArray());
        Assert.NotNull(await _redis.Cache.GetAsync<List<AirportDb>>("airports:all:db"));
        Assert.Equal(3, second.Count);
        Assert.Equal(2L, await _db.ScalarAsync<long>("SELECT COUNT(*) FROM dds.airports"));
    }
}
