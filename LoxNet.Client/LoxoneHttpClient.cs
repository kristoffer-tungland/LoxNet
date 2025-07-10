using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LoxNet;

public class LoxoneHttpClient : ILoxoneHttpClient
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

    public async Task<JsonDocument> RequestJsonAsync(string path, CancellationToken cancellationToken = default)
    {
        using var resp = await _http.GetAsync($"{BaseUrl}/{path}", cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<KeyInfo> GetKey2Async(string user, CancellationToken cancellationToken = default)
    {
        using var doc = await RequestJsonAsync($"jdev/sys/getkey2/{Uri.EscapeDataString(user)}", cancellationToken).ConfigureAwait(false);
        var msg = LoxoneMessageParser.Parse(doc);
        msg.EnsureSuccess();
        var value = msg.Value;
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

    public async Task<TokenInfo> GetJwtAsync(string user, string password, int permission, string info, CancellationToken cancellationToken = default)
    {
        var keyInfo = await GetKey2Async(user, cancellationToken).ConfigureAwait(false);
        var keyBytes = HexUtils.FromHexString(keyInfo.Key);
        var algoName = keyInfo.HashAlg.Equals("sha256", StringComparison.OrdinalIgnoreCase) ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA1;
        using HashAlgorithm algo = algoName == HashAlgorithmName.SHA256 ? SHA256.Create() : SHA1.Create();
        var pwHash = HashToUpper(Encoding.UTF8.GetBytes($"{password}:{keyInfo.Salt}"), algo);
        var userHash = HmacHex(keyBytes, Encoding.UTF8.GetBytes($"{user}:{pwHash}"), algoName);
        var uid = Guid.NewGuid().ToString("N");
        var encInfo = Uri.EscapeDataString(info);
        using var doc = await RequestJsonAsync($"jdev/sys/getjwt/{userHash}/{user}/{permission}/{uid}/{encInfo}", cancellationToken).ConfigureAwait(false);
        var msg = LoxoneMessageParser.Parse(doc);
        msg.EnsureSuccess();
        var val = msg.Value;
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

    /// <summary>
    /// Refreshes the currently stored JWT token using the websocket API.
    /// </summary>
    /// <param name="wsClient">The websocket client to use.</param>
    /// <param name="user">The user associated with the token.</param>
    /// <returns>The refreshed <see cref="TokenInfo"/>.</returns>
    public async Task<TokenInfo> RefreshJwtAsync(ILoxoneWebSocketClient wsClient, string user, CancellationToken cancellationToken = default)
    {
        if (wsClient is null) throw new ArgumentNullException(nameof(wsClient));

        var current = LastToken ?? throw new InvalidOperationException("No JWT token available");

        using var keyDoc = await RequestJsonAsync("jdev/sys/getkey", cancellationToken).ConfigureAwait(false);
        var keyMsg = LoxoneMessageParser.Parse(keyDoc);
        keyMsg.EnsureSuccess();
        var key = HexUtils.FromHexString(keyMsg.Value.GetString()!);

        var tokenHash = HmacHex(key, Encoding.UTF8.GetBytes(current.Token), HashAlgorithmName.SHA1);
        var msg = await wsClient.CommandAsync($"refreshjwt/{tokenHash}/{user}", cancellationToken).ConfigureAwait(false);
        msg.EnsureSuccess();
        var val = msg.Value;

        var token = val.GetProperty("token").GetString()!;
        var validUntil = val.GetProperty("validUntil").GetInt64();
        var unsecure = val.GetProperty("unsecurePass").GetBoolean();
        var rights = current.TokenRights;
        if (val.TryGetProperty("tokenRights", out JsonElement r))
            rights = r.GetInt32();
        var keyStr = current.Key;
        if (val.TryGetProperty("key", out JsonElement k))
            keyStr = k.GetString()!;

        var info = new TokenInfo(token, validUntil, rights, unsecure, keyStr);
        LastToken = info;

        return info;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposeHttpClient)
            _http.Dispose();
#if NET48
        return default;
#else
        return ValueTask.CompletedTask;
#endif
    }
}
