using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Services;
using ATAP.DataAccess;
using ATAP.DataAccess.Factories;
using ATAP.DataAccess.Repositories;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

/// <summary>Фильтры карты (FR-06) на настоящей БД: страна регистрации судна берётся из витрины карты.</summary>
[Trait("Category", "Integration")]
public class MapService_GetCurrentFlights_FilterByCountry_IntegrationTest
    : IClassFixture<TestDatabaseFixture>, IClassFixture<TestRedisFixture>
{
    private readonly TestDatabaseFixture _db;
    private readonly TestRedisFixture _redis;

    public MapService_GetCurrentFlights_FilterByCountry_IntegrationTest(TestDatabaseFixture db, TestRedisFixture redis)
    {
        _db = db;
        _redis = redis;
    }

    [Fact]
    public async Task GetCurrentFlightsAsync_WithCountryFilter_ReturnsOnlyFlightsOfThatCountry()
    {
        // Arrange
        var repository = new PostgresMapRepository(_db.ConnectionString, new SqlQueryFactory(), _redis.Cache, new CacheSettings());
        var service = new MapService(repository, _redis.Cache);

        // Act
        var german = await service.GetCurrentFlightsAsync(new FlightFilter { Country = "germany" });
        var unknown = await service.GetCurrentFlightsAsync(new FlightFilter { Country = "Atlantis" });

        // Assert
        var flight = Assert.Single(german);
        Assert.Equal("DLH002", flight.Callsign);
        Assert.Equal("Germany", flight.Country);
        Assert.Empty(unknown);
    }
}
