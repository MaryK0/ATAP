using System.Text;
using ATAP.BusinessLogic.Services;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class AnalyticsService_ExportToCsv_RowCountEqualsEntries_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public AnalyticsService_ExportToCsv_RowCountEqualsEntries_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task ExportToCsvAsync_ReportFromDatabase_HasHeaderPlusOneRowPerEntry()
    {
        // Arrange
        var service = new AnalyticsService(_db.CreateAnalyticsRepository());
        var start = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc);
        var report = await service.GetCongestionAsync(1, start, end);

        // Act
        var csv = Encoding.UTF8.GetString(await service.ExportToCsvAsync(report));

        // Assert
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.StartsWith("FlightId,Callsign,EntryTime", lines[0]);
        Assert.Equal(report.Entries.Count, lines.Length - 1);
        Assert.Contains(lines.Skip(1), l => l.Contains("BAW001") && l.Contains("2026-06-10 07:06:00"));
    }
}
