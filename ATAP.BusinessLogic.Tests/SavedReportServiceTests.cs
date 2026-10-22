using Xunit;
using ATAP.BusinessLogic.Services;
using ATAP.BusinessLogic.Tests.Mocks;
using ATAP.BusinessLogic.Tests.TestData.Builders;
using Allure.Xunit.Attributes;

namespace ATAP.BusinessLogic.Tests;

/// <summary>
/// Classical unit-тесты для <see cref="SavedReportService"/>.
/// Техника подготовки данных: граничные значения (boundary value analysis) для периода отчёта —
/// date_from == date_to (валидная граница), date_from == date_to + 1 день (первое невалидное значение
/// за границей), плюс тесты исключений через Data Builder (<see cref="CreateSavedReportDtoBuilder"/>).
/// </summary>
[AllureSuite("SavedReportService")]
public class SavedReportServiceTests
{
    [Fact]
    [AllureFeature("Saved reports")]
    public async Task SaveAsync_WithValidDto_ShouldPersistReportForUser()
    {
        // Arrange
        var repo = new MockSavedReportRepository();
        var service = new SavedReportService(repo);
        var dto = new CreateSavedReportDtoBuilder().WithName("Q1 SVO").WithAirportId(1).Build();

        // Act
        await service.SaveAsync(userId: 7, dto);

        // Assert
        Assert.True(repo.Contains(userId: 7, name: "Q1 SVO"));
        Assert.Equal(1, repo.CountForUser(7));
    }

    [Fact]
    [AllureFeature("Saved reports")]
    public async Task SaveAsync_WithSingleDayPeriod_ShouldSucceed()
    {
        // Arrange (граничное значение: date_from == date_to — валидно по CHECK в БД)
        var repo = new MockSavedReportRepository();
        var service = new SavedReportService(repo);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var dto = new CreateSavedReportDtoBuilder().WithName("Один день").WithSingleDayPeriod(today).Build();

        // Act
        await service.SaveAsync(userId: 1, dto);

        // Assert
        Assert.True(repo.Contains(1, "Один день"));
    }

    [Fact]
    [AllureFeature("Saved reports")]
    public async Task SaveAsync_WithInvertedPeriod_ShouldThrowArgumentException()
    {
        // Arrange (граничное невалидное значение: date_to раньше date_from)
        var repo = new MockSavedReportRepository();
        var service = new SavedReportService(repo);
        var dto = new CreateSavedReportDtoBuilder().WithName("Плохой период").WithInvertedPeriod().Build();

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(userId: 1, dto));
        Assert.Equal(0, repo.CountForUser(1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [AllureFeature("Saved reports")]
    public async Task SaveAsync_WithBlankName_ShouldThrowArgumentException(string blankName)
    {
        // Arrange (эквивалентное разбиение: "пустая" и "состоящая из пробелов" строка — один класс)
        var repo = new MockSavedReportRepository();
        var service = new SavedReportService(repo);
        var dto = new CreateSavedReportDtoBuilder().WithName(blankName).Build();

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(userId: 1, dto));
    }

    [Fact]
    [AllureFeature("Saved reports")]
    public async Task GetByUserAsync_WithSavedReports_ShouldReturnOnlyThatUsersReports()
    {
        // Arrange
        var repo = new MockSavedReportRepository();
        var service = new SavedReportService(repo);
        await service.SaveAsync(1, new CreateSavedReportDtoBuilder().WithName("Мой отчёт").Build());
        await service.SaveAsync(2, new CreateSavedReportDtoBuilder().WithName("Чужой отчёт").Build());

        // Act
        var result = await service.GetByUserAsync(1);

        // Assert
        Assert.Single(result);
        Assert.Equal("Мой отчёт", result[0].Name);
    }

    [Fact]
    [AllureFeature("Saved reports")]
    public async Task GetByUserAsync_WithNoReports_ShouldReturnEmptyList()
    {
        // Arrange (граничное значение: пользователь без отчётов)
        var repo = new MockSavedReportRepository();
        var service = new SavedReportService(repo);

        // Act
        var result = await service.GetByUserAsync(42);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    [AllureFeature("Saved reports")]
    public async Task DeleteAsync_WithOwnedReport_ShouldRemoveIt()
    {
        // Arrange
        var repo = new MockSavedReportRepository();
        var service = new SavedReportService(repo);
        await service.SaveAsync(1, new CreateSavedReportDtoBuilder().WithName("К удалению").Build());
        var reportId = (await service.GetByUserAsync(1))[0].Id;

        // Act
        await service.DeleteAsync(reportId, userId: 1);

        // Assert
        Assert.Equal(0, repo.CountForUser(1));
    }

    [Fact]
    [AllureFeature("Saved reports")]
    public async Task DeleteAsync_WithForeignReportId_ShouldNotRemoveAnything()
    {
        // Arrange — попытка удалить чужой отчёт (id принадлежит другому userId)
        var repo = new MockSavedReportRepository();
        var service = new SavedReportService(repo);
        await service.SaveAsync(1, new CreateSavedReportDtoBuilder().WithName("Чужое").Build());
        var reportId = (await service.GetByUserAsync(1))[0].Id;

        // Act
        await service.DeleteAsync(reportId, userId: 999);

        // Assert — запись осталась нетронутой, т.к. userId не совпал
        Assert.Equal(1, repo.CountForUser(1));
    }
}
