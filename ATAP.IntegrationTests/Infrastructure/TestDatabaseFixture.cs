using ATAP.DataAccess.Factories;
using ATAP.DataAccess.Repositories;
using Npgsql;

namespace ATAP.IntegrationTests.Infrastructure;

/// <summary>
/// Своя копия тестовой БД для каждого тестового класса: CREATE DATABASE ... TEMPLATE atap_db.
/// Каждый тест начинает с чистого состояния (структура + тестовые данные), портит только свою копию,
/// а по окончании копия удаляется — повторные и параллельные запуски не влияют друг на друга.
/// </summary>
public sealed class TestDatabaseFixture : IAsyncLifetime
{
    // CREATE DATABASE из одного шаблона требует, чтобы к нему никто не подключался; копии
    // создаются по одной внутри процесса и под advisory-блокировкой между процессами.
    private static readonly SemaphoreSlim CloneLock = new(1, 1);
    private const long AdvisoryLockKey = 7357;

    private string _databaseName = "";

    public string ConnectionString { get; private set; } = "";

    public PostgresUserRepository CreateUserRepository()
        => new(ConnectionString, new SqlQueryFactory());

    public PostgresAnalyticsRepository CreateAnalyticsRepository()
        => new(ConnectionString, new SqlQueryFactory());

    public PostgresSavedReportRepository CreateSavedReportRepository()
        => new(ConnectionString, new SqlQueryFactory());

    public async Task InitializeAsync()
    {
        _databaseName = $"atap_it_{Guid.NewGuid():N}";
        var admin = new NpgsqlConnectionStringBuilder(TestEnvironment.PostgresBase) { Database = "postgres" };

        await CloneLock.WaitAsync();
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await using var connection = new NpgsqlConnection(admin.ConnectionString);
                    await connection.OpenAsync();
                    await RunAsync(connection, $"SELECT pg_advisory_lock({AdvisoryLockKey})");
                    try
                    {
                        await RunAsync(connection,
                            $"CREATE DATABASE \"{_databaseName}\" TEMPLATE {TestEnvironment.TemplateDatabase}");
                    }
                    finally
                    {
                        await RunAsync(connection, $"SELECT pg_advisory_unlock({AdvisoryLockKey})");
                    }
                    break;
                }
                // 55006 — шаблон занят другим копированием; повторяем
                catch (PostgresException e) when (e.SqlState == "55006" && attempt < 20)
                {
                    await Task.Delay(100 * attempt);
                }
            }
        }
        finally
        {
            CloneLock.Release();
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(TestEnvironment.PostgresBase)
        {
            Database = _databaseName
        }.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (_databaseName.Length == 0) return;

        using (var own = new NpgsqlConnection(ConnectionString))
            NpgsqlConnection.ClearPool(own);

        var admin = new NpgsqlConnectionStringBuilder(TestEnvironment.PostgresBase) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await RunAsync(connection, $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)");
    }

    /// <summary>Скалярный запрос к копии БД — для проверки состояния напрямую, мимо репозиториев.</summary>
    public async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        var result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T));
    }

    /// <summary>Команда без результата к копии БД (подготовка данных в блоке Arrange).</summary>
    public async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RunAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
