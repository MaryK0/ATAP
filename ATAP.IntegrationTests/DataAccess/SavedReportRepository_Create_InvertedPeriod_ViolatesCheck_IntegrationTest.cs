using ATAP.BusinessLogic.DTO;
using ATAP.IntegrationTests.Infrastructure;
using Npgsql;

namespace ATAP.IntegrationTests.DataAccess;

/// <summary>Целостность на уровне БД: CHECK date_from &lt;= date_to срабатывает, даже если сервис проверку не выполнил.</summary>
[Trait("Category", "Integration")]
public class SavedReportRepository_Create_InvertedPeriod_ViolatesCheck_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public SavedReportRepository_Create_InvertedPeriod_ViolatesCheck_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task CreateAsync_DateFromAfterDateTo_ViolatesCheckConstraintAndStoresNothing()
    {
        // Arrange
        var repository = _db.CreateSavedReportRepository();
        var userId = await _db.ScalarAsync<int>("SELECT user_id FROM dds.users WHERE username = 'admin'");
        var reportsBefore = await _db.ScalarAsync<long>("SELECT COUNT(*) FROM dds.saved_reports");
        var dto = new CreateSavedReportDto
        {
            Name = "Неверный период",
            AirportId = 1,
            DateFrom = new DateOnly(2026, 6, 11),
            DateTo = new DateOnly(2026, 6, 10)
        };

        // Act
        var error = await Assert.ThrowsAsync<PostgresException>(() => repository.CreateAsync(userId, dto));

        // Assert
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(reportsBefore, await _db.ScalarAsync<long>("SELECT COUNT(*) FROM dds.saved_reports"));
    }
}
