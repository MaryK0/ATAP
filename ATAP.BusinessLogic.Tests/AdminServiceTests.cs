using Xunit;
using ATAP.BusinessLogic.Services;
using ATAP.BusinessLogic.Tests.Mocks;
using ATAP.BusinessLogic.Tests.TestData.Mothers;
using Allure.Xunit.Attributes;

namespace ATAP.BusinessLogic.Tests;

/// <summary>
/// Classical (state-based, без mock-фреймворка) unit-тесты для <see cref="AdminService"/>.
/// Коллаборатор — <see cref="MockUserRepository"/>, самописный in-memory fake (не mock-объект).
/// Техника подготовки данных: Object Mother (<see cref="Users"/>) + эквивалентное разбиение
/// (существующий/несуществующий пользователь) и граничное значение (пустой список пользователей).
/// </summary>
[AllureSuite("AdminService")]
public class AdminServiceTests
{
    [Fact]
    [AllureFeature("Users management")]
    public async Task GetAllUsersAsync_WithSeededUsers_ShouldReturnMappedDtos()
    {
        // Arrange
        var repo = new MockUserRepository();
        await repo.CreateAsync(Users.AnalystAnna());
        await repo.CreateAsync(Users.AdminRoot());
        var service = new AdminService(repo);

        // Act
        var result = await service.GetAllUsersAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, u => u.Username == "anna" && u.Role == "analyst");
        Assert.Contains(result, u => u.Username == "root" && u.Role == "admin");
    }

    [Fact]
    [AllureFeature("Users management")]
    public async Task GetAllUsersAsync_WithNoUsers_ShouldReturnEmptyList()
    {
        // Arrange (граничное значение: пустая коллекция)
        var repo = new MockUserRepository();
        var service = new AdminService(repo);

        // Act
        var result = await service.GetAllUsersAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    [AllureFeature("Users management")]
    public async Task DeleteUserAsync_WithAdminUser_ShouldThrowAndKeepUser()
    {
        // Arrange (FR-16: пользователя с ролью admin удалить нельзя; «удалить себя» — тот же случай,
        // потому что удалять пользователей может только администратор)
        var repo = new MockUserRepository();
        var adminId = await repo.CreateAsync(Users.AdminRoot());
        var service = new AdminService(repo);

        // Act + Assert
        await Assert.ThrowsAsync<AdminDeletionForbiddenException>(() => service.DeleteUserAsync(adminId));
        var remaining = await service.GetAllUsersAsync();
        Assert.Contains(remaining, u => u.UserId == adminId && u.Role == "admin");
    }

    [Fact]
    [AllureFeature("Users management")]
    public async Task DeleteUserAsync_WithAnalystWhileAdminExists_ShouldRemoveOnlyAnalyst()
    {
        // Arrange (администратор в системе есть, удаляется обычный пользователь — правило не мешает)
        var repo = new MockUserRepository();
        await repo.CreateAsync(Users.AdminRoot());
        var analystId = await repo.CreateAsync(Users.AnalystAnna());
        var service = new AdminService(repo);

        // Act
        var deleted = await service.DeleteUserAsync(analystId);

        // Assert
        Assert.True(deleted);
        var remaining = await service.GetAllUsersAsync();
        Assert.Equal(new[] { "root" }, remaining.Select(u => u.Username).ToArray());
    }

    [Fact]
    [AllureFeature("Users management")]
    public async Task UpdateUserAsync_WithExistingUser_ShouldPersistChangesAndReturnTrue()
    {
        // Arrange
        var repo = new MockUserRepository();
        var userId = await repo.CreateAsync(Users.AnalystAnna());
        var service = new AdminService(repo);

        // Act
        var updated = await service.UpdateUserAsync(userId, "anna2", "anna2@atap.test", newPassword: null, role: "analyst");

        // Assert
        Assert.True(updated);
        var stored = await repo.GetByUsernameAsync("anna2");
        Assert.NotNull(stored);
        Assert.Equal("anna2@atap.test", stored!.Email);
    }

    [Fact]
    [AllureFeature("Users management")]
    public async Task UpdateUserAsync_WithNonExistentUser_ShouldReturnFalse()
    {
        // Arrange (эквивалентный класс "пользователь не найден")
        var repo = new MockUserRepository();
        var service = new AdminService(repo);

        // Act
        var updated = await service.UpdateUserAsync(999, "ghost", "ghost@atap.test", null, "analyst");

        // Assert
        Assert.False(updated);
    }

    [Fact]
    [AllureFeature("Users management")]
    public async Task UpdateUserAsync_WithNewPassword_ShouldStoreDifferentHashThanPlainPassword()
    {
        // Arrange
        var repo = new MockUserRepository();
        var userId = await repo.CreateAsync(Users.AnalystAnna());
        var service = new AdminService(repo);

        // Act
        await service.UpdateUserAsync(userId, "anna", "anna@atap.test", newPassword: "newpass123", role: "analyst");

        // Assert — пароль в хранилище не совпадает с открытым текстом (базовая гарантия хэширования)
        var stored = await repo.GetByUsernameAsync("anna");
        Assert.NotNull(stored);
        Assert.NotEqual("newpass123", stored!.PasswordHash);
    }

    [Fact]
    [AllureFeature("Users management")]
    public async Task DeleteUserAsync_WithExistingUser_ShouldRemoveAndReturnTrue()
    {
        // Arrange
        var repo = new MockUserRepository();
        var userId = await repo.CreateAsync(Users.AnalystAnna());
        var service = new AdminService(repo);

        // Act
        var deleted = await service.DeleteUserAsync(userId);

        // Assert
        Assert.True(deleted);
        Assert.Null(await repo.GetByUsernameAsync("anna"));
    }

    [Fact]
    [AllureFeature("Users management")]
    public async Task DeleteUserAsync_WithNonExistentUser_ShouldReturnFalse()
    {
        // Arrange
        var repo = new MockUserRepository();
        var service = new AdminService(repo);

        // Act
        var deleted = await service.DeleteUserAsync(12345);

        // Assert
        Assert.False(deleted);
    }
}
