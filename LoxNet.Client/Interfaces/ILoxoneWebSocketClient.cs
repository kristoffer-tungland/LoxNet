using System;
using System.Threading;
using System.Threading.Tasks;

namespace LoxNet;

public interface ILoxoneWebSocketClient : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
    Task<LoxoneMessage> AuthenticateWithTokenAsync(string token, string user, CancellationToken cancellationToken = default);
    Task<LoxoneMessage> ConnectAndAuthenticateAsync(string user, CancellationToken cancellationToken = default);
    Task KeepAliveAsync(CancellationToken cancellationToken = default);
    Task<LoxoneMessage> CommandAsync(string path, CancellationToken cancellationToken = default);
    Task<LoxoneMessage> SendEncryptedCommandAsync(string command, CancellationToken cancellationToken = default);
    Task PrepareEncryptionAsync(CancellationToken cancellationToken = default);
    Task<bool> InitializeEncryptionAsync(CancellationToken cancellationToken = default);
    Task<TokenInfo> AcquireJwtTokenAsync(string user, string password, int permission, string info, CancellationToken cancellationToken = default);

    /// <summary>
    /// Raised when a raw message is received from the websocket.
    /// </summary>
    event EventHandler<string>? MessageReceived;

    /// <summary>
    /// Raised when the WebSocket connection is lost unexpectedly (not on intentional close).
    /// </summary>
    event EventHandler? Disconnected;

    /// <summary>
    /// Starts listening for incoming messages.
    /// </summary>
    Task ListenAsync(CancellationToken cancellationToken = default);
}
