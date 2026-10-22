using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace ATAP.ConsoleUI;

sealed class DynamicSerilogProvider : ILoggerProvider
{
    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
        => new DynamicSerilogLogger(categoryName);

    public void Dispose() { }
}

sealed class DynamicSerilogLogger : Microsoft.Extensions.Logging.ILogger
{
    readonly string _category;

    public DynamicSerilogLogger(string category) => _category = category;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.None) return;

        var level = logLevel switch
        {
            LogLevel.Trace       => LogEventLevel.Verbose,
            LogLevel.Debug       => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning     => LogEventLevel.Warning,
            LogLevel.Error       => LogEventLevel.Error,
            LogLevel.Critical    => LogEventLevel.Fatal,
            _                    => LogEventLevel.Information
        };

        var serilog = Serilog.Log.Logger;
        if (!serilog.IsEnabled(level)) return;

        serilog.ForContext("SourceContext", _category)
               .Write(level, exception, "{Message}", formatter(state, exception));
    }
}
