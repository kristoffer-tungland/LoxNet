using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using LoxNet.Bridge.Config;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace LoxNet.Bridge.Logging;

public static class LoggingSetup
{
    public static void Configure(ILoggingBuilder builder, IConfiguration configuration)
    {
        builder.ClearProviders();
        
        // Parse log level from config, defaulting to Information
        var loggingConfig = configuration.GetSection("logging").Get<LoggingSection>() ?? new LoggingSection();
        var minimumLevel = ParseLogLevel(loggingConfig.MinLevel);
        
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/loxnet-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 30
            )
            .CreateLogger();
        
        builder.AddSerilog(Log.Logger);
    }
    
    private static LogEventLevel ParseLogLevel(string levelString)
    {
        return levelString?.ToLowerInvariant() switch
        {
            "trace" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" or "info" => LogEventLevel.Information,
            "warning" or "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "critical" or "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}
