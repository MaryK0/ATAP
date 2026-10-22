using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class CacheRepository_Remove_DeletesKey_IntegrationTest : IClassFixture<TestRedisFixture>
{
    private readonly TestRedisFixture _redis;

    public CacheRepository_Remove_DeletesKey_IntegrationTest(TestRedisFixture redis) => _redis = redis;

    [Fact]
    public async Task RemoveAsync_ExistingKey_MakesValueUnavailable()
    {
        // Arrange
        var cache = _redis.Cache;
        await cache.SetAsync("to-remove", "value", ttlSeconds: 60);
        Assert.Equal("value", await cache.GetAsync<string>("to-remove"));

        // Act
        await cache.RemoveAsync("to-remove");

        // Assert
        Assert.Null(await cache.GetAsync<string>("to-remove"));
    }
}
