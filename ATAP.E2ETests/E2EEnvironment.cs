namespace ATAP.E2ETests;

/// <summary>
/// Адрес тестируемого приложения и учётные записи сценария. Адрес печатает
/// <c>scripts/test-stand.sh up</c> (переменная ATAP_TEST_API); учётные записи заведены в
/// db/test/03_seed_test_data.sql.
/// </summary>
public static class E2EEnvironment
{
    public static string ApiBaseUrl { get; } =
        Environment.GetEnvironmentVariable("ATAP_TEST_API") ?? "http://127.0.0.1:5080";

    public static string AnalystUsername { get; } =
        Environment.GetEnvironmentVariable("ATAP_E2E_ANALYST") ?? "analyst1";

    public static string AnalystPassword { get; } =
        Environment.GetEnvironmentVariable("ATAP_E2E_ANALYST_PASSWORD") ?? "Analyst#2026";
}
