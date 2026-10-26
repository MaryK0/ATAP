namespace ATAP.DataAccess.Factories;
using ATAP.DataAccess.Interfaces; 

public class SqlQueryFactory : ISqlQueryFactory
{

    public string GetCongestionQuery(bool includeDetails)
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
    
    
    public string GetCurrentFlightsQuery() => @"
        SELECT flight_id, callsign, latitude, longitude, altitude, origin_country
        FROM mart.current_flights_map";
    
    public string GetAirportsQuery() => @"
        SELECT airport_id, icao_code, name, latitude, longitude,
               analysis_radius, iata_code, city, country_code, airport_type, elevation
        FROM dds.airports";
    
    public string GetUserByUsernameQuery() => @"
        SELECT user_id, username, email, password_hash, role, created_at
        FROM dds.users 
        WHERE username = @username";
    
    public string CreateUserQuery() => @"
        INSERT INTO dds.users (username, email, password_hash, role) 
        VALUES (@username, @email, @passwordHash, @role) 
        RETURNING user_id";
}