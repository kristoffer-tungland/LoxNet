using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LoxNet;

public class LoxoneHttpClient : IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly bool _disposeHttpClient;
    public LoxoneConnectionOptions Options { get; }
    public TokenInfo? LastToken { get; private set; }

    public LoxoneHttpClient(HttpClient httpClient, LoxoneConnectionOptions options)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _disposeHttpClient = false;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri($"{(Options.Secure ? "https" : "http")}://{Options.Host}:{Options.Port}");
    }

    public LoxoneHttpClient(HttpClient httpClient)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        if (_http.BaseAddress is null)
            throw new ArgumentException("HttpClient must have BaseAddress set", nameof(httpClient));
        Options = new LoxoneConnectionOptions(
            _http.BaseAddress.Host,
            _http.BaseAddress.Port,
            _http.BaseAddress.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase));
        _disposeHttpClient = false;
    }

    public LoxoneHttpClient(LoxoneConnectionOptions options)
        : this(new HttpClient(), options)
    {
        _disposeHttpClient = true;
    }

    private string BaseUrl => _http.BaseAddress?.ToString().TrimEnd('/') ?? $"{(Options.Secure ? "https" : "http")}://{Options.Host}:{Options.Port}";

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
        var token = new TokenInfo(
            val.GetProperty("token").GetString()!,
            val.GetProperty("validUntil").GetInt64(),
            val.GetProperty("tokenRights").GetInt32(),
            val.GetProperty("unsecurePass").GetBoolean(),
            val.GetProperty("key").GetString()!
        );
        LastToken = token;
        return token;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposeHttpClient)
            _http.Dispose();
        return ValueTask.CompletedTask;
    }
}
