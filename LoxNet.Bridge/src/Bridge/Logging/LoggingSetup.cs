using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using LoxNet.Bridge.Config;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace LoxNet.Bridge.Logging;

public static class LoggingSetup
{
    /// <summary>
    /// Singleton in-memory sink shared across the application lifetime so the UI
    /// log page can subscribe to it without DI gymnastics.
    /// </summary>
    public static readonly InMemoryLogSink InMemorySink = new();

    public static void Configure(ILoggingBuilder builder, IConfiguration configuration, LoggingSection? loggingSection = null)
    {
        builder.ClearProviders();

        // Parse log level from config, defaulting to Information
        var loggingConfig = loggingSection ?? configuration.GetSection("logging").Get<LoggingSection>() ?? new LoggingSection();
        var minimumLevel = ParseLogLevel(loggingConfig.MinLevel);

        BuildLogger(minimumLevel);

        builder.AddSerilog(Log.Logger);
    }

    /// <summary>
    /// Reconfigures the global Serilog logger with a new minimum level at runtime,
    /// preserving all sinks (console, file, in-memory).
    /// </summary>
    public static void SetMinimumLevel(string levelString)
    {
        BuildLogger(ParseLogLevel(levelString));
    }

    private static void BuildLogger(LogEventLevel minimumLevel)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
            .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/loxnet-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 30
            )
            .WriteTo.Sink(InMemorySink)
            .CreateLogger();
    }

    public static LogEventLevel ParseLogLevel(string levelString)
    {
        return levelString?.ToLowerInvariant() switch
        {
            "verbose" or "trace" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" or "info" => LogEventLevel.Information,
            "warning" or "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "critical" or "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}
