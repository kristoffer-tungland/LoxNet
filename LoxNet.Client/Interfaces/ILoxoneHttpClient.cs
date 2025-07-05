using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace LoxNet;

public interface ILoxoneHttpClient : IAsyncDisposable
{
    LoxoneConnectionOptions Options { get; }
    TokenInfo? LastToken { get; }
    Task<JsonDocument> RequestJsonAsync(string path);
    Task<KeyInfo> GetKey2Async(string user);
    Task<TokenInfo> GetJwtAsync(string user, string password, int permission, string info);
}
