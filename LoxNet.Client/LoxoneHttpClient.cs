using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LoxNet;

public class LoxoneHttpClient : ILoxoneHttpClient
{
    private readonly HttpClient _http;
    private readonly bool _disposeHttpClient;
    private readonly ILogger<LoxoneHttpClient> _logger;
    public LoxoneConnectionOptions Options { get; }
    public TokenInfo? LastToken { get; set; }
    public string? Username { get; set; }

    public LoxoneHttpClient(ILogger<LoxoneHttpClient> logger, HttpClient httpClient, LoxoneConnectionOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _disposeHttpClient = false;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri($"{(Options.Secure ? "https" : "http")}://{Options.Host}:{Options.Port}");
    }

    public LoxoneHttpClient(ILogger<LoxoneHttpClient> logger, HttpClient httpClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        if (_http.BaseAddress is null)
            throw new ArgumentException("HttpClient must have BaseAddress set", nameof(httpClient));
        Options = new LoxoneConnectionOptions(
            _http.BaseAddress.Host,
            _http.BaseAddress.Port,
            _http.BaseAddress.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase));
        _disposeHttpClient = false;
    }

    public LoxoneHttpClient(ILogger<LoxoneHttpClient> logger, LoxoneConnectionOptions options)
        : this(logger, new HttpClient(), options)
    {
        _disposeHttpClient = true;
    }

    private string BaseUrl => _http.BaseAddress?.ToString().TrimEnd('/') ?? $"{(Options.Secure ? "https" : "http")}://{Options.Host}:{Options.Port}";

    /// <summary>
    /// Builds an authenticated path by appending token authentication query parameters.
    /// Sends the JWT token in plaintext as supported since Miniserver firmware 11.2.
    /// Returns the original path if no token or username is available.
    /// </summary>
    private string BuildAuthenticatedPath(string path)
    {
        if (LastToken is null || string.IsNullOrEmpty(Username))
            return path;

        // Since Miniserver 11.2, JWT tokens can be sent in plaintext (no HMAC step needed).
        // This avoids the SHA1 vs SHA256 ambiguity and removes the extra getkey roundtrip.
        var separator = path.Contains('?') ? '&' : '?';
        return $"{path}{separator}autht={Uri.EscapeDataString(LastToken.Token)}&user={Uri.EscapeDataString(Username)}";
    }

    /// <summary>
    /// Internal method for unauthenticated JSON requests (e.g., getkey).
    /// </summary>
    private async Task<JsonDocument> RequestJsonInternalAsync(string path, CancellationToken cancellationToken = default)
    {
        using var resp = await _http.GetAsync($"{BaseUrl}/{path}", cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            string content = string.Empty;
            try
            {
#if NET48
                content = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
                content = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#endif
            }
            catch { }
            _logger.LogError("HTTP {StatusCode} for path '{Path}': {Content}", resp.StatusCode, path, content);
            resp.EnsureSuccessStatusCode();
        }
#if NET48
        var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
#else
        var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#endif
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<JsonDocument> RequestJsonAsync(string path, CancellationToken cancellationToken = default)
    {
        // Build authenticated path if token is available
        var authenticatedPath = BuildAuthenticatedPath(path);
        using var resp = await _http.GetAsync($"{BaseUrl}/{authenticatedPath}", cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            string content = string.Empty;
            try
            {
#if NET48
                content = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
                content = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#endif
            }
            catch { }
            _logger.LogError("HTTP {StatusCode} for path '{Path}': {Content}", resp.StatusCode, path, content);
            resp.EnsureSuccessStatusCode();
        }
#if NET48
        var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
#else
        var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#endif
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches raw text response (e.g., certificate data) without JSON parsing.
    /// </summary>
    public async Task<string> RequestTextAsync(string path, CancellationToken cancellationToken = default)
    {
        // Build authenticated path if token is available
        var authenticatedPath = BuildAuthenticatedPath(path);
        using var resp = await _http.GetAsync($"{BaseUrl}/{authenticatedPath}", cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
#if NET48
        return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
        return await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#endif
    }

    public async Task<KeyInfo> GetKey2Async(string user, CancellationToken cancellationToken = default)
    {
        using var doc = await RequestJsonInternalAsync($"jdev/sys/getkey2/{Uri.EscapeDataString(user)}", cancellationToken).ConfigureAwait(false);
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

    internal static string HashToUpperInternal(ReadOnlySpan<byte> data, HashAlgorithm algo) => HashToUpper(data, algo);

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
        var path = $"jdev/sys/getjwt/{userHash}/{Uri.EscapeDataString(user)}/{permission}/{uid}/{encInfo}";
        var url = $"{BaseUrl}/{path}";
        _logger.LogDebug("Requesting JWT URL: {Url}", url);
        using var doc = await RequestJsonInternalAsync(path, cancellationToken).ConfigureAwait(false);
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

        try
        {
            var claims = DecodeJwtPayload(token.Token);
            _logger.LogDebug("Decoded JWT payload: {Claims}", claims);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decode JWT payload");
        }
        return token;
    }

    private static string DecodeJwtPayload(string jwt)
    {
        if (string.IsNullOrEmpty(jwt))
            return string.Empty;
        var parts = jwt.Split('.');
        if (parts.Length < 2)
            return string.Empty;
        var payload = parts[1];
        // base64url -> base64
        payload = payload.Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
            case 1: payload += "==="; break;
        }
        var bytes = Convert.FromBase64String(payload);
        return Encoding.UTF8.GetString(bytes);
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

        using var keyDoc = await RequestJsonInternalAsync("jdev/sys/getkey", cancellationToken).ConfigureAwait(false);
        var keyMsg = LoxoneMessageParser.Parse(keyDoc);
        keyMsg.EnsureSuccess();
        var key = HexUtils.FromHexString(keyMsg.Value.GetString()!);

        var tokenHash = HmacHex(key, Encoding.UTF8.GetBytes(current.Token), HashAlgorithmName.SHA1);
        // Use encrypted command for refreshjwt (similar to getjwt)
        // Miniserver versions 11.2+ also support plaintext token instead of hash
        var msg = await wsClient.SendEncryptedCommandAsync($"jdev/sys/refreshjwt/{tokenHash}/{user}", cancellationToken).ConfigureAwait(false);
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
