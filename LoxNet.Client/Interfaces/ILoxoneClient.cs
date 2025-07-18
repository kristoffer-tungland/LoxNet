using System.Threading;

namespace LoxNet;

public interface ILoxoneClient : IAsyncDisposable
{
    ILoxoneHttpClient Http { get; }
    ILoxoneWebSocketClient WebSocket { get; }

    /// <summary>
    /// The username used for the current login session.
    /// </summary>
    string? Username { get; }

    /// <summary>
    /// Ensures the currently stored JWT token is valid, refreshing it when needed.
    /// </summary>
    Task EnsureValidTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a JWT token and authenticates the websocket connection.
    /// </summary>
    /// <param name="user">Username to authenticate.</param>
    /// <param name="password">Password for the user.</param>
    /// <param name="permission">The permission level of the token.</param>
    /// <param name="info">Additional client info.</param>
    Task LoginAsync(string user, string password, int permission = 4, string info = "LoxNet", CancellationToken cancellationToken = default);
}
