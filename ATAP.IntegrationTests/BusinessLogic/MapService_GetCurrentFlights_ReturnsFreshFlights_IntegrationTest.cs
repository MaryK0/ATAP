using ATAP.BusinessLogic.Services;
using ATAP.DataAccess;
using ATAP.DataAccess.Factories;
using ATAP.DataAccess.Repositories;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class MapService_GetCurrentFlights_ReturnsFreshFlights_IntegrationTest
    : IClassFixture<TestDatabaseFixture>, IClassFixture<TestRedisFixture>
{
    private readonly TestDatabaseFixture _db;
    private readonly TestRedisFixture _redis;

    public MapService_GetCurrentFlights_ReturnsFreshFlights_IntegrationTest(TestDatabaseFixture db, TestRedisFixture redis)
    {
        _db = db;
        _redis = redis;
    }

    [Fact]
    public async Task GetCurrentFlightsAsync_SeededDatabase_ReturnsThreeFlightsMatchingCounterOnMap()
    {
        // Arrange
        var repository = new PostgresMapRepository(_db.ConnectionString, new SqlQueryFactory(), _redis.Cache, new CacheSettings());
        var service = new MapService(repository, _redis.Cache);

        // Act
        var flights = await service.GetCurrentFlightsAsync();

        // Assert
        Assert.Equal(3, flights.Count);
        Assert.All(flights, f => Assert.False(string.IsNullOrEmpty(f.Callsign)));
        Assert.Contains(flights, f => f.Callsign == "DLH002" && f.Altitude == 2800);
    }
}
