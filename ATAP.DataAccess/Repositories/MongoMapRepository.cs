using MongoDB.Bson;
using MongoDB.Driver;
using ATAP.BusinessLogic.Interfaces.Repositories;

namespace ATAP.DataAccess.Repositories;

public class MongoMapRepository : IMapRepository
{
    private readonly IMongoCollection<BsonDocument> _airports;
    private readonly IMongoCollection<BsonDocument> _flights;
    private readonly ICacheRepository               _cacheRepo;
    private readonly CacheSettings                  _cacheSettings;

    public MongoMapRepository(string connectionString, string databaseName,
        ICacheRepository cacheRepo, CacheSettings cacheSettings)
    {
        var client     = new MongoClient(connectionString);
        var database   = client.GetDatabase(databaseName);
        _airports      = database.GetCollection<BsonDocument>("airports");
        _flights       = database.GetCollection<BsonDocument>("current_flights");
        _cacheRepo     = cacheRepo;
        _cacheSettings = cacheSettings;
    }

    public async Task<List<FlightMapDb>> GetCurrentFlightsAsync()
    {
        var docs = await _flights.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        return docs.Select(d => new FlightMapDb
        {
            FlightId  = d["flightId"].AsInt32,
            Callsign  = d["callsign"].AsString,
            Latitude  = d["latitude"].AsDouble,
            Longitude = d["longitude"].AsDouble,
            Altitude  = d.Contains("altitude") && !d["altitude"].IsBsonNull ? d["altitude"].AsInt32 : 0
        }).ToList();
    }

    public async Task<List<AirportDb>> GetAirportsAsync()
    {
        // Отдельный ключ: под "airports:all" MapService кэширует AirportDto с другими полями.
        const string cacheKey = "airports:all:db";

        var cached = await _cacheRepo.GetAsync<List<AirportDb>>(cacheKey);
        if (cached != null) return cached;

        var docs   = await _airports.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        var result = docs.Select(d => new AirportDb
        {
            AirportId = d["airportId"].AsInt32,
            IcaoCode  = d["icaoCode"].AsString,
            Name      = d["name"].AsString,
            Latitude  = d["latitude"].AsDouble,
            Longitude = d["longitude"].AsDouble
        }).ToList();

        await _cacheRepo.SetAsync(cacheKey, result, _cacheSettings.AirportsTTLSeconds);
        return result;
    }
}
