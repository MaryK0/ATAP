using ATAP.BusinessLogic.Interfaces.Repositories;

namespace ATAP.Api.Infrastructure;

/// <summary>
/// Обёртка над кэшем: недоступный Redis не должен ломать запросы (NFR-R2). Ошибки кэша
/// фиксируются в журнале и трактуются как «в кэше нет» — данные берутся из БД.
/// </summary>
public sealed class ResilientCacheRepository : ICacheRepository
{
    private readonly ICacheRepository _inner;
    private readonly ILogger<ResilientCacheRepository> _logger;

    public ResilientCacheRepository(ICacheRepository inner, ILogger<ResilientCacheRepository> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try { return await _inner.GetAsync<T>(key); }
        catch (Exception ex) { _logger.LogWarning(ex, "Кэш недоступен при чтении ключа {Key}", key); return default; }
    }

    public async Task SetAsync<T>(string key, T value, int ttlSeconds)
    {
        try { await _inner.SetAsync(key, value, ttlSeconds); }
        catch (Exception ex) { _logger.LogWarning(ex, "Кэш недоступен при записи ключа {Key}", key); }
    }

    public async Task RemoveAsync(string key)
    {
        try { await _inner.RemoveAsync(key); }
        catch (Exception ex) { _logger.LogWarning(ex, "Кэш недоступен при удалении ключа {Key}", key); }
    }
}
