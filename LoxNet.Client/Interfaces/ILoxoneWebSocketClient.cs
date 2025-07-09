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

    /// <summary>
    /// Raised when a raw message is received from the websocket.
    /// </summary>
    event EventHandler<string>? MessageReceived;

    /// <summary>
    /// Starts listening for incoming messages.
    /// </summary>
    Task ListenAsync(CancellationToken cancellationToken = default);
}
