using Xunit;
using ATAP.BusinessLogic.Services;
using ATAP.BusinessLogic.Tests.Mocks;
using ATAP.BusinessLogic.Tests.TestData.Mothers;
using ATAP.BusinessLogic.DTO;
using Allure.Xunit.Attributes;

namespace ATAP.BusinessLogic.Tests;

/// <summary>
/// Classical (state-based) unit-тесты для <see cref="MapService"/> с самописными fake-репозиториями.
/// Для той же логики кэширования в "Лондонском" (mock+verify) стиле см. <see cref="MapServiceMoqTests"/>.
/// </summary>
[AllureSuite("MapService")]
public class MapServiceTests
{
    [Fact]
    [AllureFeature("Flights")]
    public async Task GetCurrentFlightsAsync_ShouldReturnFlights()
    {
        // Arrange
        var mockRepo = new MockMapRepository();
        var service = new MapService(mockRepo);

        // Act
        var result = await service.GetCurrentFlightsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("AFL123", result[0].Callsign);
    }

    [Fact]
    [AllureFeature("Flights")]
    public async Task GetCurrentFlightsAsync_WhenRepositoryEmpty_ShouldReturnEmptyList()
    {
        // Arrange (граничное значение: пустой источник данных)
        var mockRepo = new MockMapRepository(empty: true);
        var service = new MapService(mockRepo);

        // Act
        var result = await service.GetCurrentFlightsAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    [AllureFeature("Airports")]
    public async Task GetAirportsAsync_ShouldReturnAirports()
    {
        // Arrange
        var mockRepo = new MockMapRepository();
        var service = new MapService(mockRepo);

        // Act
        var result = await service.GetAirportsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Шереметьево", result[0].Name);
    }

    [Fact]
    [AllureFeature("Airports")]
    public async Task GetAirportsAsync_WhenRepositoryEmpty_ShouldReturnEmptyList()
    {
        // Arrange (граничное значение: пустой источник данных — симметрично GetCurrentFlightsAsync)
        var mockRepo = new MockMapRepository(empty: true);
        var service = new MapService(mockRepo);

        // Act
        var result = await service.GetAirportsAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    [AllureFeature("Airports")]
    public async Task GetAirportsAsync_WithCache_ShouldReturnFromCache()
    {
        // Arrange
        var mockRepo = new MockMapRepository();
        var mockCache = new MockCacheRepository();
        var service = new MapService(mockRepo, mockCache);
        var expectedAirports = new List<AirportDto>
        {
            new() { Id = 1, Name = "Cached Airport", IcaoCode = "XXXX", Latitude = 0, Longitude = 0 }
        };
        await mockCache.SetAsync("airports:all", expectedAirports, 3600);

        // Act
        var result = await service.GetAirportsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Cached Airport", result[0].Name);
    }

    [Fact]
    [AllureFeature("Airports")]
    public async Task InvalidateAirportsCacheAsync_ShouldRemoveCachedEntry()
    {
        // Arrange
        var mockRepo = new MockMapRepository();
        var mockCache = new MockCacheRepository();
        var service = new MapService(mockRepo, mockCache);
        await mockCache.SetAsync("airports:all", Airports.Default(), 3600);

        // Act
        await service.InvalidateAirportsCacheAsync();

        // Assert
        var cached = await mockCache.GetAsync<List<AirportDto>>("airports:all");
        Assert.Null(cached);
    }

    [Fact]
    [AllureFeature("Airports")]
    public async Task InvalidateAirportsCacheAsync_WithoutCacheConfigured_ShouldNotThrow()
    {
        // Arrange (граничный случай: ICacheRepository не передан — MapService без кэша)
        var mockRepo = new MockMapRepository();
        var service = new MapService(mockRepo, cacheRepository: null);

        // Act
        var exception = await Record.ExceptionAsync(() => service.InvalidateAirportsCacheAsync());

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    [AllureFeature("Aircrafts")]
    public async Task InvalidateAircraftsCacheAsync_ShouldRemoveCachedEntry()
    {
        // Arrange
        var mockRepo = new MockMapRepository();
        var mockCache = new MockCacheRepository();
        var service = new MapService(mockRepo, mockCache);
        await mockCache.SetAsync("aircrafts:all", new List<string> { "A320" }, 3600);

        // Act
        await service.InvalidateAircraftsCacheAsync();

        // Assert
        var cached = await mockCache.GetAsync<List<string>>("aircrafts:all");
        Assert.Null(cached);
    }

    [Fact]
    [AllureFeature("Aircrafts")]
    public async Task InvalidateAircraftsCacheAsync_WithoutCacheConfigured_ShouldNotThrow()
    {
        // Arrange (граничный случай: ICacheRepository не передан)
        var mockRepo = new MockMapRepository();
        var service = new MapService(mockRepo, cacheRepository: null);

        // Act
        var exception = await Record.ExceptionAsync(() => service.InvalidateAircraftsCacheAsync());

        // Assert
        Assert.Null(exception);
    }
}
