namespace ATAP.IntegrationTests.Infrastructure;

internal static class CongestionTestData
{
    // Только персональная копия тестовой БД, созданная TestDatabaseFixture.
    public static Task ResetAsync(TestDatabaseFixture db) => db.ExecuteAsync("""
        TRUNCATE mart.mart_airport_congestion, dds.positions, dds.flights_airports,
            dds.saved_reports, dds.report_exports, dds.flights, dds.aircrafts, dds.airports
            RESTART IDENTITY CASCADE;
        INSERT INTO dds.airports (airport_id,icao_code,name,latitude,longitude,geom,analysis_radius)
        VALUES (1,'ZZZZ','Near',0,0,ST_SetSRID(ST_MakePoint(0,0),4326),10000),
               (2,'YYYY','Far',40,40,ST_SetSRID(ST_MakePoint(40,40),4326),10000);
        INSERT INTO dds.aircrafts (aircraft_id,icao24) VALUES (1,'ABCDEF');
        """);
}
