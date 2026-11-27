// Интеграционный тест: выход из радиуса и превышение высоты разделяют посещения;
// несколько связей рейса не умножают строки, события относятся к своему интервалу.
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class CongestionProcedure_SplitsVisits_IntegrationTest(TestDatabaseFixture db)
    : IClassFixture<TestDatabaseFixture>
{
    [Fact]
    public async Task Calculate_SplitsVisitsAndAssociatesArrivalDepartureByTime()
    {
        await CongestionTestData.ResetAsync(db);
        await db.ExecuteAsync("""
            INSERT INTO dds.flights (flight_id,callsign,aircraft_id) VALUES (1,'VISITS',1);
            INSERT INTO dds.positions (flight_id,time_position,latitude,longitude,altitude)
            SELECT 1,'2026-06-10 00:00Z'::timestamptz+n*interval '1 minute',
                0,CASE WHEN n=2 THEN 0.2 ELSE 0.01 END,CASE WHEN n=5 THEN 611 ELSE 400 END
            FROM generate_series(0,7) n;
            INSERT INTO dds.flights_airports (flight_id,airport_id,relation_type,event_time)
            VALUES (1,1,'departure','2026-06-10 00:00:15Z'),
                   (1,1,'departure','2026-06-10 00:00:30Z'),
                   (1,1,'arrival','2026-06-10 00:03:30Z');
            """);

        await db.ExecuteAsync("CALL calculate_airport_congestion('2026-06-10 00:00Z','2026-06-11 00:00Z')");

        Assert.Equal(3, await db.ScalarAsync<int>("SELECT count(*) FROM mart.mart_airport_congestion"));
        Assert.Equal(3, await db.ScalarAsync<int>("SELECT count(*) FROM mart.mart_airport_congestion WHERE duration_seconds=60"));
        Assert.Equal(1, await db.ScalarAsync<int>("SELECT count(*) FROM mart.mart_airport_congestion WHERE is_departure AND NOT is_arrival AND entry_time='2026-06-10 00:00Z'"));
        Assert.Equal(1, await db.ScalarAsync<int>("SELECT count(*) FROM mart.mart_airport_congestion WHERE is_arrival AND NOT is_departure AND entry_time='2026-06-10 00:03Z'"));
        Assert.Equal(1, await db.ScalarAsync<int>("SELECT count(*) FROM mart.mart_airport_congestion WHERE NOT is_arrival AND NOT is_departure AND entry_time='2026-06-10 00:06Z'"));
    }
}
