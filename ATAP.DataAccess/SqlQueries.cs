// ATAP.DataAccess/SqlQueries.cs
namespace ATAP.DataAccess;

public static class SqlQueries
{

    public static string GetCongestionQuery(bool includeDetails)
    {
        if (includeDetails)
        {
            return @"
                SELECT id, airport_id, flight_id, callsign, entry_time, exit_time,
                       duration_seconds, date_hour, is_departure, is_arrival,
                       max_altitude_in_zone, min_altitude_in_zone, loaded_at
                FROM mart.mart_airport_congestion
                WHERE airport_id = @airportId
                  AND entry_time >= @start
                  AND entry_time < @end
                ORDER BY entry_time";
        }

        return @"
            SELECT id, airport_id, flight_id, is_departure, is_arrival
            FROM mart.mart_airport_congestion
            WHERE airport_id = @airportId
              AND entry_time >= @start
              AND entry_time < @end";
    }
    
  
    public const string GetCurrentFlights = @"
        SELECT flight_id, callsign, latitude, longitude, altitude
        FROM mart.current_flights_map";
    
    public const string GetAirports = @"
        SELECT airport_id, icao_code, name, latitude, longitude 
        FROM dds.airports";
    
    public const string GetUserByUsername = @"
        SELECT user_id, username, email, password_hash, role 
        FROM dds.users 
        WHERE username = @username";
    
    // INSERT
    public const string CreateUser = @"
        INSERT INTO dds.users (username, email, password_hash, role)
        VALUES (@username, @email, @passwordHash, @role)
        RETURNING user_id";

    public const string GetSavedReportsByUser = @"
        SELECT sr.id, sr.name, sr.airport_id, a.name AS airport_name,
               sr.date_from, sr.date_to, sr.created_at
        FROM dds.saved_reports sr
        JOIN dds.airports a ON a.airport_id = sr.airport_id
        WHERE sr.user_id = @userId
        ORDER BY sr.created_at DESC";

    public const string CreateSavedReport = @"
        INSERT INTO dds.saved_reports (user_id, name, airport_id, date_from, date_to)
        VALUES (@userId, @name, @airportId, @dateFrom, @dateTo)
        RETURNING id";

    public const string DeleteSavedReport = @"
        DELETE FROM dds.saved_reports
        WHERE id = @id AND user_id = @userId";
}