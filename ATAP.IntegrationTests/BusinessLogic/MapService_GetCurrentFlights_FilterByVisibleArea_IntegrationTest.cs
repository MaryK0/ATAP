using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Services;
using ATAP.DataAccess;
using ATAP.DataAccess.Factories;
using ATAP.DataAccess.Repositories;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class MapService_GetCurrentFlights_FilterByVisibleArea_IntegrationTest
    : IClassFixture<TestDatabaseFixture>, IClassFixture<TestRedisFixture>
{
    private readonly TestDatabaseFixture _db;
    private readonly TestRedisFixture _redis;

    public MapService_GetCurrentFlights_FilterByVisibleArea_IntegrationTest(TestDatabaseFixture db, TestRedisFixture redis)
    {
        _db = db;
        _redis = redis;
    }

    [Fact]
    public async Task GetCurrentFlightsAsync_WithVisibleArea_ReturnsOnlyFlightsInsideArea()
    {
        // Arrange (положения в тестовых данных: AFL123 — Москва 55.75; 37.62, BAW001 — Лондон 51.47; -0.45, DLH002 — Франкфурт 50.04; 8.56)
        var repository = new PostgresMapRepository(_db.ConnectionString, new SqlQueryFactory(), _redis.Cache, new CacheSettings());
        var service = new MapService(repository, _redis.Cache);

        // Act
        var europe = await service.GetCurrentFlightsAsync(new FlightFilter { Area = new BoundingBox(-1, 49, 9, 52) });
        var moscow = await service.GetCurrentFlightsAsync(new FlightFilter { Area = new BoundingBox(37, 55, 38, 56) });

        // Assert
        Assert.Equal(new[] { "BAW001", "DLH002" }, europe.Select(f => f.Callsign).OrderBy(c => c).ToArray());
        var flight = Assert.Single(moscow);
        Assert.Equal("AFL123", flight.Callsign);
    }
}
