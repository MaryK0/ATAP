using Npgsql;
using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.DataAccess.Interfaces;

namespace ATAP.DataAccess.Repositories;

public class PostgresAnalyticsRepository : IAnalyticsRepository
{
    private readonly string _connectionString;
    private readonly ISqlQueryFactory _sqlFactory; 

    public PostgresAnalyticsRepository(string connectionString, ISqlQueryFactory sqlFactory)
    {
        _connectionString = connectionString;
        _sqlFactory = sqlFactory;
    }

    public async Task<List<CongestionDb>> GetCongestionAsync(int airportId, DateTime start, DateTime end, bool includeDetails = false)
    {
        var result = new List<CongestionDb>();
        
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        string sql = _sqlFactory.GetCongestionQuery(includeDetails);
        using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@airportId", airportId);
        cmd.Parameters.AddWithValue("@start", start);
        cmd.Parameters.AddWithValue("@end", end);
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (includeDetails)
            {
                var congestion = new CongestionDb();
                
                congestion.Id = reader.GetInt64(reader.GetOrdinal("id"));
                congestion.AirportId = reader.GetInt32(reader.GetOrdinal("airport_id"));
                congestion.FlightId = reader.GetInt32(reader.GetOrdinal("flight_id"));
                congestion.Callsign = reader.IsDBNull(reader.GetOrdinal("callsign")) ? string.Empty : reader.GetString(reader.GetOrdinal("callsign"));
                congestion.EntryTime = reader.IsDBNull(reader.GetOrdinal("entry_time")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("entry_time"));
                congestion.ExitTime = reader.IsDBNull(reader.GetOrdinal("exit_time")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("exit_time"));
                congestion.DurationSeconds = reader.IsDBNull(reader.GetOrdinal("duration_seconds")) ? 0 : reader.GetInt32(reader.GetOrdinal("duration_seconds"));
                congestion.DateHour = reader.IsDBNull(reader.GetOrdinal("date_hour")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("date_hour"));
                congestion.IsDeparture = !reader.IsDBNull(reader.GetOrdinal("is_departure")) && reader.GetBoolean(reader.GetOrdinal("is_departure"));
                congestion.IsArrival = !reader.IsDBNull(reader.GetOrdinal("is_arrival")) && reader.GetBoolean(reader.GetOrdinal("is_arrival"));
                congestion.MaxAltitudeInZone = reader.IsDBNull(reader.GetOrdinal("max_altitude_in_zone")) ? 0 : reader.GetInt32(reader.GetOrdinal("max_altitude_in_zone"));
                congestion.MinAltitudeInZone = reader.IsDBNull(reader.GetOrdinal("min_altitude_in_zone")) ? 0 : reader.GetInt32(reader.GetOrdinal("min_altitude_in_zone"));
                congestion.LoadedAt = reader.IsDBNull(reader.GetOrdinal("loaded_at")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("loaded_at"));
                
                result.Add(congestion);
            }
            else
            {
                result.Add(new CongestionDb
                {
                    Id = reader.GetInt64(0),
                    AirportId = reader.GetInt32(1),
                    FlightId = reader.GetInt32(2),
                    IsDeparture = reader.GetBoolean(3),
                    IsArrival = reader.GetBoolean(4)
                });
            }
        }
        
        return result;
    }

    // НОВЫЙ МЕТОД
    public async Task<List<CongestionDb>> GetAllCongestionAsync(DateTime start, DateTime end)
    {
        var result = new List<CongestionDb>();
        
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        const string sql = @"
            SELECT id, airport_id, flight_id, callsign, entry_time, exit_time, 
                   duration_seconds, date_hour, is_departure, is_arrival, loaded_at
            FROM mart.mart_airport_congestion 
            WHERE entry_time >= @start AND exit_time <= @end
            ORDER BY entry_time DESC";
        
        using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@start", start);
        cmd.Parameters.AddWithValue("@end", end);
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var congestion = new CongestionDb();
            
            congestion.Id = reader.GetInt64(reader.GetOrdinal("id"));
            congestion.AirportId = reader.GetInt32(reader.GetOrdinal("airport_id"));
            congestion.FlightId = reader.GetInt32(reader.GetOrdinal("flight_id"));
            congestion.Callsign = reader.IsDBNull(reader.GetOrdinal("callsign")) ? string.Empty : reader.GetString(reader.GetOrdinal("callsign"));
            congestion.EntryTime = reader.IsDBNull(reader.GetOrdinal("entry_time")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("entry_time"));
            congestion.ExitTime = reader.IsDBNull(reader.GetOrdinal("exit_time")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("exit_time"));
            congestion.DurationSeconds = reader.IsDBNull(reader.GetOrdinal("duration_seconds")) ? 0 : reader.GetInt32(reader.GetOrdinal("duration_seconds"));
            congestion.DateHour = reader.IsDBNull(reader.GetOrdinal("date_hour")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("date_hour"));
            congestion.IsDeparture = reader.IsDBNull(reader.GetOrdinal("is_departure")) ? false : reader.GetBoolean(reader.GetOrdinal("is_departure"));
            congestion.IsArrival = reader.IsDBNull(reader.GetOrdinal("is_arrival")) ? false : reader.GetBoolean(reader.GetOrdinal("is_arrival"));
            congestion.LoadedAt = reader.IsDBNull(reader.GetOrdinal("loaded_at")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("loaded_at"));
            
            result.Add(congestion);
        }
        
        return result;
    }
}