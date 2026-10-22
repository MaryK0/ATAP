namespace ATAP.IntegrationTests.Infrastructure;

/// <summary>
/// Адреса тестового стенда. Задаются переменными окружения, которые печатает
/// <c>scripts/test-stand.sh up</c>; значения по умолчанию — для ручного запуска контейнеров.
/// Рабочая БД (порт 5433) тестами не используется.
/// </summary>
public static class TestEnvironment
{
    /// <summary>Подключение к серверу PostgreSQL тестового стенда (без имени базы).</summary>
    public static string PostgresBase { get; } =
        Environment.GetEnvironmentVariable("ATAP_TEST_PG")
        ?? "Host=127.0.0.1;Port=5434;Username=postgres;Password=test_pass";

    /// <summary>Адрес Redis тестового стенда.</summary>
    public static string Redis { get; } =
        Environment.GetEnvironmentVariable("ATAP_TEST_REDIS") ?? "127.0.0.1:6380";

    /// <summary>База-шаблон с готовой структурой и тестовыми данными (создаётся образом atap-test-db).</summary>
    public const string TemplateDatabase = "atap_db";
}
