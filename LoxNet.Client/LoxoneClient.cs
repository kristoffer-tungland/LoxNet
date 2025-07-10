using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LoxNet;

public class LoxoneClient : ILoxoneClient
{
    private readonly ILoxoneHttpClient _httpClient;
    private readonly ILoxoneWebSocketClient _wsClient;
    private readonly TimeSpan _refreshWindow;

    public ILoxoneHttpClient Http { get; }
    public ILoxoneWebSocketClient WebSocket { get; }
    public string? Username { get; private set; }

    public LoxoneClient(LoxoneConnectionOptions options, TimeSpan? refreshWindow = null)
    {
        _httpClient = new LoxoneHttpClient(options);
        _wsClient = new LoxoneWebSocketClient(_httpClient);
        _refreshWindow = refreshWindow ?? TimeSpan.FromSeconds(30);
        Http = new HttpProxy(this, _httpClient);
        WebSocket = new WebSocketProxy(this, _wsClient);
    }

    public LoxoneClient(string host, int port = 80, bool secure = false, TimeSpan? refreshWindow = null)
        : this(new LoxoneConnectionOptions(host, port, secure), refreshWindow)
    {
    }

    public LoxoneClient(ILoxoneHttpClient httpClient, ILoxoneWebSocketClient? wsClient = null, TimeSpan? refreshWindow = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _wsClient = wsClient ?? new LoxoneWebSocketClient(_httpClient);
        _refreshWindow = refreshWindow ?? TimeSpan.FromSeconds(30);
        Http = new HttpProxy(this, _httpClient);
        WebSocket = new WebSocketProxy(this, _wsClient);
    }

    /// <summary>
    /// Retrieves a JWT via HTTP and authenticates the websocket connection.
    /// </summary>
    public async Task LoginAsync(string user, string password, int permission = 4, string info = "LoxNet", CancellationToken cancellationToken = default)
    {
        _ = await Http.GetJwtAsync(user, password, permission, info, cancellationToken).ConfigureAwait(false);
        await WebSocket.ConnectAndAuthenticateAsync(user, cancellationToken).ConfigureAwait(false);
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

        var expiry = DateTimeOffset.FromUnixTimeSeconds(token.ValidUntil);
        if (expiry - DateTimeOffset.UtcNow <= _refreshWindow)
        {
            var user = Username ?? throw new InvalidOperationException("Client is not logged in");
            await _httpClient.RefreshJwtAsync(_wsClient, user, cancellationToken).ConfigureAwait(false);
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
        public TokenInfo? LastToken => _inner.LastToken;

        public async Task<JsonDocument> RequestJsonAsync(string path, CancellationToken cancellationToken = default)
        {
            await _parent.EnsureValidTokenAsync(cancellationToken).ConfigureAwait(false);
            return await _inner.RequestJsonAsync(path, cancellationToken).ConfigureAwait(false);
        }

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

        public Task ListenAsync(CancellationToken cancellationToken = default) =>
            _inner.ListenAsync(cancellationToken);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}
