using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.DataAccess.Repositories;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ATAP.IntegrationTests.Infrastructure;

/// <summary>
/// Кэш Redis тестового стенда. У каждого тестового класса свой префикс ключей, поэтому классы,
/// идущие параллельно, не видят чужих данных; по окончании ключи префикса удаляются.
/// </summary>
public sealed class TestRedisFixture : IAsyncLifetime
{
    private RedisCache? _distributedCache;

    public string KeyPrefix { get; } = $"it_{Guid.NewGuid():N}_";

    public ICacheRepository Cache { get; private set; } = null!;

    public Task InitializeAsync()
    {
        _distributedCache = new RedisCache(Options.Create(new RedisCacheOptions
        {
            Configuration = TestEnvironment.Redis,
            InstanceName = KeyPrefix
        }));
        Cache = new RedisCacheRepository(_distributedCache);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _distributedCache?.Dispose();

        var options = ConfigurationOptions.Parse(TestEnvironment.Redis);
        options.AllowAdmin = true;
        options.AbortOnConnectFail = false;
        await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(options);
        var database = multiplexer.GetDatabase();
        foreach (var endpoint in multiplexer.GetEndPoints())
        {
            var server = multiplexer.GetServer(endpoint);
            foreach (var key in server.Keys(pattern: KeyPrefix + "*"))
                await database.KeyDeleteAsync(key);
        }
    }
}
