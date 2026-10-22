using MongoDB.Bson;
using MongoDB.Driver;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Repositories;

namespace ATAP.DataAccess.Repositories;

public class MongoSavedReportRepository : ISavedReportRepository
{
    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoSavedReportRepository(string connectionString, string databaseName)
    {
        var client   = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);
        _collection  = database.GetCollection<BsonDocument>("saved_reports");
    }

    public async Task<List<SavedReportDto>> GetByUserAsync(int userId)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("userId", userId);
        var sort   = Builders<BsonDocument>.Sort.Descending("createdAt");
        var docs   = await _collection.Find(filter).Sort(sort).ToListAsync();

        return docs.Select(d => new SavedReportDto
        {
            Id          = d["_id"].AsObjectId.GetHashCode() & 0x7FFFFFFF,
            Name        = d["name"].AsString,
            AirportId   = d["airportId"].AsInt32,
            AirportName = "",
            DateFrom    = DateOnly.Parse(d["dateFrom"].AsString),
            DateTo      = DateOnly.Parse(d["dateTo"].AsString),
            CreatedAt   = d["createdAt"].ToUniversalTime()
        }).ToList();
    }

    public async Task<int> CreateAsync(int userId, CreateSavedReportDto dto)
    {
        var doc = new BsonDocument
        {
            ["userId"]    = userId,
            ["name"]      = dto.Name,
            ["airportId"] = dto.AirportId,
            ["dateFrom"]  = dto.DateFrom.ToString("yyyy-MM-dd"),
            ["dateTo"]    = dto.DateTo.ToString("yyyy-MM-dd"),
            ["createdAt"] = DateTime.UtcNow
        };

        await _collection.InsertOneAsync(doc);
        return doc["_id"].AsObjectId.GetHashCode() & 0x7FFFFFFF;
    }

    public async Task DeleteAsync(int id, int userId)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("userId", userId);
        var docs   = await _collection.Find(filter).ToListAsync();
        var target = docs.FirstOrDefault(d => (d["_id"].AsObjectId.GetHashCode() & 0x7FFFFFFF) == id);
        if (target != null)
            await _collection.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", target["_id"]));
    }
}
