// Интеграционный тест: повторный расчёт не создаёт дублей; новые точки продлевают
// интервал с сохранением начала, данные остальных рейсов остаются на месте.
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class CongestionProcedure_RebuildsAffectedFlights_IntegrationTest(TestDatabaseFixture db)
    : IClassFixture<TestDatabaseFixture>
{
    [Fact]
    public async Task Calculate_ReplacesAffectedFlightIntervalsWithoutChangingOtherFlights()
    {
        await CongestionTestData.ResetAsync(db);
        await db.ExecuteAsync("""
            INSERT INTO dds.flights (flight_id,callsign,aircraft_id) VALUES (1,'UPDATED',1),(2,'UNCHANGED',1);
            INSERT INTO dds.positions (flight_id,time_position,latitude,longitude,altitude)
            SELECT f,'2026-06-10 00:00Z'::timestamptz-(f-1)*interval '1 day'+n*interval '1 minute',0,0.01,400
            FROM generate_series(1,2) f CROSS JOIN generate_series(0,1) n;
            CALL calculate_airport_congestion('2026-06-09 00:00Z','2026-06-11 00:00Z');
            """);
        var untouchedId = await db.ScalarAsync<long>("SELECT id FROM mart.mart_airport_congestion WHERE flight_id=2");

        await db.ExecuteAsync("CALL calculate_airport_congestion('2026-06-10 00:00Z','2026-06-11 00:00Z'); CALL calculate_airport_congestion('2026-06-10 00:00Z','2026-06-11 00:00Z')");
        Assert.Equal(2, await db.ScalarAsync<int>("SELECT count(*) FROM mart.mart_airport_congestion"));

        await db.ExecuteAsync("""
            INSERT INTO dds.positions (flight_id,time_position,latitude,longitude,altitude)
            VALUES (1,'2026-06-10 00:20Z',0,0.01,400);
            CALL calculate_airport_congestion('2026-06-10 00:20Z','2026-06-10 00:21Z');
            """);
        Assert.Equal(2, await db.ScalarAsync<int>("SELECT count(*) FROM mart.mart_airport_congestion"));
        Assert.Equal(1200, await db.ScalarAsync<int>("SELECT duration_seconds FROM mart.mart_airport_congestion WHERE flight_id=1"));
        Assert.Equal(untouchedId, await db.ScalarAsync<long>("SELECT id FROM mart.mart_airport_congestion WHERE flight_id=2"));
    }
}
