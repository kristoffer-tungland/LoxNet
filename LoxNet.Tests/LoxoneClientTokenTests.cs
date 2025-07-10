using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using LoxNet;

namespace LoxNet.Tests;

public class LoxoneClientTokenTests
{
    private class MockHttp : ILoxoneHttpClient
    {
        public int RefreshCalls { get; private set; }
        public TokenInfo? LastToken { get; set; }
        public TokenInfo RefreshResult = new("new", 0, 0, false, "k");
        public TokenInfo LoginResult = new("login", 0, 0, false, "k");

        public LoxoneConnectionOptions Options => new("localhost", 0, false);
        public Task<JsonDocument> RequestJsonAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(JsonDocument.Parse("{}"));
        public Task<KeyInfo> GetKey2Async(string user, CancellationToken cancellationToken = default) => Task.FromResult(new KeyInfo("k", "s", "sha"));
        public Task<TokenInfo> GetJwtAsync(string user, string password, int permission, string info, CancellationToken cancellationToken = default)
        {
            LastToken = LoginResult;
            return Task.FromResult(LoginResult);
        }
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
        public int CommandCalls { get; private set; }
        public event EventHandler<string>? MessageReceived;
        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<LoxoneMessage> AuthenticateWithTokenAsync(string token, string user, CancellationToken cancellationToken = default) => Task.FromResult(new LoxoneMessage(0, default, null));
        public Task<LoxoneMessage> ConnectAndAuthenticateAsync(string user, CancellationToken cancellationToken = default) => Task.FromResult(new LoxoneMessage(0, default, null));
        public Task KeepAliveAsync(CancellationToken cancellationToken = default) { CommandCalls++; return Task.CompletedTask; }
        public Task<LoxoneMessage> CommandAsync(string path, CancellationToken cancellationToken = default) { CommandCalls++; return Task.FromResult(new LoxoneMessage(0, default, null)); }
        public Task ListenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task EnsureValidTokenAsync_RefreshesExpired()
    {
        var http = new MockHttp();
        var ws = new MockWs();
        var client = new LoxoneClient(http, ws);
        await client.LoginAsync("u", "p");

        http.LastToken = new TokenInfo("old", DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds(), 0, false, "k");
        http.RefreshResult = new TokenInfo("new", DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds(), 0, false, "k");

        await client.EnsureValidTokenAsync();

        Assert.Equal(1, http.RefreshCalls);
        Assert.Equal("new", http.LastToken!.Token);
    }

    [Fact]
    public async Task WebSocketCommand_TriggersRefresh()
    {
        var http = new MockHttp();
        var ws = new MockWs();
        var client = new LoxoneClient(http, ws, TimeSpan.FromSeconds(30));
        await client.LoginAsync("u", "p");

        http.LastToken = new TokenInfo("old", DateTimeOffset.UtcNow.AddSeconds(10).ToUnixTimeSeconds(), 0, false, "k");
        http.RefreshResult = new TokenInfo("new", DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds(), 0, false, "k");

        await client.WebSocket.CommandAsync("foo");

        Assert.Equal(1, http.RefreshCalls);
        Assert.Equal(1, ws.CommandCalls);
        Assert.Equal("new", http.LastToken!.Token);
    }
}
