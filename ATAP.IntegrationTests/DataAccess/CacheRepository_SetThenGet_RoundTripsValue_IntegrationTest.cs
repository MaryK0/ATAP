using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class CacheRepository_SetThenGet_RoundTripsValue_IntegrationTest : IClassFixture<TestRedisFixture>
{
    private readonly TestRedisFixture _redis;

    public CacheRepository_SetThenGet_RoundTripsValue_IntegrationTest(TestRedisFixture redis) => _redis = redis;

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsSameValueFromRealRedis()
    {
        // Arrange
        var cache = _redis.Cache;
        var value = new List<string> { "UUEE", "UUDD", "UUWW" };

        // Act
        await cache.SetAsync("airports-codes", value, ttlSeconds: 60);
        var restored = await cache.GetAsync<List<string>>("airports-codes");

        // Assert
        Assert.NotNull(restored);
        Assert.Equal(value, restored);
        Assert.Null(await cache.GetAsync<List<string>>("missing-key"));
    }
}
