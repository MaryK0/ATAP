using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class CacheRepository_ExpiredEntry_ReturnsNull_IntegrationTest : IClassFixture<TestRedisFixture>
{
    private readonly TestRedisFixture _redis;

    public CacheRepository_ExpiredEntry_ReturnsNull_IntegrationTest(TestRedisFixture redis) => _redis = redis;

    [Fact]
    public async Task GetAsync_AfterTtlElapsed_ReturnsNull()
    {
        // Arrange
        var cache = _redis.Cache;
        await cache.SetAsync("short-lived", "value", ttlSeconds: 1);
        Assert.Equal("value", await cache.GetAsync<string>("short-lived"));

        // Act
        await Task.Delay(TimeSpan.FromSeconds(2));
        var afterExpiry = await cache.GetAsync<string>("short-lived");

        // Assert
        Assert.Null(afterExpiry);
    }
}
