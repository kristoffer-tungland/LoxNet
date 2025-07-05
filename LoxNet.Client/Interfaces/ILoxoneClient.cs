namespace LoxNet;

public interface ILoxoneClient : IAsyncDisposable
{
    ILoxoneHttpClient Http { get; }
    ILoxoneWebSocketClient WebSocket { get; }
}
