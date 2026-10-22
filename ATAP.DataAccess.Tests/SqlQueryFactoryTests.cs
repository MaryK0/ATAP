using Xunit;
using ATAP.DataAccess.Factories;
using Allure.Xunit.Attributes;

namespace ATAP.DataAccess.Tests;

/// <summary>
/// Классические unit-тесты <see cref="SqlQueryFactory"/> — единственного компонента слоя доступа
/// к данным, который является чистой логикой (построение текста SQL-запроса) без обращения к БД,
/// сети или файловой системе. Не требует mock/stub и работает полностью offline.
/// Техника подготовки данных: эквивалентное разбиение по булеву параметру includeDetails
/// (true/false — два класса эквивалентности с разным набором колонок и ORDER BY).
/// </summary>
[AllureSuite("SqlQueryFactory")]
public class SqlQueryFactoryTests
{
    private readonly SqlQueryFactory _factory = new();

    [Fact]
    [AllureFeature("Congestion query")]
    public void GetCongestionQuery_WithIncludeDetailsTrue_ShouldSelectDetailedColumnsAndOrderByEntryTime()
    {
        // Arrange — factory создана в поле (общий Arrange для всех тестов класса)

        // Act
        var sql = _factory.GetCongestionQuery(includeDetails: true);

        // Assert
        Assert.Contains("callsign", sql);
        Assert.Contains("entry_time", sql);
        Assert.Contains("exit_time", sql);
        Assert.Contains("ORDER BY entry_time", sql);
        Assert.Contains("mart.mart_airport_congestion", sql);
    }

    [Fact]
    [AllureFeature("Congestion query")]
    public void GetCongestionQuery_WithIncludeDetailsFalse_ShouldOmitDetailedColumnsAndOrdering()
    {
        // Act (Arrange — см. комментарий в предыдущем тесте: общее поле _factory на весь класс)
        var sql = _factory.GetCongestionQuery(includeDetails: false);

        // Assert — "негативный" по отношению к предыдущему тесту: то, чего в кратком варианте быть не должно
        Assert.DoesNotContain("callsign", sql);
        Assert.DoesNotContain("ORDER BY", sql);
        Assert.Contains("is_departure", sql);
        Assert.Contains("is_arrival", sql);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [AllureFeature("Congestion query")]
    public void GetCongestionQuery_AnyVariant_ShouldFilterByAirportIdAndPeriod(bool includeDetails)
    {
        // Act — ровно один вызов на тест-кейс; было 2 вызова в одном [Fact] (не допускается п.5 задания),
        // исправлено разбиением на параметризованный [Theory] по обоим значениям includeDetails
        var sql = _factory.GetCongestionQuery(includeDetails);

        // Assert — общий инвариант, не зависящий от ветки includeDetails
        Assert.Contains("@airportId", sql);
        Assert.Contains("@start", sql);
        Assert.Contains("@end", sql);
    }

    [Fact]
    [AllureFeature("Map queries")]
    public void GetCurrentFlightsQuery_ShouldSelectFromCurrentFlightsMap()
    {
        // Act
        var sql = _factory.GetCurrentFlightsQuery();

        // Assert
        Assert.Contains("mart.current_flights_map", sql);
        Assert.Contains("flight_id", sql);
        Assert.Contains("callsign", sql);
    }

    [Fact]
    [AllureFeature("Map queries")]
    public void GetCurrentFlightsQuery_ShouldNotReferenceUnrelatedSchemas()
    {
        // Act (негативная проверка — запрос карты не должен трогать dds/пользователей)
        var sql = _factory.GetCurrentFlightsQuery();

        // Assert
        Assert.DoesNotContain("dds.users", sql);
        Assert.DoesNotContain("mart.mart_airport_congestion", sql);
    }

    [Fact]
    [AllureFeature("Map queries")]
    public void GetAirportsQuery_ShouldSelectFromAirportsTable()
    {
        // Act
        var sql = _factory.GetAirportsQuery();

        // Assert
        Assert.Contains("dds.airports", sql);
        Assert.Contains("icao_code", sql);
    }

    [Fact]
    [AllureFeature("Map queries")]
    public void GetAirportsQuery_ShouldNotFilterByAnyParameter()
    {
        // Act (негативная проверка — справочник аэропортов возвращается целиком, без WHERE)
        var sql = _factory.GetAirportsQuery();

        // Assert
        Assert.DoesNotContain("WHERE", sql);
        Assert.DoesNotContain("@", sql);
    }

    [Fact]
    [AllureFeature("User queries")]
    public void GetUserByUsernameQuery_ShouldFilterByUsernameParameter()
    {
        // Act
        var sql = _factory.GetUserByUsernameQuery();

        // Assert
        Assert.Contains("WHERE username = @username", sql);
        Assert.Contains("password_hash", sql);
    }

    [Fact]
    [AllureFeature("User queries")]
    public void GetUserByUsernameQuery_ShouldNotLeakUnrelatedTables()
    {
        // Act (негативная проверка — запрос по пользователю не должен трогать другие схемы)
        var sql = _factory.GetUserByUsernameQuery();

        // Assert
        Assert.DoesNotContain("mart.", sql);
        Assert.DoesNotContain("dds.airports", sql);
    }

    [Fact]
    [AllureFeature("User queries")]
    public void CreateUserQuery_ShouldInsertIntoUsersAndReturnGeneratedId()
    {
        // Act
        var sql = _factory.CreateUserQuery();

        // Assert
        Assert.Contains("INSERT INTO dds.users", sql);
        Assert.Contains("@username", sql);
        Assert.Contains("@email", sql);
        Assert.Contains("@passwordHash", sql);
        Assert.Contains("RETURNING user_id", sql);
    }

    [Fact]
    [AllureFeature("User queries")]
    public void CreateUserQuery_ShouldNotExplicitlyInsertGeneratedIdColumn()
    {
        // Act (негативная проверка — user_id генерируется SERIAL-последовательностью в БД,
        // явно передавать его в INSERT нельзя)
        var sql = _factory.CreateUserQuery();

        // Assert
        Assert.DoesNotContain("user_id,", sql);
        Assert.DoesNotContain("@userId", sql);
    }
}
