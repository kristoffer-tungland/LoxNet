using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LoxNet;

public class LoxoneWebSocketClient : ILoxoneWebSocketClient
{
    private readonly ILoxoneHttpClient _http;
    private ClientWebSocket? _ws;
    public event EventHandler<string>? MessageReceived;

    public LoxoneWebSocketClient(ILoxoneHttpClient httpClient)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task ConnectAsync()
    {
        var opts = _http.Options;
        _ws = new ClientWebSocket();
        _ws.Options.AddSubProtocol("remotecontrol");
        string scheme = opts.Secure ? "wss" : "ws";
        await _ws.ConnectAsync(new Uri($"{scheme}://{opts.Host}:{opts.Port}/ws/rfc6455"), CancellationToken.None);
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
        return LoxoneMessageParser.Parse(doc);
    }

    public async Task<LoxoneMessage> AuthenticateWithTokenAsync(string token, string user)
    {
        using var doc = await _http.RequestJsonAsync("jdev/sys/getkey");
        var key = HexUtils.FromHexString(doc.RootElement.GetProperty("LL").GetProperty("value").GetString()!);
        var digest = LoxoneHttpClient.HmacHex(key, Encoding.UTF8.GetBytes(token), System.Security.Cryptography.HashAlgorithmName.SHA1);
        return await SendCommandAsync($"authwithtoken/{digest}/{user}");
    }

    public async Task<LoxoneMessage> ConnectAndAuthenticateAsync(string user)
    {
        await ConnectAsync();
        var token = _http.LastToken?.Token ?? throw new InvalidOperationException("No JWT token available");
        return await AuthenticateWithTokenAsync(token, user);
    }

    public async Task ListenAsync(CancellationToken cancellationToken = default)
    {
        while (_ws is not null && _ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var msg = await ReceiveStringAsync();
            MessageReceived?.Invoke(this, msg);
        }
    }

    public async Task KeepAliveAsync() => _ = await SendCommandAsync("keepalive");

    public async Task<LoxoneMessage> CommandAsync(string path) => await SendCommandAsync(path);

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
    }
}
