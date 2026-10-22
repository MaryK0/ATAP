using ATAP.BusinessLogic.Interfaces.Repositories;

namespace ATAP.BusinessLogic.Tests.Mocks;

public class MockCacheRepository : ICacheRepository
{
    private readonly Dictionary<string, (object Value, DateTime Expiry)> _cache = new();

    public Task<T?> GetAsync<T>(string key)
    {
        if (_cache.TryGetValue(key, out var cached) && cached.Expiry > DateTime.Now)
            return Task.FromResult((T?)cached.Value);
        
        return Task.FromResult(default(T));
    }

    public Task SetAsync<T>(string key, T value, int ttlSeconds)
    {
        _cache[key] = (value!, DateTime.Now.AddSeconds(ttlSeconds));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
}