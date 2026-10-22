using ATAP.BusinessLogic.Interfaces.Repositories;

namespace ATAP.ConsoleUI.Infrastructure;

public class NullCacheRepository : ICacheRepository
{
    public Task<T?> GetAsync<T>(string key) => Task.FromResult(default(T));
    public Task SetAsync<T>(string key, T value, int ttlSeconds) => Task.CompletedTask;
    public Task RemoveAsync(string key) => Task.CompletedTask;
}
