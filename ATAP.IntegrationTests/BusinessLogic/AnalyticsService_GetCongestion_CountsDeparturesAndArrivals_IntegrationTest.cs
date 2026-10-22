using ATAP.BusinessLogic.Services;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class AnalyticsService_GetCongestion_CountsDeparturesAndArrivals_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public AnalyticsService_GetCongestion_CountsDeparturesAndArrivals_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task GetCongestionAsync_FullDay_ReturnsEntriesAndDeparturesPlusArrivalsEqualEntries()
    {
        // Arrange
        var service = new AnalyticsService(_db.CreateAnalyticsRepository());
        var start = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var report = await service.GetCongestionAsync(1, start, end);

        // Assert
        Assert.Equal(1, report.AirportId);
        Assert.Equal(4, report.Entries.Count);
        Assert.Equal(1, report.DepartureCount);
        Assert.Equal(3, report.ArrivalCount);
        Assert.Equal(report.Entries.Count, report.DepartureCount + report.ArrivalCount);
        Assert.Equal(start, report.PeriodStart);
        Assert.Equal(end, report.PeriodEnd);
    }
}
