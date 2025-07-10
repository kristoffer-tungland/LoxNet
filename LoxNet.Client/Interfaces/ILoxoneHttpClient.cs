using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LoxNet;

public interface ILoxoneHttpClient : IAsyncDisposable
{
    LoxoneConnectionOptions Options { get; }
    TokenInfo? LastToken { get; }
    Task<JsonDocument> RequestJsonAsync(string path, CancellationToken cancellationToken = default);
    Task<KeyInfo> GetKey2Async(string user, CancellationToken cancellationToken = default);
    Task<TokenInfo> GetJwtAsync(string user, string password, int permission, string info, CancellationToken cancellationToken = default);
}
