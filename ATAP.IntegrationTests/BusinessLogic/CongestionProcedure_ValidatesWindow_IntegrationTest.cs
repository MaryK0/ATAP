// Интеграционный тест: пустой/обратный период отклоняется, окно без новых точек
// не меняет витрину, прежний вызов без параметров остаётся рабочим.
using ATAP.IntegrationTests.Infrastructure;
using Npgsql;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class CongestionProcedure_ValidatesWindow_IntegrationTest(TestDatabaseFixture db)
    : IClassFixture<TestDatabaseFixture>
{
    [Fact]
    public async Task Calculate_ValidatesWindowAndPreservesZeroArgumentCall()
    {
        await CongestionTestData.ResetAsync(db);
        await db.ExecuteAsync("""
            INSERT INTO dds.flights (flight_id,callsign,aircraft_id) VALUES (1,'RECENT',1);
            INSERT INTO dds.positions (flight_id,time_position,latitude,longitude,altitude)
            VALUES (1,now()-interval '30 minutes',0,0.01,400),(1,now()-interval '29 minutes',0,0.01,400);
            CALL calculate_airport_congestion();
            """);
        var id = await db.ScalarAsync<long>("SELECT id FROM mart.mart_airport_congestion");

        foreach (var args in new[] { "NULL,now()", "now(),NULL", "now(),now()", "now(),now()-interval '1 hour'" })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.ExecuteAsync($"CALL calculate_airport_congestion({args})"));
            Assert.Equal("22023", error.SqlState);
        }
        await db.ExecuteAsync("CALL calculate_airport_congestion('2000-01-01 00:00Z','2000-01-02 00:00Z')");
        Assert.Equal(id, await db.ScalarAsync<long>("SELECT id FROM mart.mart_airport_congestion"));
        Assert.Equal(60, await db.ScalarAsync<int>("SELECT duration_seconds FROM mart.mart_airport_congestion"));
    }
}
