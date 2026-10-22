using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using ATAP.BusinessLogic.Interfaces.Repositories;

namespace ATAP.DataAccess.Repositories;

public class RedisCacheRepository : ICacheRepository
{
    private readonly IDistributedCache _cache;

    public RedisCacheRepository(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var data = await _cache.GetStringAsync(key);
        if (string.IsNullOrEmpty(data))
            return default;
        
        return JsonSerializer.Deserialize<T>(data);
    }

    public async Task SetAsync<T>(string key, T value, int ttlSeconds)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds)
        };
        
        var jsonData = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(key, jsonData, options);
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }
}