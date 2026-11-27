// Интеграционный тест: 9 км входят в зону, 11 км — нет; высота 610 м допустима,
// 611 м — нет. Координаты используются и при NULL, и при ошибочном geom.
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class CongestionProcedure_UsesMetersAndAltitude_IntegrationTest(TestDatabaseFixture db)
    : IClassFixture<TestDatabaseFixture>
{
    [Fact]
    public async Task Calculate_UsesMetersStrictRadiusAndInclusiveAltitude()
    {
        await CongestionTestData.ResetAsync(db);
        await db.ExecuteAsync("""
            INSERT INTO dds.flights (flight_id,callsign,aircraft_id)
                SELECT n,'TEST'||n,1 FROM generate_series(1,5) n;
            WITH samples(flight_id,meters,altitude) AS (
                VALUES (1,9000,610),(2,11000,500),(3,9000,611),(4,10000,500),(5,9000,500)
            ), points AS (
                SELECT *,ST_Project('SRID=4326;POINT(0 0)'::geography,meters,pi()/2)::geometry AS point
                FROM samples
            )
            INSERT INTO dds.positions (flight_id,time_position,latitude,longitude,geom,altitude)
            SELECT flight_id,'2026-06-10 00:00Z'::timestamptz+n*interval '1 minute',
                ST_Y(point),ST_X(point),CASE WHEN flight_id=1 THEN NULL
                    WHEN flight_id=2 THEN ST_SetSRID(ST_MakePoint(0,0),4326) ELSE point END,altitude
            FROM points CROSS JOIN generate_series(1,2) n WHERE flight_id<>5 OR n=1;
            """);

        await db.ExecuteAsync("CALL calculate_airport_congestion('2026-06-10 00:00Z','2026-06-11 00:00Z')");

        Assert.Equal(1, await db.ScalarAsync<int>("SELECT count(*) FROM mart.mart_airport_congestion"));
        Assert.Equal(1, await db.ScalarAsync<int>("SELECT flight_id FROM mart.mart_airport_congestion"));
        Assert.Equal(1, await db.ScalarAsync<int>("SELECT airport_id FROM mart.mart_airport_congestion"));
        Assert.Equal(60, await db.ScalarAsync<int>("SELECT duration_seconds FROM mart.mart_airport_congestion"));
        Assert.Equal(610, await db.ScalarAsync<int>("SELECT max_altitude_in_zone FROM mart.mart_airport_congestion"));
    }
}
