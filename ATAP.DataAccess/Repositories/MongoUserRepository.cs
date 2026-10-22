using MongoDB.Bson;
using MongoDB.Driver;
using ATAP.BusinessLogic.Interfaces.Repositories;

namespace ATAP.DataAccess.Repositories;

public class MongoUserRepository : IUserRepository
{
    private readonly IMongoCollection<BsonDocument> _collection;
    private readonly IMongoCollection<BsonDocument> _counters;

    public MongoUserRepository(string connectionString, string databaseName)
    {
        var client   = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);
        _collection  = database.GetCollection<BsonDocument>("users");
        _counters    = database.GetCollection<BsonDocument>("counters");
    }

    private async Task<int> GetNextIdAsync()
    {
        var filter  = Builders<BsonDocument>.Filter.Eq("_id", "users");
        var update  = Builders<BsonDocument>.Update.Inc("seq", 1);
        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            IsUpsert       = true,
            ReturnDocument = ReturnDocument.After
        };
        var result = await _counters.FindOneAndUpdateAsync(filter, update, options);
        return result["seq"].AsInt32;
    }

    public async Task<UserDb?> GetByUsernameAsync(string username)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("username", username);
        var doc    = await _collection.Find(filter).FirstOrDefaultAsync();
        return doc == null ? null : MapToUserDb(doc);
    }

    public async Task<UserDb?> GetByEmailAsync(string email)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("email", email);
        var doc    = await _collection.Find(filter).FirstOrDefaultAsync();
        return doc == null ? null : MapToUserDb(doc);
    }

    public async Task<int> CreateAsync(UserDb user)
    {
        var id  = await GetNextIdAsync();
        var doc = new BsonDocument
        {
            ["userId"]       = id,
            ["username"]     = user.Username,
            ["email"]        = user.Email,
            ["passwordHash"] = user.PasswordHash,
            ["role"]         = user.Role,
            ["createdAt"]    = DateTime.UtcNow
        };
        await _collection.InsertOneAsync(doc);
        return id;
    }

    public async Task<bool> UpdateAsync(UserDb user)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("userId", user.UserId);
        var update = Builders<BsonDocument>.Update
            .Set("username",     user.Username)
            .Set("email",        user.Email)
            .Set("role",         user.Role)
            .Set("passwordHash", user.PasswordHash);
        var result = await _collection.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(int userId)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("userId", userId);
        var result = await _collection.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    public async Task<List<UserDb>> GetAllAsync()
    {
        var docs = await _collection.Find(FilterDefinition<BsonDocument>.Empty)
                                    .Sort(Builders<BsonDocument>.Sort.Ascending("userId"))
                                    .ToListAsync();
        return docs.Select(MapToUserDb).ToList();
    }

    private static UserDb MapToUserDb(BsonDocument doc) => new()
    {
        UserId       = doc["userId"].AsInt32,
        Username     = doc["username"].AsString,
        Email        = doc["email"].AsString,
        PasswordHash = doc["passwordHash"].AsString,
        Role         = doc["role"].AsString,
        CreatedAt    = doc.Contains("createdAt") ? doc["createdAt"].ToUniversalTime() : DateTime.MinValue
    };
}
