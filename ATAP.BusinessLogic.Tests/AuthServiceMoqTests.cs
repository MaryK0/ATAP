using Xunit;
using Moq;
using ATAP.BusinessLogic.Services;
using ATAP.BusinessLogic.Interfaces.Repositories;
using Allure.Xunit.Attributes;

namespace ATAP.BusinessLogic.Tests;

/// <summary>
/// "Лондонский" (mockist) вариант тестов <see cref="AuthService"/> — с mock-фреймворком Moq вместо
/// самописных fake-объектов. Проверяем не итоговое состояние, а ВЗАИМОДЕЙСТВИЕ с коллаборатором
/// (какие методы репозитория вызваны и с какими аргументами).
///
/// Для сравнения — те же сценарии в classical-стиле: <see cref="AuthServiceTests.RegisterAsync_WithNewUser_ShouldSucceed"/>
/// и <see cref="AuthServiceTests.LoginAsync_WithNonExistentUser_ShouldFail"/>.
/// </summary>
[AllureSuite("AuthService (London/mock style)")]
public class AuthServiceMoqTests
{
    [Fact]
    [AllureFeature("Registration")]
    public async Task RegisterAsync_WithNewUser_ShouldCallCreateAsyncWithHashedPasswordAndAnalystRole()
    {
        // Arrange
        // Strict — mock упадёт с исключением на ЛЮБОМ вызове, который явно не разрешён ниже.
        // Это заставляет прописать все 3 обращения, которые реально делает AuthService.RegisterAsync
        // по порядку: GetByUsernameAsync -> GetByEmailAsync -> CreateAsync.
        var repoMock = new Mock<IUserRepository>(MockBehavior.Strict);

        // Ответ на 1-й внутренний вызов: null = "логин свободен", RegisterAsync не остановится на первой проверке.
        repoMock.Setup(r => r.GetByUsernameAsync("newuser")).ReturnsAsync((UserDb?)null);
        // Ответ на 2-й внутренний вызов: null = "email свободен", не остановится на второй проверке.
        repoMock.Setup(r => r.GetByEmailAsync("new@test.com")).ReturnsAsync((UserDb?)null);
        // Ответ на 3-й внутренний вызов — и одновременно проверка, КАКИМ объектом его вызвали:
        // It.Is<UserDb>(...) сработает только если AuthService сам правильно собрал UserDb —
        // поставил роль "analyst" и реально хэшировал пароль (PasswordHash != открытый текст).
        // Если бы сборка объекта была неправильной — Setup не совпал бы, и Strict-мок упал бы
        // со своим исключением ещё до Assert.
        repoMock
            .Setup(r => r.CreateAsync(It.Is<UserDb>(u =>
                u.Username == "newuser" &&
                u.Email == "new@test.com" &&
                u.Role == "analyst" &&
                u.PasswordHash != "pass123")))
            .ReturnsAsync(1);
        var service = new AuthService(repoMock.Object);

        // Act
        var result = await service.RegisterAsync("newuser", "new@test.com", "pass123");

        // Assert — и итоговый результат, и (главное для London-стиля) сам факт вызова с нужными аргументами
        Assert.True(result);
        repoMock.Verify(r => r.CreateAsync(It.IsAny<UserDb>()), Times.Once);
    }

    [Fact]
    [AllureFeature("Registration")]
    public async Task RegisterAsync_WithExistingUsername_ShouldNeverCallCreateAsync()
    {
        // Arrange
        var repoMock = new Mock<IUserRepository>(MockBehavior.Strict);
        repoMock.Setup(r => r.GetByUsernameAsync("existing")).ReturnsAsync(new UserDb { UserId = 1, Username = "existing" });
        var service = new AuthService(repoMock.Object);

        // Act
        var result = await service.RegisterAsync("existing", "another@test.com", "pass");

        // Assert — интересует не только false, но и то, что запись в БД точно не создавалась
        Assert.False(result);
        repoMock.Verify(r => r.CreateAsync(It.IsAny<UserDb>()), Times.Never);
    }

    [Fact]
    [AllureFeature("Login")]
    public async Task LoginAsync_WithNonExistentUser_ShouldOnlyQueryRepositoryOnce()
    {
        // Arrange
        var repoMock = new Mock<IUserRepository>(MockBehavior.Strict);
        repoMock.Setup(r => r.GetByUsernameAsync("nonexistent")).ReturnsAsync((UserDb?)null);
        var service = new AuthService(repoMock.Object);

        // Act
        var result = await service.LoginAsync("nonexistent", "anypass");

        // Assert
        Assert.Null(result);
        repoMock.Verify(r => r.GetByUsernameAsync("nonexistent"), Times.Once);
    }
}
