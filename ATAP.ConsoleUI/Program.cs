using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using ATAP.DataAccess;
using ATAP.DataAccess.Factories;
using ATAP.DataAccess.Interfaces;
using ATAP.DataAccess.Repositories;
using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.BusinessLogic.Interfaces.Services;
using ATAP.BusinessLogic.Services;
using ATAP.ConsoleUI;
using ATAP.ConsoleUI.Infrastructure;
using ATAP.ConsoleUI.Menus;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var connectionString = config.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Строка подключения 'DefaultConnection' не задана в appsettings.json");

var appSettings = new AppSettings();
config.GetSection("AppSettings").Bind(appSettings);

Console.OutputEncoding = System.Text.Encoding.UTF8;

var defaultLogPath = config["Logging:LogFilePath"]
    ?? throw new InvalidOperationException("Logging:LogFilePath не задан в appsettings.json");

LogConfigurator.Initialize(defaultLogPath);

try
{
    Log.Information("Приложение ATAP ConsoleUI запускается");

    var services = new ServiceCollection();

    services.AddLogging(builder => builder.AddProvider(new DynamicSerilogProvider()));

    var sqlFactory = new SqlQueryFactory();
    var cacheSettings = new CacheSettings();

    services.AddSingleton<ISqlQueryFactory>(sqlFactory);
    services.AddSingleton(cacheSettings);
    services.AddSingleton(appSettings);

    services.AddSingleton<ICacheRepository, NullCacheRepository>();

    var storageBackend = config["StorageBackend"] ?? "postgres";
    var mongoConn      = config.GetConnectionString("MongoDB")!;
    var mongoDb        = config["MongoDB:DatabaseName"]!;

    if (storageBackend == "mongodb")
    {
        services.AddTransient<IUserRepository>(_ =>
            new MongoUserRepository(mongoConn, mongoDb));
        services.AddTransient<IAnalyticsRepository>(_ =>
            new MongoAnalyticsRepository(mongoConn, mongoDb));
        services.AddTransient<IMapRepository>(p =>
            new MongoMapRepository(mongoConn, mongoDb,
                p.GetRequiredService<ICacheRepository>(),
                p.GetRequiredService<CacheSettings>()));
        services.AddTransient<ISavedReportRepository>(_ =>
            new MongoSavedReportRepository(mongoConn, mongoDb));
    }
    else
    {
        services.AddTransient<IUserRepository>(_ =>
            new PostgresUserRepository(connectionString, sqlFactory));
        services.AddTransient<IAnalyticsRepository>(_ =>
            new PostgresAnalyticsRepository(connectionString, sqlFactory));
        services.AddTransient<IMapRepository>(p =>
            new PostgresMapRepository(connectionString, sqlFactory,
                p.GetRequiredService<ICacheRepository>(),
                p.GetRequiredService<CacheSettings>()));
        services.AddTransient<ISavedReportRepository>(_ =>
            new PostgresSavedReportRepository(connectionString, sqlFactory));
    }

    services.AddTransient<IAuthService, AuthService>();
    services.AddTransient<IAnalyticsService, AnalyticsService>();
    services.AddTransient<IMapService, MapService>();
    services.AddTransient<IAdminService, AdminService>();
    services.AddTransient<ISavedReportService, SavedReportService>();

    services.AddSingleton<SessionContext>();
    services.AddTransient<GuestMenu>();
    services.AddTransient<AnalystMenu>();
    services.AddTransient<AdminMenu>();
    services.AddTransient<Application>();

    var provider = services.BuildServiceProvider();
    var app = provider.GetRequiredService<Application>();
    await app.RunAsync();

    Log.Information("Приложение ATAP ConsoleUI завершило работу");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Критическая ошибка при запуске приложения");
}
finally
{
    await Log.CloseAndFlushAsync();
}
