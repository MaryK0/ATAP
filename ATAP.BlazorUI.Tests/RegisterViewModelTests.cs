using ATAP.BlazorUI.ViewModels;
using ATAP.BusinessLogic.Interfaces.Services;
using Allure.Xunit.Attributes;
using Moq;
using Xunit;

namespace ATAP.BlazorUI.Tests;

/// <summary>
/// Unit-тесты формы регистрации (<see cref="RegisterViewModel"/>): критерии приёмки сценария 1 из README —
/// «пустое поле → запрос к БД не выполняется, показано, какое поле не заполнено»; «логин или email заняты → показано сообщение».
/// Сервис аутентификации подменён mock-объектом, поэтому проверяется и то, что сервис при пустых полях не вызывается.
/// </summary>
[AllureSuite("RegisterViewModel")]
public class RegisterViewModelTests
{
    private static (RegisterViewModel Model, Mock<IAuthService> Auth) Create(bool registerResult = true)
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(registerResult);
        return (new RegisterViewModel(auth.Object), auth);
    }

    private static void NeverCalled(Mock<IAuthService> auth) =>
        auth.Verify(a => a.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);

    [Theory]
    [AllureFeature("Registration form")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterAsync_WithEmptyUsername_ShouldShowLoginErrorAndNotCallService(string username)
    {
        // Arrange (эквивалентное разбиение: пустая строка и строка из пробелов — один класс «не заполнено»)
        var (model, auth) = Create();
        model.Username = username; model.Email = "a@atap.com"; model.Password = "S3cure!pass";

        // Act
        var ok = await model.RegisterAsync();

        // Assert
        Assert.False(ok);
        Assert.Equal("Введите логин", model.Error);
        Assert.False(model.Success);
        NeverCalled(auth);
    }

    [Fact]
    [AllureFeature("Registration form")]
    public async Task RegisterAsync_WithEmptyEmail_ShouldShowEmailErrorAndNotCallService()
    {
        // Arrange
        var (model, auth) = Create();
        model.Username = "anna"; model.Email = "  "; model.Password = "S3cure!pass";

        // Act
        var ok = await model.RegisterAsync();

        // Assert
        Assert.False(ok);
        Assert.Equal("Введите email", model.Error);
        NeverCalled(auth);
    }

    [Fact]
    [AllureFeature("Registration form")]
    public async Task RegisterAsync_WithEmptyPassword_ShouldShowPasswordErrorAndNotCallService()
    {
        // Arrange
        var (model, auth) = Create();
        model.Username = "anna"; model.Email = "a@atap.com"; model.Password = "";

        // Act
        var ok = await model.RegisterAsync();

        // Assert
        Assert.False(ok);
        Assert.Equal("Введите пароль", model.Error);
        NeverCalled(auth);
    }

    [Fact]
    [AllureFeature("Registration form")]
    public async Task RegisterAsync_WithAllFieldsEmpty_ShouldReportFirstMissingFieldFirst()
    {
        // Arrange (граничный случай: не заполнено ничего — сообщение о первом поле, порядок: логин, email, пароль)
        var (model, auth) = Create();

        // Act
        var ok = await model.RegisterAsync();

        // Assert
        Assert.False(ok);
        Assert.Equal("Введите логин", model.Error);
        NeverCalled(auth);
    }

    [Fact]
    [AllureFeature("Registration form")]
    public async Task RegisterAsync_WithValidData_ShouldSucceedAndPassFieldsToService()
    {
        // Arrange
        var (model, auth) = Create(registerResult: true);
        model.Username = "anna"; model.Email = "a@atap.com"; model.Password = "S3cure!pass";

        // Act
        var ok = await model.RegisterAsync();

        // Assert
        Assert.True(ok);
        Assert.True(model.Success);
        Assert.Null(model.Error);
        Assert.False(model.IsLoading);
        auth.Verify(a => a.RegisterAsync("anna", "a@atap.com", "S3cure!pass"), Times.Once);
    }

    [Fact]
    [AllureFeature("Registration form")]
    public async Task RegisterAsync_WhenLoginOrEmailTaken_ShouldShowDuplicateMessage()
    {
        // Arrange (сервис отказал: логин или email заняты)
        var (model, _) = Create(registerResult: false);
        model.Username = "admin"; model.Email = "admin@atap.com"; model.Password = "S3cure!pass";

        // Act
        var ok = await model.RegisterAsync();

        // Assert
        Assert.False(ok);
        Assert.False(model.Success);
        Assert.Equal("Пользователь с таким логином или email уже существует", model.Error);
    }

    [Fact]
    [AllureFeature("Registration form")]
    public async Task RegisterAsync_WhenServiceThrows_ShouldShowErrorInsteadOfCrashing()
    {
        // Arrange (недоступность хранилища не должна ронять страницу)
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("нет соединения"));
        var model = new RegisterViewModel(auth.Object)
        {
            Username = "anna", Email = "a@atap.com", Password = "S3cure!pass"
        };

        // Act
        var ok = await model.RegisterAsync();

        // Assert
        Assert.False(ok);
        Assert.Equal("Ошибка: нет соединения", model.Error);
        Assert.False(model.IsLoading);
    }

    [Fact]
    [AllureFeature("Registration form")]
    public async Task RegisterAsync_AfterFailureThenCorrection_ShouldClearPreviousError()
    {
        // Arrange (переход состояний: ошибка → исправление → успех; прежняя ошибка сбрасывается)
        var (model, _) = Create(registerResult: true);
        model.Username = "anna"; model.Email = ""; model.Password = "S3cure!pass";
        await model.RegisterAsync();
        Assert.Equal("Введите email", model.Error);
        model.Email = "a@atap.com";

        // Act
        var ok = await model.RegisterAsync();

        // Assert
        Assert.True(ok);
        Assert.Null(model.Error);
    }
}
