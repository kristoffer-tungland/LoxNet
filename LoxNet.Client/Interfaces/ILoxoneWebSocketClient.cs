using System;
using System.Threading.Tasks;

namespace LoxNet;

public interface ILoxoneWebSocketClient : IAsyncDisposable
{
    Task ConnectAsync();
    Task CloseAsync();
    Task<LoxoneMessage> AuthenticateWithTokenAsync(string token, string user);
    Task<LoxoneMessage> ConnectAndAuthenticateAsync(string user);
    Task KeepAliveAsync();
    Task<LoxoneMessage> CommandAsync(string path);

    /// <summary>
    /// Raised when a raw message is received from the websocket.
    /// </summary>
    event EventHandler<string>? MessageReceived;

    /// <summary>
    /// Starts listening for incoming messages.
    /// </summary>
    Task ListenAsync(System.Threading.CancellationToken cancellationToken = default);
}
