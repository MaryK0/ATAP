using Xunit;
using ATAP.BusinessLogic.DTO;
using Allure.Xunit.Attributes;

namespace ATAP.BusinessLogic.Tests;

/// <summary>
/// Unit-тесты фильтров карты (FR-06, вариант использования «Задать фильтры на карте»): область, высота, страна.
/// Техника: граничные значения (высота и координаты ровно на границе), эквивалентное разбиение
/// (внутри / снаружи), область через меридиан 180°, комбинирование фильтров.
/// </summary>
[AllureSuite("FlightFilter")]
public class FlightFilterTests
{
    private static FlightDto Flight(double lat, double lon, int altitude, string? country = "Germany") => new()
    {
        Id = 1, Callsign = "TEST01", Latitude = lat, Longitude = lon, Altitude = altitude, Country = country
    };

    [Fact]
    [AllureFeature("Map filters")]
    public void Matches_WithEmptyFilter_ShouldAcceptAnyFlight()
    {
        // Arrange
        var filter = new FlightFilter();

        // Act
        var result = filter.Matches(Flight(-80, 170, 12000, country: null));

        // Assert
        Assert.True(filter.IsEmpty);
        Assert.True(result);
    }

    [Theory]
    [AllureFeature("Map filters")]
    [InlineData(55.5, 37.5, true)]   // внутри области
    [InlineData(55.0, 37.0, true)]   // угол области — граница входит
    [InlineData(56.0, 38.0, true)]   // противоположный угол
    [InlineData(54.99, 37.5, false)] // южнее
    [InlineData(55.5, 38.01, false)] // восточнее
    public void Matches_WithVisibleArea_ShouldIncludeBoundaryAndExcludeOutside(double lat, double lon, bool expected)
    {
        // Arrange (видимая область над Москвой: lon 37..38, lat 55..56)
        var filter = new FlightFilter { Area = new BoundingBox(37, 55, 38, 56) };

        // Act
        var result = filter.Matches(Flight(lat, lon, 3000));

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [AllureFeature("Map filters")]
    [InlineData(-17.0, 179.5, true)]   // восточнее 170° в. д.
    [InlineData(-17.0, -179.5, true)]  // за меридианом 180°, западнее 170° з. д.
    [InlineData(-17.0, 0.0, false)]    // далеко от области
    public void Matches_WithAreaCrossingAntimeridian_ShouldWrapAroundLongitude(double lat, double lon, bool expected)
    {
        // Arrange (область пересекает меридиан 180°: minLon 170 больше maxLon -170)
        var filter = new FlightFilter { Area = new BoundingBox(170, -20, -170, -10) };

        // Act
        var result = filter.Matches(Flight(lat, lon, 3000));

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [AllureFeature("Map filters")]
    [InlineData(999, false)]    // ниже диапазона
    [InlineData(1000, true)]    // нижняя граница входит
    [InlineData(2500, true)]    // внутри
    [InlineData(3000, true)]    // верхняя граница входит
    [InlineData(3001, false)]   // выше диапазона
    public void Matches_WithAltitudeRange_ShouldIncludeBoundaries(int altitude, bool expected)
    {
        // Arrange
        var filter = new FlightFilter { MinAltitude = 1000, MaxAltitude = 3000 };

        // Act
        var result = filter.Matches(Flight(50, 8, altitude));

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    [AllureFeature("Map filters")]
    public void Matches_WithOnlyMinAltitude_ShouldNotLimitFromAbove()
    {
        // Arrange (одно из двух значений диапазона не задано — ограничение только с одной стороны)
        var filter = new FlightFilter { MinAltitude = 10000 };

        // Act + Assert
        Assert.True(filter.Matches(Flight(50, 8, 12000)));
        Assert.False(filter.Matches(Flight(50, 8, 9000)));
    }

    [Fact]
    [AllureFeature("Map filters")]
    public void Matches_WithCountry_ShouldIgnoreCaseAndRejectUnknownCountry()
    {
        // Arrange
        var filter = new FlightFilter { Country = "germany" };

        // Act + Assert
        Assert.True(filter.Matches(Flight(50, 8, 3000, country: "Germany")));
        Assert.False(filter.Matches(Flight(50, 8, 3000, country: "France")));
        Assert.False(filter.Matches(Flight(50, 8, 3000, country: null)));
    }

    [Fact]
    [AllureFeature("Map filters")]
    public void Apply_WithCombinedFilters_ShouldKeepOnlyFlightsMatchingAll()
    {
        // Arrange (область, диапазон высот и страна применяются вместе)
        var filter = new FlightFilter
        {
            Area = new BoundingBox(0, 45, 15, 55),
            MinAltitude = 1000,
            MaxAltitude = 5000,
            Country = "Germany"
        };
        var flights = new List<FlightDto>
        {
            Flight(50, 8, 3000, "Germany"),   // подходит под всё
            Flight(50, 8, 3000, "France"),    // чужая страна
            Flight(50, 8, 9000, "Germany"),   // слишком высоко
            Flight(60, 8, 3000, "Germany")    // вне области
        };

        // Act
        var result = filter.Apply(flights).ToList();

        // Assert
        var kept = Assert.Single(result);
        Assert.Equal(3000, kept.Altitude);
        Assert.Equal("Germany", kept.Country);
        Assert.False(filter.IsEmpty);
    }
}
