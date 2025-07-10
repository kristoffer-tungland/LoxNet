using System;
using System.Threading;
using System.Threading.Tasks;

namespace LoxNet;

public class LoxoneClient : ILoxoneClient
{
    public ILoxoneHttpClient Http { get; }
    public ILoxoneWebSocketClient WebSocket { get; }
    public string? Username { get; private set; }

    public LoxoneClient(LoxoneConnectionOptions options)
    {
        Http = new LoxoneHttpClient(options);
        WebSocket = new LoxoneWebSocketClient(Http);
    }

    public LoxoneClient(string host, int port = 80, bool secure = false)
        : this(new LoxoneConnectionOptions(host, port, secure))
    {
    }

    public LoxoneClient(ILoxoneHttpClient httpClient)
    {
        Http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        WebSocket = new LoxoneWebSocketClient(Http);
    }

    /// <summary>
    /// Retrieves a JWT via HTTP and authenticates the websocket connection.
    /// </summary>
    public async Task LoginAsync(string user, string password, int permission = 4, string info = "LoxNet", CancellationToken cancellationToken = default)
    {
        _ = await Http.GetJwtAsync(user, password, permission, info, cancellationToken).ConfigureAwait(false);
        await WebSocket.ConnectAndAuthenticateAsync(user, cancellationToken).ConfigureAwait(false);
        Username = user;
    }

    public async ValueTask DisposeAsync()
    {
        await Http.DisposeAsync().ConfigureAwait(false);
        await WebSocket.DisposeAsync().ConfigureAwait(false);
    }
}
