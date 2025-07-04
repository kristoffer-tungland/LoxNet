using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LoxNet;

public record LoxoneMessage(int Code, JsonElement? Value, string? Message);

public record KeyInfo(string Key, string Salt, string HashAlg);

public record TokenInfo(string Token, long ValidUntil, int TokenRights, bool UnsecurePass, string Key);

public class LoxoneClient : IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly HttpClient _http;
    private readonly bool _disposeHttpClient;
    private readonly bool _secure;
    private ClientWebSocket? _ws;

    public LoxoneClient(HttpClient httpClient, string host, int port = 80, bool secure = false)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _host = host;
        _port = port;
        _secure = secure;
        _disposeHttpClient = false;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri($"{(_secure ? "https" : "http")}://{_host}:{_port}");
    }

    public LoxoneClient(HttpClient httpClient)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        if (_http.BaseAddress is null)
            throw new ArgumentException("HttpClient must have BaseAddress set", nameof(httpClient));
        _host = _http.BaseAddress.Host;
        _port = _http.BaseAddress.Port;
        _secure = _http.BaseAddress.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        _disposeHttpClient = false;
    }

    public LoxoneClient(string host, int port = 80, bool secure = false)
        : this(new HttpClient(), host, port, secure)
    {
        _disposeHttpClient = true;
    }

    private string BaseUrl => _http.BaseAddress?.ToString().TrimEnd('/') ?? $"{(_secure ? "https" : "http")}://{_host}:{_port}";

    private async Task<JsonDocument> RequestJsonAsync(string path)
    {
        using var resp = await _http.GetAsync($"{BaseUrl}/{path}");
        resp.EnsureSuccessStatusCode();
        var stream = await resp.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    public async Task<KeyInfo> GetKey2Async(string user)
    {
        using var doc = await RequestJsonAsync($"jdev/sys/getkey2/{Uri.EscapeDataString(user)}");
        var value = doc.RootElement.GetProperty("LL").GetProperty("value");
        return new KeyInfo(
            value.GetProperty("key").GetString()!,
            value.GetProperty("salt").GetString()!,
            value.GetProperty("hashAlg").GetString()!
        );
    }

    private static string HashToUpper(ReadOnlySpan<byte> data, HashAlgorithm algo)
    {
        var hash = algo.ComputeHash(data.ToArray());
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("X2"));
        return sb.ToString();
    }

    private static string HmacHex(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, HashAlgorithmName name)
    {
        using HMAC hmac = name.Name == "SHA256"
            ? new HMACSHA256(key.ToArray())
            : new HMACSHA1(key.ToArray());
        var hash = hmac.ComputeHash(data.ToArray());
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    public async Task<TokenInfo> GetJwtAsync(string user, string password, int permission, string info)
    {
        var keyInfo = await GetKey2Async(user);
        var keyBytes = Convert.FromHexString(keyInfo.Key);
        var algoName = keyInfo.HashAlg.Equals("sha256", StringComparison.OrdinalIgnoreCase) ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA1;
        using HashAlgorithm algo = algoName == HashAlgorithmName.SHA256 ? SHA256.Create() : SHA1.Create();
        var pwHash = HashToUpper(Encoding.UTF8.GetBytes($"{password}:{keyInfo.Salt}"), algo);
        var userHash = HmacHex(keyBytes, Encoding.UTF8.GetBytes($"{user}:{pwHash}"), algoName);
        var uid = Guid.NewGuid().ToString("N");
        var encInfo = Uri.EscapeDataString(info);
        using var doc = await RequestJsonAsync($"jdev/sys/getjwt/{userHash}/{user}/{permission}/{uid}/{encInfo}");
        var val = doc.RootElement.GetProperty("LL").GetProperty("value");
        return new TokenInfo(
            val.GetProperty("token").GetString()!,
            val.GetProperty("validUntil").GetInt64(),
            val.GetProperty("tokenRights").GetInt32(),
            val.GetProperty("unsecurePass").GetBoolean(),
            val.GetProperty("key").GetString()!
        );
    }

    public async Task ConnectAsync()
    {
        _ws = new ClientWebSocket();
        _ws.Options.AddSubProtocol("remotecontrol");
        string scheme = _secure ? "wss" : "ws";
        await _ws.ConnectAsync(new Uri($"{scheme}://{_host}:{_port}/ws/rfc6455"), CancellationToken.None);
    }

    public async Task CloseAsync()
    {
        if (_ws is not null)
        {
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            _ws.Dispose();
            _ws = null;
        }
    }

    private async Task<string> ReceiveStringAsync()
    {
        if (_ws is null) throw new InvalidOperationException("WebSocket not connected");
        var buffer = new ArraySegment<byte>(new byte[8192]);
        using var ms = new System.IO.MemoryStream();
        while (true)
        {
            var result = await _ws.ReceiveAsync(buffer, CancellationToken.None);
            ms.Write(buffer.Array!, buffer.Offset, result.Count);
            if (result.EndOfMessage)
                break;
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private async Task SendStringAsync(string text)
    {
        if (_ws is null) throw new InvalidOperationException("WebSocket not connected");
        var data = Encoding.UTF8.GetBytes(text);
        await _ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task<LoxoneMessage> SendCommandAsync(string command)
    {
        await SendStringAsync(command);
        var response = await ReceiveStringAsync();
        using var doc = JsonDocument.Parse(response);
        var ll = doc.RootElement.GetProperty("LL");
        JsonElement value = ll.TryGetProperty("value", out var v) ? v : default;
        string? message = ll.TryGetProperty("value", out var msg) ? msg.GetString() : null;
        return new LoxoneMessage(ll.GetProperty("Code").GetInt32(), value, message);
    }

    public async Task<LoxoneMessage> AuthenticateWithTokenAsync(string token, string user)
    {
        using var doc = await RequestJsonAsync("jdev/sys/getkey");
        var key = Convert.FromHexString(doc.RootElement.GetProperty("LL").GetProperty("value").GetString()!);
        var digest = HmacHex(key, Encoding.UTF8.GetBytes(token), HashAlgorithmName.SHA1);
        return await SendCommandAsync($"authwithtoken/{digest}/{user}");
    }

    public async Task KeepAliveAsync() => _ = await SendCommandAsync("keepalive");

    public async Task<LoxoneMessage> CommandAsync(string path) => await SendCommandAsync(path);

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        if (_disposeHttpClient)
            _http.Dispose();
    }
}
