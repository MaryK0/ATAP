using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Services;
using ATAP.DataAccess;
using ATAP.DataAccess.Factories;
using ATAP.DataAccess.Repositories;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class MapService_GetCurrentFlights_FilterByAltitudeRange_IntegrationTest
    : IClassFixture<TestDatabaseFixture>, IClassFixture<TestRedisFixture>
{
    private readonly TestDatabaseFixture _db;
    private readonly TestRedisFixture _redis;

    public MapService_GetCurrentFlights_FilterByAltitudeRange_IntegrationTest(TestDatabaseFixture db, TestRedisFixture redis)
    {
        _db = db;
        _redis = redis;
    }

    [Fact]
    public async Task GetCurrentFlightsAsync_WithAltitudeRange_ReturnsOnlyFlightsInsideRangeBoundariesIncluded()
    {
        // Arrange (высоты в тестовых данных: AFL123 — 10000 м, BAW001 — 3200 м, DLH002 — 2800 м)
        var repository = new PostgresMapRepository(_db.ConnectionString, new SqlQueryFactory(), _redis.Cache, new CacheSettings());
        var service = new MapService(repository, _redis.Cache);

        // Act
        var low = await service.GetCurrentFlightsAsync(new FlightFilter { MinAltitude = 2000, MaxAltitude = 3500 });
        var onBoundary = await service.GetCurrentFlightsAsync(new FlightFilter { MinAltitude = 3200, MaxAltitude = 3200 });

        // Assert
        Assert.Equal(new[] { "BAW001", "DLH002" }, low.Select(f => f.Callsign).OrderBy(c => c).ToArray());
        var flight = Assert.Single(onBoundary);
        Assert.Equal("BAW001", flight.Callsign);
    }
}
