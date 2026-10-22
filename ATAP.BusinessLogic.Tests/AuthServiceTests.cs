using Xunit;
using ATAP.BusinessLogic.Services;
using ATAP.BusinessLogic.Tests.Mocks;
using ATAP.BusinessLogic.Tests.TestData.Mothers;
using Allure.Xunit.Attributes;

namespace ATAP.BusinessLogic.Tests;

/// <summary>
/// Classical (state-based) unit-тесты для <see cref="AuthService"/> с fake-репозиторием
/// <see cref="MockUserRepository"/>. Технику см. в TESTING.md: эквивалентное разбиение
/// (существующий / несуществующий логин, верный / неверный пароль) + переход состояний
/// (не залогинен → залогинен → разлогинен).
/// Для сравнения с "Лондонским" стилем (mock + verify) см. <see cref="AuthServiceMoqTests"/>.
/// </summary>
[AllureSuite("AuthService")]
public class AuthServiceTests
{
    [Fact]
    [AllureFeature("Registration")]
    public async Task RegisterAsync_WithNewUser_ShouldSucceed()
    {
        // Arrange
        var mockRepo = new MockUserRepository();
        var service = new AuthService(mockRepo);

        // Act
        var result = await service.RegisterAsync("newuser", "new@test.com", "pass123");

        // Assert
        Assert.True(result);
    }

    [Fact]
    [AllureFeature("Registration")]
    public async Task RegisterAsync_WithExistingUsername_ShouldFail()
    {
        // Arrange
        var mockRepo = new MockUserRepository();
        var service = new AuthService(mockRepo);
        await service.RegisterAsync("existing", "existing@test.com", "pass");

        // Act
        var result = await service.RegisterAsync("existing", "another@test.com", "pass");

        // Assert
        Assert.False(result);
    }

    [Fact]
    [AllureFeature("Registration")]
    public async Task RegisterAsync_WithExistingEmail_ShouldFail()
    {
        // Arrange (второй класс эквивалентности конфликта — уникален email, а не логин)
        var mockRepo = new MockUserRepository();
        var service = new AuthService(mockRepo);
        await service.RegisterAsync("first", "shared@test.com", "pass");

        // Act
        var result = await service.RegisterAsync("second", "shared@test.com", "pass");

        // Assert
        Assert.False(result);
    }

    [Fact]
    [AllureFeature("Login")]
    public async Task LoginAsync_WithValidCredentials_ShouldSucceed()
    {
        // Arrange
        var mockRepo = new MockUserRepository();
        var service = new AuthService(mockRepo);
        await service.RegisterAsync("testuser", "test@test.com", "correctpass");

        // Act
        var result = await service.LoginAsync("testuser", "correctpass");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("testuser", result!.Username);
    }

    [Fact]
    [AllureFeature("Login")]
    public async Task LoginAsync_WithInvalidPassword_ShouldFail()
    {
        // Arrange
        var mockRepo = new MockUserRepository();
        var service = new AuthService(mockRepo);
        await service.RegisterAsync("testuser", "test@test.com", "correctpass");

        // Act
        var result = await service.LoginAsync("testuser", "wrongpass");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    [AllureFeature("Login")]
    public async Task LoginAsync_WithNonExistentUser_ShouldFail()
    {
        // Arrange
        var mockRepo = new MockUserRepository();
        var service = new AuthService(mockRepo);

        // Act
        var result = await service.LoginAsync("nonexistent", "anypass");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    [AllureFeature("Session")]
    public async Task GetCurrentUserAsync_WithoutLogin_ShouldReturnNull()
    {
        // Arrange (граничное/начальное состояние — до входа в систему)
        var mockRepo = new MockUserRepository();
        var service = new AuthService(mockRepo);

        // Act
        var result = await service.GetCurrentUserAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    [AllureFeature("Session")]
    public async Task GetCurrentUserAsync_AfterLogin_ShouldReturnUser()
    {
        // Arrange — Register+Login здесь являются частью Arrange (создают предусловие),
        // проверяемый ("Act") вызов — только GetCurrentUserAsync().
        var mockRepo = new MockUserRepository();
        var service = new AuthService(mockRepo);
        await service.RegisterAsync("testuser", "test@test.com", "pass");
        await service.LoginAsync("testuser", "pass");

        // Act
        var result = await service.GetCurrentUserAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("testuser", result!.Username);
    }

    [Fact]
    [AllureFeature("Session")]
    public async Task LogoutAsync_AfterLogin_ShouldClearCurrentSession()
    {
        // Arrange — переход состояний: не залогинен → залогинен → (Act) разлогинен
        var mockRepo = new MockUserRepository();
        var service = new AuthService(mockRepo);
        await service.RegisterAsync("testuser", "test@test.com", "pass");
        await service.LoginAsync("testuser", "pass");

        // Act
        await service.LogoutAsync();

        // Assert
        var current = await service.GetCurrentUserAsync();
        Assert.Null(current);
    }

    [Fact]
    [AllureFeature("Session")]
    public async Task LogoutAsync_WithoutActiveSession_ShouldNotThrow()
    {
        // Arrange (граничный случай: выход без предварительного входа — не должно приводить к исключению)
        var mockRepo = new MockUserRepository();
        var service = new AuthService(mockRepo);

        // Act
        var exception = await Record.ExceptionAsync(() => service.LogoutAsync());

        // Assert
        Assert.Null(exception);
    }
}
