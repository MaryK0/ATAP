// Интеграционный тест: очередь в PostgreSQL проверяет владельца, однократный захват, восстановление задания и состояния completed/failed.

using ATAP.BusinessLogic.Domain;
using ATAP.DataAccess.Repositories;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class ExportRepository_Lifecycle_IntegrationTest(TestDatabaseFixture db) : IClassFixture<TestDatabaseFixture>
{
    [Fact]
    public async Task QueueClaimsOnceRecoversExpiredLeaseAndRestrictsOwner()
    {
        var repo = new PostgresExportRepository(db.ConnectionString);
        var period = new AnalysisPeriod(new(2026, 6, 10), new(2026, 6, 10));
        var created = await repo.CreateAsync(2, 1, period, default);
        Assert.Equal("queued", created.Status);
        Assert.Equal(2, created.UserId);
        Assert.Equal(1, created.AirportId);
        Assert.Equal(period.From, created.DateFrom);
        Assert.Equal(period.To, created.DateTo);
        Assert.Null(created.Result);
        Assert.Null(created.Error);
        Assert.Null(await repo.GetAsync(created.Id, 1, default));
        var claims = await Task.WhenAll(repo.ClaimAsync(default), repo.ClaimAsync(default));
        Assert.Single(claims, c => c is not null);
        Assert.Equal("running", claims.Single(c => c is not null)!.Status);
        await db.ScalarAsync<int>("UPDATE dds.report_exports SET lease_until=NOW()-INTERVAL '1 second' WHERE id=@id RETURNING 1", ("id", created.Id));
        Assert.Equal(created.Id, (await repo.ClaimAsync(default))!.Id);
        await repo.CompleteAsync(created.Id, [1, 2, 3], null, default);
        var complete = await repo.GetAsync(created.Id, 2, default);
        Assert.Equal("completed", complete!.Status);
        Assert.Equal(new byte[] { 1, 2, 3 }, complete.Result);
        Assert.Null(await repo.ClaimAsync(default));

        var failed = await repo.CreateAsync(2, null, period, default);
        Assert.Null(failed.AirportId);
        await repo.ClaimAsync(default);
        await repo.CompleteAsync(failed.Id, null, "Test failure", default);
        var result = await repo.GetAsync(failed.Id, 2, default);
        Assert.Equal("failed", result!.Status);
        Assert.Null(result.AirportId);
        Assert.Equal(period.From, result.DateFrom);
        Assert.Equal(period.To, result.DateTo);
        Assert.Null(result.Result);
        Assert.Equal("Test failure", result.Error);
        Assert.Null(await repo.ClaimAsync(default));
    }
}
