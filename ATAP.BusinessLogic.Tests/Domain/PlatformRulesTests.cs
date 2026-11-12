using System.Security.Cryptography;
using System.Text;
using ATAP.BusinessLogic.Domain;
using ATAP.BusinessLogic.Security;
using Moq;
using Xunit;

namespace ATAP.BusinessLogic.Tests.Domain;

public class PlatformRulesTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(31)]
    public void AnalysisPeriodIncludesBothDates(int days)
    {
        var from = new DateOnly(2026, 1, 1);
        var period = new AnalysisPeriod(from, from.AddDays(days - 1));
        period.Validate();
        Assert.Equal(days, (period.EndExclusive - period.Start).TotalDays);
    }

    [Fact]
    public async Task TooLongPeriodDoesNotReachStorage()
    {
        var repo = new Mock<IPlatformRepository>(MockBehavior.Strict);
        var service = new PlatformService(repo.Object);
        var error = await Assert.ThrowsAsync<DomainException>(() => service.CongestionAsync(
            1, new(new(2026, 1, 1), new(2026, 2, 1)), new()));
        Assert.Equal("period-too-long", error.Code);
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CannotDeleteAdmin()
    {
        var repo = new Mock<IPlatformRepository>(MockBehavior.Strict);
        repo.Setup(r => r.UserAsync(1, default)).ReturnsAsync(
            new UserAccount(new(1, "admin", "admin@example.com", "admin", DateTime.UtcNow), "hash"));
        var error = await Assert.ThrowsAsync<DomainException>(() => new PlatformService(repo.Object).DeleteUserAsync(1));
        Assert.Equal(DomainError.Forbidden, error.Error);
        repo.Verify(r => r.DeleteUserAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void PasswordHashesAreSaltedAndVerifyOnlyCorrectPassword()
    {
        var first = PasswordHasher.Hash("Example#2026");
        var second = PasswordHasher.Hash("Example#2026");
        Assert.NotEqual(first, second);
        Assert.True(PasswordHasher.Verify("Example#2026", first));
        Assert.False(PasswordHasher.Verify("wrong", first));
        Assert.False(PasswordHasher.Verify("Example#2026", "invalid"));
    }

    [Fact]
    public void LegacyPasswordCanBeVerifiedAndMarkedForUpgrade()
    {
        var legacy = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("Example#2026")));
        Assert.True(PasswordHasher.Verify("Example#2026", legacy));
        Assert.True(PasswordHasher.NeedsUpgrade(legacy));
        Assert.False(PasswordHasher.NeedsUpgrade(PasswordHasher.Hash("Example#2026")));
    }
}
