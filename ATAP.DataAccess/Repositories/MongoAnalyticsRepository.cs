using MongoDB.Bson;
using MongoDB.Driver;
using ATAP.BusinessLogic.Interfaces.Repositories;

namespace ATAP.DataAccess.Repositories;

public class MongoAnalyticsRepository : IAnalyticsRepository
{
    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoAnalyticsRepository(string connectionString, string databaseName)
    {
        var client   = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);
        _collection  = database.GetCollection<BsonDocument>("mart_airport_congestion");
    }

    public async Task<List<CongestionDb>> GetCongestionAsync(int airportId, DateTime start, DateTime end, bool includeDetails = false)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("airportId", airportId),
            Builders<BsonDocument>.Filter.Gte("entryTime", start),
            Builders<BsonDocument>.Filter.Lte("exitTime", end));

        var docs = await _collection.Find(filter)
                                    .Sort(Builders<BsonDocument>.Sort.Descending("entryTime"))
                                    .ToListAsync();
        return docs.Select(d => MapToCongestionDb(d, includeDetails)).ToList();
    }

    public async Task<List<CongestionDb>> GetAllCongestionAsync(DateTime start, DateTime end)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Gte("entryTime", start),
            Builders<BsonDocument>.Filter.Lte("exitTime", end));

        var docs = await _collection.Find(filter)
                                    .Sort(Builders<BsonDocument>.Sort.Descending("entryTime"))
                                    .ToListAsync();
        return docs.Select(d => MapToCongestionDb(d, true)).ToList();
    }

    private static CongestionDb MapToCongestionDb(BsonDocument d, bool includeDetails) => new()
    {
        Id                = d["_id"].IsObjectId
                                ? d["_id"].AsObjectId.GetHashCode() & 0x7FFFFFFFL
                                : d["_id"].BsonType == BsonType.Int64 ? d["_id"].AsInt64 : (long)d["_id"].AsInt32,
        AirportId         = d["airportId"].AsInt32,
        FlightId          = d["flightId"].AsInt32,
        IsDeparture       = d.Contains("isDeparture") && d["isDeparture"].AsBoolean,
        IsArrival         = d.Contains("isArrival")   && d["isArrival"].AsBoolean,
        Callsign          = includeDetails && d.Contains("callsign")  ? d["callsign"].AsString         : "",
        EntryTime         = includeDetails && d.Contains("entryTime") ? d["entryTime"].ToUniversalTime() : DateTime.MinValue,
        ExitTime          = includeDetails && d.Contains("exitTime")  ? d["exitTime"].ToUniversalTime()  : DateTime.MinValue,
        DurationSeconds   = includeDetails && d.Contains("durationSeconds")   ? d["durationSeconds"].AsInt32   : 0,
        DateHour          = includeDetails && d.Contains("dateHour")          ? d["dateHour"].ToUniversalTime() : DateTime.MinValue,
        MaxAltitudeInZone = includeDetails && d.Contains("maxAltitudeInZone") ? d["maxAltitudeInZone"].AsInt32 : 0,
        MinAltitudeInZone = includeDetails && d.Contains("minAltitudeInZone") ? d["minAltitudeInZone"].AsInt32 : 0,
        LoadedAt          = includeDetails && d.Contains("loadedAt")          ? d["loadedAt"].ToUniversalTime()  : DateTime.MinValue
    };
}
