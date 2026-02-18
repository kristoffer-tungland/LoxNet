using Microsoft.Extensions.Logging;
using System;

namespace LoxNet;

/// <summary>
/// Provides extension methods for working with loggers.
/// </summary>
internal static class LoggingExtensions
{
    /// <summary>
    /// Creates a new null logger for a different type.
    /// Used internally when loggers need to be created without access to the full factory.
    /// In production, loggers should be provided via dependency injection.
    /// </summary>
    internal static ILogger<T> CreateChildLogger<T>()
    {
        return new NullLoggerProxy<T>();
    }

    /// <summary>
    /// Simple null logger implementation that does nothing.
    /// </summary>
    private sealed class NullLoggerProxy<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) { }
    }
}
