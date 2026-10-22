namespace ATAP.BusinessLogic.Interfaces.Repositories;

public interface ICacheRepository
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, int ttlSeconds);
    Task RemoveAsync(string key);
}