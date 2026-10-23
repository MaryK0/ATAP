using Xunit;
using ATAP.BusinessLogic.Services;
using ATAP.BusinessLogic.Tests.Mocks;
using ATAP.BusinessLogic.DTO;
using Allure.Xunit.Attributes;

namespace ATAP.BusinessLogic.Tests;

/// <summary>
/// Classical unit-тесты для <see cref="AnalyticsService"/>.
/// Техника: граничное значение (start == end, start &gt; end), эквивалентное разбиение
/// (данные есть / данных нет), тест на исключение (ArgumentException / ArgumentNullException).
/// </summary>
[AllureSuite("AnalyticsService")]
public class AnalyticsServiceTests
{
    [Fact]
    [AllureFeature("Congestion report")]
    public async Task GetCongestionAsync_ShouldReturnReport()
    {
        // Arrange
        var mockRepo = new MockAnalyticsRepository();
        var service = new AnalyticsService(mockRepo);

        // Act
        var result = await service.GetCongestionAsync(1, DateTime.Today, DateTime.Today.AddDays(1));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.AirportId);
        Assert.Equal(1, result.DepartureCount);
        Assert.Equal(1, result.ArrivalCount);
    }

    [Fact]
    [AllureFeature("Congestion report")]
    public async Task GetCongestionAsync_WhenStartEqualsEnd_ShouldSucceed()
    {
        // Arrange (граничное значение: start == end — нижняя допустимая граница периода)
        var mockRepo = new MockAnalyticsRepository();
        var service = new AnalyticsService(mockRepo);
        var day = DateTime.Today;

        // Act
        var result = await service.GetCongestionAsync(1, day, day);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(day, result.PeriodStart);
    }

    [Fact]
    [AllureFeature("Congestion report")]
    public async Task GetAllCongestionAsync_ShouldReturnReportForAllAirports()
    {
        // Arrange (запрос по всем аэропортам: airportId в отчёте — 0, записи берутся без фильтра по аэропорту)
        var mockRepo = new MockAnalyticsRepository();
        var service = new AnalyticsService(mockRepo);

        // Act
        var result = await service.GetAllCongestionAsync(DateTime.Today, DateTime.Today.AddDays(1));

        // Assert
        Assert.Equal(0, result.AirportId);
        Assert.Equal(2, result.Entries.Count);
        Assert.Equal(1, result.DepartureCount);
        Assert.Equal(1, result.ArrivalCount);
        Assert.All(result.Entries, e => Assert.Equal(1, e.AirportId));
    }

    [Fact]
    [AllureFeature("Congestion report")]
    public async Task GetAllCongestionAsync_WhenStartDateAfterEndDate_ShouldThrowException()
    {
        // Arrange (первое невалидное значение за границей: start = end + 1 день)
        var mockRepo = new MockAnalyticsRepository();
        var service = new AnalyticsService(mockRepo);

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetAllCongestionAsync(DateTime.Today.AddDays(1), DateTime.Today));
    }

    [Fact]
    [AllureFeature("Congestion report")]
    public async Task GetCongestionAsync_WhenStartDateAfterEndDate_ShouldThrowException()
    {
        // Arrange (первое невалидное значение за границей: start = end + 1 день)
        var mockRepo = new MockAnalyticsRepository();
        var service = new AnalyticsService(mockRepo);

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetCongestionAsync(1, DateTime.Today.AddDays(1), DateTime.Today));
    }

    [Fact]
    [AllureFeature("Congestion report")]
    public async Task GetCongestionAsync_WithEmptyData_ShouldReturnZeroCounts()
    {
        // Arrange (эквивалентный класс: у аэропорта нет записей за период)
        var mockRepo = new MockAnalyticsRepository(empty: true);
        var service = new AnalyticsService(mockRepo);

        // Act
        var result = await service.GetCongestionAsync(1, DateTime.Today, DateTime.Today.AddDays(1));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.DepartureCount);
        Assert.Equal(0, result.ArrivalCount);
    }

    [Fact]
    [AllureFeature("Congestion report")]
    public async Task GetCongestionAsync_WithCacheHit_ShouldReturnCachedReportWithoutTouchingRepository()
    {
        // Arrange
        var mockRepo = new MockAnalyticsRepository(empty: true); // пуст — если сервис обратится к нему, тест провалится по данным
        var mockCache = new MockCacheRepository();
        var service = new AnalyticsService(mockRepo, mockCache);
        var start = DateTime.Today;
        var end = DateTime.Today.AddDays(1);
        var cachedReport = new CongestionReportDto { AirportId = 1, PeriodStart = start, PeriodEnd = end, DepartureCount = 42, ArrivalCount = 7 };
        await mockCache.SetAsync($"congestion:1:{start:yyyyMMddHH}:{end:yyyyMMddHH}", cachedReport, 3600);

        // Act
        var result = await service.GetCongestionAsync(1, start, end);

        // Assert
        Assert.Equal(42, result.DepartureCount);
        Assert.Equal(7, result.ArrivalCount);
    }

    [Fact]
    [AllureFeature("Congestion report")]
    public async Task GetCongestionAsync_WithCacheConfiguredButMiss_ShouldQueryRepositoryAndPopulateCache()
    {
        // Arrange — кэш подключён, но пуст: должна выполниться ветка "положить результат в кэш"
        var mockRepo = new MockAnalyticsRepository();
        var mockCache = new MockCacheRepository();
        var service = new AnalyticsService(mockRepo, mockCache);
        var start = DateTime.Today;
        var end = DateTime.Today.AddDays(1);

        // Act
        var result = await service.GetCongestionAsync(1, start, end);

        // Assert
        Assert.Equal(1, result.DepartureCount);
        var cached = await mockCache.GetAsync<CongestionReportDto>($"congestion:1:{start:yyyyMMddHH}:{end:yyyyMMddHH}");
        Assert.NotNull(cached);
    }

    [Fact]
    [AllureFeature("CSV export")]
    public async Task ExportToCsvAsync_WithEmptyEntries_ShouldReturnOnlyHeaderRow()
    {
        // Arrange (граничное значение: отчёт без записей)
        var service = new AnalyticsService(new MockAnalyticsRepository());
        var report = new CongestionReportDto
        {
            AirportId = 1,
            PeriodStart = DateTime.Now,
            PeriodEnd = DateTime.Now.AddDays(1),
            DepartureCount = 0,
            ArrivalCount = 0
        };

        // Act
        var result = await service.ExportToCsvAsync(report);

        // Assert
        var csv = System.Text.Encoding.UTF8.GetString(result);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines); // только заголовок
        Assert.StartsWith("FlightId,Callsign", lines[0]);
    }

    [Fact]
    [AllureFeature("CSV export")]
    public async Task ExportToCsvAsync_WithEntries_ShouldSerializeEachEntryAsCsvRow()
    {
        // Arrange
        var service = new AnalyticsService(new MockAnalyticsRepository());
        var report = new CongestionReportDto
        {
            AirportId = 1,
            PeriodStart = DateTime.Now,
            PeriodEnd = DateTime.Now.AddDays(1),
            DepartureCount = 1,
            ArrivalCount = 0,
            Entries = new List<CongestionEntryDto>
            {
                new()
                {
                    Id = 1, FlightId = 10, Callsign = "AFL123",
                    EntryTime = new DateTime(2026, 1, 1, 10, 0, 0),
                    ExitTime = new DateTime(2026, 1, 1, 10, 15, 0),
                    DurationSeconds = 900, IsDeparture = true, IsArrival = false,
                    MaxAltitudeInZone = 600, MinAltitudeInZone = 100
                }
            }
        };

        // Act
        var result = await service.ExportToCsvAsync(report);

        // Assert
        var csv = System.Text.Encoding.UTF8.GetString(result);
        Assert.Contains("AFL123", csv);
        Assert.Contains("900", csv);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length); // заголовок + одна запись
    }

    [Fact]
    [AllureFeature("CSV export")]
    public async Task ExportToCsvAsync_WithNullReport_ShouldThrowException()
    {
        // Arrange
        var service = new AnalyticsService(new MockAnalyticsRepository());

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.ExportToCsvAsync(null!));
    }
}
