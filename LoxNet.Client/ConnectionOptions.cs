namespace LoxNet;

/// <summary>
/// Configuration options for connecting to a Loxone Miniserver.
/// </summary>
/// <param name="Host">The hostname or IP address of the Miniserver.</param>
/// <param name="Port">The port number (default: 80).</param>
/// <param name="Secure">Whether to use HTTPS/WSS (default: false).</param>
/// <param name="StructureCachePath">Optional path for caching structure files (default: system temp directory).</param>
public record LoxoneConnectionOptions(
    string Host,
    int Port = 80,
    bool Secure = false,
    string? StructureCachePath = null);
