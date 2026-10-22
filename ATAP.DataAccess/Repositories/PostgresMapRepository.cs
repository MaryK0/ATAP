using Npgsql;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.DataAccess.Interfaces;

namespace ATAP.DataAccess.Repositories;

public class PostgresMapRepository : IMapRepository
{
    private readonly string _connectionString;
    private readonly ISqlQueryFactory _sqlFactory;
    private readonly ICacheRepository _cacheRepo;
    private readonly CacheSettings _cacheSettings;

    public PostgresMapRepository(string connectionString, ISqlQueryFactory sqlFactory, ICacheRepository cacheRepo, CacheSettings cacheSettings)
    {
        _connectionString = connectionString;
        _sqlFactory = sqlFactory;
        _cacheRepo = cacheRepo;
        _cacheSettings = cacheSettings;
    }

    public async Task<List<FlightMapDb>> GetCurrentFlightsAsync()
    {
        // Динамические данные не кэшируем
        var result = new List<FlightMapDb>();
        
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var cmd = new NpgsqlCommand(_sqlFactory.GetCurrentFlightsQuery(), connection);
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new FlightMapDb
            {
                FlightId = reader.GetInt32(0),
                Callsign = reader.GetString(1),
                Latitude = reader.GetDouble(2),
                Longitude = reader.GetDouble(3),
                Altitude = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
            });
        }
        
        return result;
    }

    public async Task<List<AirportDb>> GetAirportsAsync()
    {
        // Отдельный ключ: под "airports:all" MapService кэширует AirportDto с другими полями.
        const string cacheKey = "airports:all:db";
        
        // 1. Cache-Aside: пробуем получить из кэша
        var cached = await _cacheRepo.GetAsync<List<AirportDb>>(cacheKey);
        if (cached != null)
        {
            Console.WriteLine($"[Cache HIT] {cacheKey}");
            return cached;
        }
        
        Console.WriteLine($"[Cache MISS] {cacheKey}, загрузка из PostgreSQL");
        
        // 2. Загружаем из БД
        var result = new List<AirportDb>();
        
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var cmd = new NpgsqlCommand(_sqlFactory.GetAirportsQuery(), connection);
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new AirportDb
            {
                AirportId = reader.GetInt32(0),
                IcaoCode = reader.GetString(1),
                Name = reader.GetString(2),
                Latitude = reader.GetDouble(3),
                Longitude = reader.GetDouble(4),
                AnalysisRadius = reader.IsDBNull(5) ? 10000 : reader.GetInt32(5),
                IataCode = reader.IsDBNull(6) ? null : reader.GetString(6).Trim(),
                City = reader.IsDBNull(7) ? null : reader.GetString(7),
                CountryCode = reader.IsDBNull(8) ? null : reader.GetString(8).Trim(),
                AirportType = reader.IsDBNull(9) ? null : reader.GetString(9),
                Elevation = reader.IsDBNull(10) ? null : reader.GetInt32(10)
            });
        }
        
        // 3. Сохраняем в кэш
        await _cacheRepo.SetAsync(cacheKey, result, _cacheSettings.AirportsTTLSeconds);
        
        return result;
    }
}