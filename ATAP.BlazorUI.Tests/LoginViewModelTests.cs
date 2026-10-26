using ATAP.BlazorUI.Services;
using ATAP.BlazorUI.ViewModels;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Services;
using Allure.Xunit.Attributes;
using Moq;
using Xunit;

namespace ATAP.BlazorUI.Tests;

/// <summary>
/// Unit-тесты формы входа (<see cref="LoginViewModel"/>): сценарий 2 из README — пустое поле, неверные данные
/// («неизвестный логин и неверный пароль дают одно и то же сообщение»), успешный вход.
/// </summary>
[AllureSuite("LoginViewModel")]
public class LoginViewModelTests
{
    private static (LoginViewModel Model, Mock<IAuthService> Auth) Create(UserDto? loginResult = null)
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.LoginAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(loginResult);
        return (new LoginViewModel(auth.Object, new AppStateService()), auth);
    }

    private static void NeverCalled(Mock<IAuthService> auth) =>
        auth.Verify(a => a.LoginAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

    [Theory]
    [AllureFeature("Login form")]
    [InlineData("")]
    [InlineData("  ")]
    public async Task LoginAsync_WithEmptyUsername_ShouldShowErrorAndNotCallService(string username)
    {
        // Arrange
        var (model, auth) = Create();
        model.Username = username; model.Password = "S3cure!pass";

        // Act
        var user = await model.LoginAsync();

        // Assert
        Assert.Null(user);
        Assert.Equal("Введите логин", model.Error);
        NeverCalled(auth);
    }

    [Fact]
    [AllureFeature("Login form")]
    public async Task LoginAsync_WithEmptyPassword_ShouldShowErrorAndNotCallService()
    {
        // Arrange
        var (model, auth) = Create();
        model.Username = "anna"; model.Password = "";

        // Act
        var user = await model.LoginAsync();

        // Assert
        Assert.Null(user);
        Assert.Equal("Введите пароль", model.Error);
        NeverCalled(auth);
    }

    [Fact]
    [AllureFeature("Login form")]
    public async Task LoginAsync_WithWrongCredentials_ShouldShowSingleMessageForAnyCause()
    {
        // Arrange (сервис не нашёл пользователя или пароль неверен — клиент не различает причины)
        var (model, _) = Create(loginResult: null);
        model.Username = "anna"; model.Password = "wrong";

        // Act
        var user = await model.LoginAsync();

        // Assert
        Assert.Null(user);
        Assert.Equal("Неверный логин или пароль", model.Error);
    }

    [Fact]
    [AllureFeature("Login form")]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnUserWithoutError()
    {
        // Arrange
        var expected = new UserDto { UserId = 7, Username = "anna", Role = "analyst" };
        var (model, auth) = Create(loginResult: expected);
        model.Username = "anna"; model.Password = "S3cure!pass";

        // Act
        var user = await model.LoginAsync();

        // Assert
        Assert.Same(expected, user);
        Assert.Null(model.Error);
        Assert.False(model.IsLoading);
        auth.Verify(a => a.LoginAsync("anna", "S3cure!pass"), Times.Once);
    }

    [Fact]
    [AllureFeature("Login form")]
    public async Task LoginAsync_WhenServiceThrows_ShouldShowConnectionErrorInsteadOfCrashing()
    {
        // Arrange
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("нет соединения"));
        var model = new LoginViewModel(auth.Object, new AppStateService()) { Username = "anna", Password = "p" };

        // Act
        var user = await model.LoginAsync();

        // Assert
        Assert.Null(user);
        Assert.Equal("Ошибка подключения: нет соединения", model.Error);
        Assert.False(model.IsLoading);
    }
}
