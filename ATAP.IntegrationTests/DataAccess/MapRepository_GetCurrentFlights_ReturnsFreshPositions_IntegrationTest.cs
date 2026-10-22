using ATAP.DataAccess;
using ATAP.DataAccess.Factories;
using ATAP.DataAccess.Repositories;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

/// <summary>Карта берёт рейсы из материализованного представления: только позиции за последние 15 минут на момент обновления.</summary>
[Trait("Category", "Integration")]
public class MapRepository_GetCurrentFlights_ReturnsFreshPositions_IntegrationTest
    : IClassFixture<TestDatabaseFixture>, IClassFixture<TestRedisFixture>
{
    private readonly TestDatabaseFixture _db;
    private readonly TestRedisFixture _redis;

    public MapRepository_GetCurrentFlights_ReturnsFreshPositions_IntegrationTest(TestDatabaseFixture db, TestRedisFixture redis)
    {
        _db = db;
        _redis = redis;
    }

    [Fact]
    public async Task GetCurrentFlightsAsync_SeededDatabase_ReturnsThreeFreshFlightsWithoutStaleOne()
    {
        // Arrange
        var repository = new PostgresMapRepository(_db.ConnectionString, new SqlQueryFactory(), _redis.Cache, new CacheSettings());

        // Act
        var flights = await repository.GetCurrentFlightsAsync();

        // Assert
        Assert.Equal(new[] { "AFL123", "BAW001", "DLH002" }, flights.Select(f => f.Callsign).OrderBy(c => c).ToArray());
        Assert.DoesNotContain(flights, f => f.Callsign == "SWR003");
        var british = flights.Single(f => f.Callsign == "BAW001");
        Assert.Equal(51.470, british.Latitude, 3);
        Assert.Equal(-0.454, british.Longitude, 3);
        Assert.Equal(3200, british.Altitude);
    }
}
