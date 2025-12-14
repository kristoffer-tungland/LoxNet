using Microsoft.Extensions.Logging;

namespace LoxNet.Bridge.Logging;

public static class LoggingSetup
{
    public static void Configure(ILoggingBuilder builder)
    {
        builder.ClearProviders();
        builder.AddSimpleConsole(options =>
        {
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            options.SingleLine = true;
        });
        builder.SetMinimumLevel(LogLevel.Information);
    }
}
