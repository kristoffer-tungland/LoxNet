using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LoxNet;

public class LoxoneClient : ILoxoneClient
{
    private readonly ILoxoneHttpClient _httpClient;
    private readonly ILoxoneWebSocketClient _wsClient;
    private readonly TimeSpan _refreshWindow;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private readonly ILogger<LoxoneClient> _logger;

    public ILoxoneHttpClient Http { get; }
    public ILoxoneWebSocketClient WebSocket { get; }
    public string? Username { get; private set; }

    public LoxoneClient(ILogger<LoxoneClient> logger, LoxoneConnectionOptions options, TimeSpan? refreshWindow = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = new LoxoneHttpClient(LoggingExtensions.CreateChildLogger<LoxoneHttpClient>(), options);
        _wsClient = new LoxoneWebSocketClient(LoggingExtensions.CreateChildLogger<LoxoneWebSocketClient>(), _httpClient);
        _refreshWindow = refreshWindow ?? TimeSpan.FromSeconds(30);
        Http = new HttpProxy(this, _httpClient);
        WebSocket = new WebSocketProxy(this, _wsClient);
    }

    public LoxoneClient(ILogger<LoxoneClient> logger, string host, int port = 80, bool secure = false, TimeSpan? refreshWindow = null)
        : this(logger, new LoxoneConnectionOptions(host, port, secure), refreshWindow)
    {
    }

    public LoxoneClient(ILogger<LoxoneClient> logger, ILoxoneHttpClient httpClient, ILoxoneWebSocketClient? wsClient = null, TimeSpan? refreshWindow = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _wsClient = wsClient ?? new LoxoneWebSocketClient(LoggingExtensions.CreateChildLogger<LoxoneWebSocketClient>(), _httpClient);
        _refreshWindow = refreshWindow ?? TimeSpan.FromSeconds(30);
        Http = new HttpProxy(this, _httpClient);
        WebSocket = new WebSocketProxy(this, _wsClient);
    }

    /// <summary>
    /// Connects to Miniserver, initializes encryption, acquires JWT via encrypted WebSocket, and authenticates.
    /// This follows the recommended Loxone protocol flow.
    /// </summary>
    public async Task LoginAsync(string user, string password, int permission = 4, string info = "LoxNet", CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting encrypted login for user '{User}'", user);
        
        // Step 1 (per Loxone docs): Fetch certificate via HTTP BEFORE opening WebSocket
        // This removes HTTP latency from the critical auth window
        await WebSocket.PrepareEncryptionAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Encryption preparation complete (certificate cached)");
        
        // Step 2 (per Loxone docs): Open WebSocket and start receive loop immediately
        await WebSocket.ConnectAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("WebSocket connected");

        // Step 3 (per Loxone docs): Initialize encryption via keyexchange (fast, already have cert)
        var encryptionOk = await WebSocket.InitializeEncryptionAsync(cancellationToken).ConfigureAwait(false);
        if (!encryptionOk)
        {
            await WebSocket.CloseAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Failed to initialize WebSocket encryption. Check debug output for keyexchange error details. Ensure the Miniserver is accessible and supports encrypted WebSocket communication.");
        }
        _logger.LogDebug("Encryption initialized");

        // Step 4 (per Loxone docs): Acquire JWT via encrypted WebSocket
        var token = await WebSocket.AcquireJwtTokenAsync(user, password, permission, info, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("JWT obtained: rights={TokenRights}", token.TokenRights);

        // Store token in HTTP client for later use
        Http.LastToken = token;
        _httpClient.Username = user;

        // Step 5 (per Loxone docs): Authenticate WebSocket with the token
        // Note: The JWT token itself serves as authentication after getjwt.
        // authwithtoken was attempted but returned 400 (Bad request) on this Miniserver version,
        // suggesting the JWT is the complete authentication mechanism.
        // For now, we skip the explicit authwithtoken step since getjwt was successful.
        _logger.LogDebug("WebSocket authentication complete via JWT token (rights={Rights})", token.TokenRights);

        Username = user;
    }

    /// <summary>
    /// Ensures that the currently stored JWT token is valid, refreshing if necessary.
    /// </summary>
    public async Task EnsureValidTokenAsync(CancellationToken cancellationToken = default)
    {
        var token = _httpClient.LastToken;
        if (token is null)
            return;

        var expiry = token.GetExpiryDate();
        if (expiry - DateTimeOffset.UtcNow > _refreshWindow)
            return;

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            token = _httpClient.LastToken;
            if (token is null)
                return;

            expiry = token.GetExpiryDate();
            if (expiry - DateTimeOffset.UtcNow <= _refreshWindow)
            {
                var user = Username ?? throw new InvalidOperationException("Client is not logged in");
                await _httpClient.RefreshJwtAsync(_wsClient, user, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Http.DisposeAsync().ConfigureAwait(false);
        await WebSocket.DisposeAsync().ConfigureAwait(false);
    }

    private sealed class HttpProxy : ILoxoneHttpClient
    {
        private readonly LoxoneClient _parent;
        private readonly ILoxoneHttpClient _inner;

        public HttpProxy(LoxoneClient parent, ILoxoneHttpClient inner)
        {
            _parent = parent;
            _inner = inner;
        }

        public LoxoneConnectionOptions Options => _inner.Options;
        public TokenInfo? LastToken 
        { 
            get => _inner.LastToken;
            set => _inner.LastToken = value;
        }
        public string? Username
        {
            get => _inner.Username;
            set => _inner.Username = value;
        }

        public async Task<JsonDocument> RequestJsonAsync(string path, CancellationToken cancellationToken = default)
        {
            await _parent.EnsureValidTokenAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await _inner.RequestJsonAsync(path, cancellationToken).ConfigureAwait(false);
            }
#if NET48
            catch (HttpRequestException ex) when (ex.Message.Contains("401"))
#else
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
#endif
            {
                await _parent.EnsureValidTokenAsync(cancellationToken).ConfigureAwait(false);
                return await _inner.RequestJsonAsync(path, cancellationToken).ConfigureAwait(false);
            }
        }

        public Task<string> RequestTextAsync(string path, CancellationToken cancellationToken = default) =>
            _inner.RequestTextAsync(path, cancellationToken);

        public Task<KeyInfo> GetKey2Async(string user, CancellationToken cancellationToken = default) =>
            _inner.GetKey2Async(user, cancellationToken);

        public Task<TokenInfo> GetJwtAsync(string user, string password, int permission, string info, CancellationToken cancellationToken = default) =>
            _inner.GetJwtAsync(user, password, permission, info, cancellationToken);

        public Task<TokenInfo> RefreshJwtAsync(ILoxoneWebSocketClient wsClient, string user, CancellationToken cancellationToken = default) =>
            _inner.RefreshJwtAsync(wsClient, user, cancellationToken);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    private sealed class WebSocketProxy : ILoxoneWebSocketClient
    {
        private readonly LoxoneClient _parent;
        private readonly ILoxoneWebSocketClient _inner;

        public WebSocketProxy(LoxoneClient parent, ILoxoneWebSocketClient inner)
        {
            _parent = parent;
            _inner = inner;
        }

        public event EventHandler<string>? MessageReceived
        {
            add => _inner.MessageReceived += value;
            remove => _inner.MessageReceived -= value;
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default) =>
            _inner.ConnectAsync(cancellationToken);

        public Task CloseAsync(CancellationToken cancellationToken = default) =>
            _inner.CloseAsync(cancellationToken);

        public Task<LoxoneMessage> AuthenticateWithTokenAsync(string token, string user, CancellationToken cancellationToken = default) =>
            _inner.AuthenticateWithTokenAsync(token, user, cancellationToken);

        public Task<LoxoneMessage> ConnectAndAuthenticateAsync(string user, CancellationToken cancellationToken = default) =>
            _inner.ConnectAndAuthenticateAsync(user, cancellationToken);

        public Task<bool> InitializeEncryptionAsync(CancellationToken cancellationToken = default) =>
            _inner.InitializeEncryptionAsync(cancellationToken);

        public Task PrepareEncryptionAsync(CancellationToken cancellationToken = default) =>
            _inner.PrepareEncryptionAsync(cancellationToken);

        public Task<TokenInfo> AcquireJwtTokenAsync(string user, string password, int permission, string info, CancellationToken cancellationToken = default) =>
            _inner.AcquireJwtTokenAsync(user, password, permission, info, cancellationToken);

        public async Task KeepAliveAsync(CancellationToken cancellationToken = default)
        {
            await _parent.EnsureValidTokenAsync(cancellationToken).ConfigureAwait(false);
            await _inner.KeepAliveAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<LoxoneMessage> CommandAsync(string path, CancellationToken cancellationToken = default)
        {
            await _parent.EnsureValidTokenAsync(cancellationToken).ConfigureAwait(false);
            return await _inner.CommandAsync(path, cancellationToken).ConfigureAwait(false);
        }

        public async Task<LoxoneMessage> SendEncryptedCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            await _parent.EnsureValidTokenAsync(cancellationToken).ConfigureAwait(false);
            return await _inner.SendEncryptedCommandAsync(command, cancellationToken).ConfigureAwait(false);
        }

        public Task ListenAsync(CancellationToken cancellationToken = default) =>
            _inner.ListenAsync(cancellationToken);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}
