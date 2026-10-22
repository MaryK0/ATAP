using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.BusinessLogic.Tests.TestData.Builders;

namespace ATAP.BusinessLogic.Tests.TestData.Mothers;

/// <summary>
/// Object Mother: набор готовых, "типовых" тестовых объектов с говорящими именами.
/// В отличие от Builder-а (который собирает произвольный вариант), Mother фиксирует конкретные,
/// часто переиспользуемые сценарии, чтобы тесты читались декларативно: "возьми аналитика Аню".
/// </summary>
public static class Users
{
    public static UserDb AnalystAnna() => new UserDbBuilder()
        .WithUserId(1)
        .WithUsername("anna")
        .WithEmail("anna@atap.test")
        .WithPasswordHash("anna-hash")
        .AsAnalyst();

    public static UserDb AdminRoot() => new UserDbBuilder()
        .WithUserId(2)
        .WithUsername("root")
        .WithEmail("root@atap.test")
        .WithPasswordHash("root-hash")
        .AsAdmin();

    /// <summary>Пользователь с "грязными" (пограничными по длине) данными — для тестов на устойчивость.</summary>
    public static UserDb WithEmptyUsername() => new UserDbBuilder()
        .WithUsername(string.Empty)
        .WithEmail("noname@atap.test")
        .AsAnalyst();
}
