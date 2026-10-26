using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Services;
using ATAP.DataAccess;
using ATAP.DataAccess.Factories;
using ATAP.DataAccess.Repositories;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class MapService_GetCurrentFlights_CombinedFilters_IntegrationTest
    : IClassFixture<TestDatabaseFixture>, IClassFixture<TestRedisFixture>
{
    private readonly TestDatabaseFixture _db;
    private readonly TestRedisFixture _redis;

    public MapService_GetCurrentFlights_CombinedFilters_IntegrationTest(TestDatabaseFixture db, TestRedisFixture redis)
    {
        _db = db;
        _redis = redis;
    }

    [Fact]
    public async Task GetCurrentFlightsAsync_WithAreaAltitudeAndCountry_ReturnsOnlyFlightsMatchingAllFilters()
    {
        // Arrange (область Европы, высота до 3000 м, страна Германия — подходит только DLH002: 2800 м)
        var repository = new PostgresMapRepository(_db.ConnectionString, new SqlQueryFactory(), _redis.Cache, new CacheSettings());
        var service = new MapService(repository, _redis.Cache);
        var filter = new FlightFilter
        {
            Area = new BoundingBox(-1, 49, 9, 52),
            MaxAltitude = 3000,
            Country = "Germany"
        };

        // Act
        var result = await service.GetCurrentFlightsAsync(filter);

        // Assert
        var flight = Assert.Single(result);
        Assert.Equal("DLH002", flight.Callsign);
        Assert.Equal(2800, flight.Altitude);
    }
}
