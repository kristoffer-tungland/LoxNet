namespace LoxNet;

public class LoxoneClient : IAsyncDisposable
{
    public LoxoneHttpClient Http { get; }
    public LoxoneWebSocketClient WebSocket { get; }

    public LoxoneClient(string host, int port = 80, bool secure = false)
    {
        Http = new LoxoneHttpClient(host, port, secure);
        WebSocket = new LoxoneWebSocketClient(host, port, secure, Http);
    }

    public async ValueTask DisposeAsync()
    {
        await Http.DisposeAsync();
        await WebSocket.DisposeAsync();
    }
}
