using System;
using System.Threading.Tasks;

namespace LoxNet;

public class LoxoneClient : IAsyncDisposable
{
    public LoxoneHttpClient Http { get; }
    public LoxoneWebSocketClient WebSocket { get; }

    public LoxoneClient(LoxoneConnectionOptions options)
    {
        Http = new LoxoneHttpClient(options);
        WebSocket = new LoxoneWebSocketClient(Http);
    }

    public LoxoneClient(string host, int port = 80, bool secure = false)
        : this(new LoxoneConnectionOptions(host, port, secure))
    {
    }

    public LoxoneClient(LoxoneHttpClient httpClient)
    {
        Http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        WebSocket = new LoxoneWebSocketClient(Http);
    }

    public async ValueTask DisposeAsync()
    {
        await Http.DisposeAsync();
        await WebSocket.DisposeAsync();
    }
}
