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
}
