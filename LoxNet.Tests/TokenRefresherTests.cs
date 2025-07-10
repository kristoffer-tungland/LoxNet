using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using LoxNet;

namespace LoxNet.Tests;

public class TokenRefresherTests
{
    private class MockHttp : ILoxoneHttpClient
    {
        public int RefreshCalls { get; private set; }
        public TokenInfo? LastToken { get; set; }
        public TokenInfo RefreshResult = new("new", 0, 0, false, "k");

        public LoxoneConnectionOptions Options => new("localhost", 0, false);
        public Task<JsonDocument> RequestJsonAsync(string path, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<KeyInfo> GetKey2Async(string user, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TokenInfo> GetJwtAsync(string user, string password, int permission, string info, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TokenInfo> RefreshJwtAsync(ILoxoneWebSocketClient wsClient, string user, CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            LastToken = RefreshResult;
            return Task.FromResult(RefreshResult);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class MockWs : ILoxoneWebSocketClient
    {
        public event EventHandler<string>? MessageReceived;
        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<LoxoneMessage> AuthenticateWithTokenAsync(string token, string user, CancellationToken cancellationToken = default) => Task.FromResult(new LoxoneMessage(0, default, null));
        public Task<LoxoneMessage> ConnectAndAuthenticateAsync(string user, CancellationToken cancellationToken = default) => Task.FromResult(new LoxoneMessage(0, default, null));
        public Task KeepAliveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<LoxoneMessage> CommandAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(new LoxoneMessage(0, default, null));
        public Task ListenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class MockClient : ILoxoneClient
    {
        public MockHttp Http { get; } = new();
        public ILoxoneHttpClient HttpIfc => Http;
        public MockWs Ws { get; } = new();
        public ILoxoneWebSocketClient WebSocketIfc => Ws;

        ILoxoneHttpClient ILoxoneClient.Http => Http;
        ILoxoneWebSocketClient ILoxoneClient.WebSocket => Ws;
        ValueTask IAsyncDisposable.DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task EnsureValidTokenAsync_UsesExistingToken()
    {
        var client = new MockClient();
        client.Http.LastToken = new TokenInfo("t", DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds(), 0, false, "k");
        var refresher = new TokenRefresher(client, "user");

        var tok = await refresher.EnsureValidTokenAsync();

        Assert.Equal("t", tok.Token);
        Assert.Equal(0, client.Http.RefreshCalls);
    }

    [Fact]
    public async Task EnsureValidTokenAsync_RefreshesExpired()
    {
        var client = new MockClient();
        client.Http.LastToken = new TokenInfo("old", DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds(), 0, false, "k");
        client.Http.RefreshResult = new TokenInfo("new", DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds(), 0, false, "k");
        var refresher = new TokenRefresher(client, "user");

        var tok = await refresher.EnsureValidTokenAsync();

        Assert.Equal("new", tok.Token);
        Assert.Equal(1, client.Http.RefreshCalls);
    }

    [Fact]
    public async Task EnsureValidTokenAsync_RefreshesNearExpiry()
    {
        var client = new MockClient();
        client.Http.LastToken = new TokenInfo("old", DateTimeOffset.UtcNow.AddSeconds(10).ToUnixTimeSeconds(), 0, false, "k");
        client.Http.RefreshResult = new TokenInfo("new", DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds(), 0, false, "k");
        var refresher = new TokenRefresher(client, "user", TimeSpan.FromSeconds(30));

        var tok = await refresher.EnsureValidTokenAsync();

        Assert.Equal("new", tok.Token);
        Assert.Equal(1, client.Http.RefreshCalls);
    }

    [Fact]
    public async Task Delegate_InvokesEnsureValidTokenAsync()
    {
        var client = new MockClient();
        client.Http.LastToken = new TokenInfo("old", DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds(), 0, false, "k");
        client.Http.RefreshResult = new TokenInfo("new", DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds(), 0, false, "k");
        var refresher = new TokenRefresher(client, "user");

        var tok = await refresher.RefreshDelegate(CancellationToken.None);

        Assert.Equal("new", tok.Token);
        Assert.Equal(1, client.Http.RefreshCalls);
    }
}
