using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LoxNet;

public class LoxoneHttpClient : IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly HttpClient _http;
    private readonly bool _disposeHttpClient;
    private readonly bool _secure;

    public LoxoneHttpClient(HttpClient httpClient, string host, int port = 80, bool secure = false)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _host = host;
        _port = port;
        _secure = secure;
        _disposeHttpClient = false;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri($"{(_secure ? "https" : "http")}://{_host}:{_port}");
    }

    public LoxoneHttpClient(HttpClient httpClient)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        if (_http.BaseAddress is null)
            throw new ArgumentException("HttpClient must have BaseAddress set", nameof(httpClient));
        _host = _http.BaseAddress.Host;
        _port = _http.BaseAddress.Port;
        _secure = _http.BaseAddress.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        _disposeHttpClient = false;
    }

    public LoxoneHttpClient(string host, int port = 80, bool secure = false)
        : this(new HttpClient(), host, port, secure)
    {
        _disposeHttpClient = true;
    }

    private string BaseUrl => _http.BaseAddress?.ToString().TrimEnd('/') ?? $"{(_secure ? "https" : "http")}://{_host}:{_port}";

    internal async Task<JsonDocument> RequestJsonAsync(string path)
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

    internal static string HmacHex(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, HashAlgorithmName name)
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

    public ValueTask DisposeAsync()
    {
        if (_disposeHttpClient)
            _http.Dispose();
        return ValueTask.CompletedTask;
    }
}
