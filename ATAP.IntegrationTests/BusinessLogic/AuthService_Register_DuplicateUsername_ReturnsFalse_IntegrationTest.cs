using ATAP.BusinessLogic.Services;
using ATAP.IntegrationTests.Infrastructure;

namespace ATAP.IntegrationTests.BusinessLogic;

[Trait("Category", "Integration")]
public class AuthService_Register_DuplicateUsername_ReturnsFalse_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public AuthService_Register_DuplicateUsername_ReturnsFalse_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task RegisterAsync_TakenUsernameOrEmail_ReturnsFalseAndAddsNoRow()
    {
        // Arrange
        var service = new AuthService(_db.CreateUserRepository());
        var usersBefore = await _db.ScalarAsync<long>("SELECT COUNT(*) FROM dds.users");

        // Act
        var byUsername = await service.RegisterAsync("admin", "another@atap.com", "S3cure!pass");
        var byEmail = await service.RegisterAsync("another_user", "admin@atap.com", "S3cure!pass");

        // Assert
        Assert.False(byUsername);
        Assert.False(byEmail);
        Assert.Equal(usersBefore, await _db.ScalarAsync<long>("SELECT COUNT(*) FROM dds.users"));
    }
}
