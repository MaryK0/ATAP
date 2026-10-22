using Serilog;
using Serilog.Events;

namespace ATAP.ConsoleUI;

public static class LogConfigurator
{
    public static string CurrentLogPath { get; private set; } = "";

    public static void Initialize(string logPath)
    {
        CurrentLogPath = Resolve(logPath);
        Directory.CreateDirectory(Path.GetDirectoryName(CurrentLogPath)!);
        Apply();
    }

    public static bool TryChange(string input, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(input))           { error = "Путь не может быть пустым"; return false; }
        if (input.Any(c => Path.GetInvalidPathChars().Contains(c))) { error = "Путь содержит недопустимые символы"; return false; }

        try
        {
            var resolved = Resolve(input);
            Directory.CreateDirectory(Path.GetDirectoryName(resolved)!);
            CurrentLogPath = resolved;
            Apply();
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }

    private static string Resolve(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);

    private static void Apply()
    {
        var newLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: CurrentLogPath,
                rollingInterval: RollingInterval.Day,
                restrictedToMinimumLevel: LogEventLevel.Debug,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var oldLogger = Log.Logger;
        Log.Logger = newLogger;
        Log.Information("Лог-файл изменён: {Path}", CurrentLogPath);
        (oldLogger as IDisposable)?.Dispose();
    }
}
