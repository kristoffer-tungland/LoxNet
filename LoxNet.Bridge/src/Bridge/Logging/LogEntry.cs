namespace LoxNet.Bridge.Logging;

/// <summary>A single structured log entry captured in-memory for the UI log page.</summary>
public sealed record LogEntry
{
    public required DateTimeOffset Timestamp { get; init; }

    public required string Level { get; init; }

    public required string Message { get; init; }

    public string? Exception { get; init; }
}
