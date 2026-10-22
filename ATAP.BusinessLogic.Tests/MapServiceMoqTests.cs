using Xunit;
using Moq;
using ATAP.BusinessLogic.Services;
using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.BusinessLogic.Tests.TestData.Mothers;
using Allure.Xunit.Attributes;

namespace ATAP.BusinessLogic.Tests;

/// <summary>
/// "Лондонский" вариант тестов кэширования в <see cref="MapService"/>. Сравните с classical-версией:
/// <see cref="MapServiceTests.GetAirportsAsync_WithCache_ShouldReturnFromCache"/>. В mock-стиле мы можем
/// прямо утверждать "репозиторий НЕ был вызван при попадании в кэш" — то, что сложно доказать
/// в classical-тесте без inline-fake со счётчиком вызовов.
/// </summary>
[AllureSuite("MapService (London/mock style)")]
public class MapServiceMoqTests
{
    [Fact]
    [AllureFeature("Airports cache")]
    public async Task GetAirportsAsync_WhenCacheMiss_ShouldQueryRepositoryAndPopulateCache()
    {
        // Arrange
        var mapRepoMock = new Mock<IMapRepository>();
        mapRepoMock.Setup(r => r.GetAirportsAsync()).ReturnsAsync(Airports.Default());
        var cacheMock = new Mock<ICacheRepository>();
        cacheMock.Setup(c => c.GetAsync<List<ATAP.BusinessLogic.DTO.AirportDto>>("airports:all"))
            .ReturnsAsync((List<ATAP.BusinessLogic.DTO.AirportDto>?)null);
        var service = new MapService(mapRepoMock.Object, cacheMock.Object);

        // Act
        var result = await service.GetAirportsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        mapRepoMock.Verify(r => r.GetAirportsAsync(), Times.Once);
        cacheMock.Verify(c => c.SetAsync("airports:all", It.IsAny<List<ATAP.BusinessLogic.DTO.AirportDto>>(), 3600), Times.Once);
    }

    [Fact]
    [AllureFeature("Airports cache")]
    public async Task GetAirportsAsync_WhenCacheHit_ShouldNeverQueryRepository()
    {
        // Arrange
        var cachedAirports = new List<ATAP.BusinessLogic.DTO.AirportDto>
        {
            new() { Id = 1, Name = "Из кэша", IcaoCode = "ZZZZ" }
        };
        var mapRepoMock = new Mock<IMapRepository>(MockBehavior.Strict); // Strict: любой незапланированный вызов -> исключение в тесте
        var cacheMock = new Mock<ICacheRepository>();
        cacheMock.Setup(c => c.GetAsync<List<ATAP.BusinessLogic.DTO.AirportDto>>("airports:all"))
            .ReturnsAsync(cachedAirports);
        var service = new MapService(mapRepoMock.Object, cacheMock.Object);

        // Act
        var result = await service.GetAirportsAsync();

        // Assert
        Assert.Single(result);
        mapRepoMock.Verify(r => r.GetAirportsAsync(), Times.Never);
    }
}
