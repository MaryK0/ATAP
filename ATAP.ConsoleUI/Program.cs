using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using ATAP.ApiClient;
using ATAP.BusinessLogic.Interfaces.Services;
using ATAP.BusinessLogic.Services;
using ATAP.ConsoleUI;
using ATAP.ConsoleUI.Infrastructure;
using ATAP.ConsoleUI.Menus;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var apiUrl = Environment.GetEnvironmentVariable("ATAP_API_URL") ?? config["Api:BaseUrl"]
    ?? "http://localhost:5080/api/v1/";

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

    services.AddSingleton(appSettings);
    services.AddSingleton(new HttpClient { BaseAddress = new Uri(apiUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromMinutes(3) });
    services.AddSingleton<AtapApiClient>();
    services.AddSingleton<IAuthService>(p => p.GetRequiredService<AtapApiClient>());
    services.AddSingleton<IMapService>(p => p.GetRequiredService<AtapApiClient>());
    services.AddSingleton<IAnalyticsService>(p => p.GetRequiredService<AtapApiClient>());
    services.AddSingleton<IAdminService>(p => p.GetRequiredService<AtapApiClient>());
    services.AddSingleton<ISavedReportService>(p => p.GetRequiredService<AtapApiClient>());
    services.AddSingleton<SessionContext>();
    services.AddTransient<GuestMenu>();
    services.AddTransient<AnalystMenu>();
    services.AddTransient<AdminMenu>();
    services.AddTransient<Application>();

    using var provider = services.BuildServiceProvider();
    if (args.Contains("--smoke"))
    {
        await ApiSmoke.RunAsync(provider.GetRequiredService<AtapApiClient>());
        return;
    }
    var app = provider.GetRequiredService<Application>();
    await app.RunAsync();

    Log.Information("Приложение ATAP ConsoleUI завершило работу");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Критическая ошибка при запуске приложения");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
