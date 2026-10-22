using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.IntegrationTests.Infrastructure;
using Npgsql;

namespace ATAP.IntegrationTests.DataAccess;

[Trait("Category", "Integration")]
public class UserRepository_Create_DuplicateUsername_Throws_IntegrationTest : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public UserRepository_Create_DuplicateUsername_Throws_IntegrationTest(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task CreateAsync_UsernameAlreadyTaken_ViolatesUniqueConstraintAndAddsNothing()
    {
        // Arrange
        var repository = _db.CreateUserRepository();
        var usersBefore = await _db.ScalarAsync<long>("SELECT COUNT(*) FROM dds.users");
        var duplicate = new UserDb
        {
            Username = "admin",
            Email = "other@test.com",
            PasswordHash = "hash",
            Role = "analyst"
        };

        // Act
        var error = await Assert.ThrowsAsync<PostgresException>(() => repository.CreateAsync(duplicate));

        // Assert
        Assert.Equal("23505", error.SqlState);
        var usersAfter = await _db.ScalarAsync<long>("SELECT COUNT(*) FROM dds.users");
        Assert.Equal(usersBefore, usersAfter);
    }
}
