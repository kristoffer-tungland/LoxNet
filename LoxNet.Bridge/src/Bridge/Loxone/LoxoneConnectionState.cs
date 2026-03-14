namespace LoxNet.Bridge.Loxone;

/// <summary>
/// Represents the current state of the Loxone Miniserver WebSocket connection.
/// </summary>
public enum LoxoneConnectionState
{
    /// <summary>Not yet connected or intentionally stopped.</summary>
    Disconnected,

    /// <summary>Performing the initial login and setup.</summary>
    Connecting,

    /// <summary>Authenticated and receiving state updates.</summary>
    Connected,

    /// <summary>Connection was lost; a reconnect attempt is in progress.</summary>
    Reconnecting
}
