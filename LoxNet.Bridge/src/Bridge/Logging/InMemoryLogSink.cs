using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace LoxNet.Bridge.Logging;

/// <summary>
/// Serilog sink that keeps the last <see cref="Capacity"/> log entries in memory
/// and raises <see cref="LogReceived"/> so Blazor pages can push updates
/// without polling.
/// </summary>
public sealed class InMemoryLogSink : ILogEventSink
{
    private const int Capacity = 500;
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    /// <summary>Raised on the thread-pool whenever a new entry is added.</summary>
    public event Action<LogEntry>? LogReceived;

    /// <summary>Snapshot of all buffered entries (oldest first).</summary>
    public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

    void ILogEventSink.Emit(LogEvent logEvent)
    {
        var entry = new LogEntry
        {
            Timestamp = logEvent.Timestamp,
            Level = logEvent.Level.ToString(),
            Message = logEvent.RenderMessage(),
            Exception = logEvent.Exception?.ToString()
        };

        _entries.Enqueue(entry);

        // Trim to capacity
        while (_entries.Count > Capacity)
        {
            _entries.TryDequeue(out _);
        }

        // Fire-and-forget on thread pool so the log call never blocks
        var handler = LogReceived;
        if (handler is not null)
        {
            ThreadPool.QueueUserWorkItem(_ => handler(entry));
        }
    }
}
