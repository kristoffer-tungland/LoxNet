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

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var opts = _http.Options;
        _ws = new ClientWebSocket();
        _ws.Options.AddSubProtocol("remotecontrol");
        string scheme = opts.Secure ? "wss" : "ws";
        await _ws.ConnectAsync(new Uri($"{scheme}://{opts.Host}:{opts.Port}/ws/rfc6455"), cancellationToken).ConfigureAwait(false);
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_ws is not null)
        {
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken).ConfigureAwait(false);
            _ws.Dispose();
            _ws = null;
        }
    }

    private async Task<string> ReceiveStringAsync(CancellationToken cancellationToken)
    {
        if (_ws is null) throw new InvalidOperationException("WebSocket not connected");
        var buffer = new ArraySegment<byte>(new byte[8192]);
        using var ms = new System.IO.MemoryStream();
        while (true)
        {
            var result = await _ws.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            ms.Write(buffer.Array!, buffer.Offset, result.Count);
            if (result.EndOfMessage)
                break;
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private async Task SendStringAsync(string text, CancellationToken cancellationToken)
    {
        if (_ws is null) throw new InvalidOperationException("WebSocket not connected");
        var data = Encoding.UTF8.GetBytes(text);
        await _ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<LoxoneMessage> SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        await SendStringAsync(command, cancellationToken).ConfigureAwait(false);
        var response = await ReceiveStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(response);
        return LoxoneMessageParser.Parse(doc);
    }

    public async Task<LoxoneMessage> AuthenticateWithTokenAsync(string token, string user, CancellationToken cancellationToken = default)
    {
        using var doc = await _http.RequestJsonAsync("jdev/sys/getkey", cancellationToken).ConfigureAwait(false);
        var msg = LoxoneMessageParser.Parse(doc);
        msg.EnsureSuccess();
        var key = HexUtils.FromHexString(msg.Value.GetString()!);
        var digest = LoxoneHttpClient.HmacHex(key, Encoding.UTF8.GetBytes(token), System.Security.Cryptography.HashAlgorithmName.SHA1);
        return await SendCommandAsync($"authwithtoken/{digest}/{user}", cancellationToken).ConfigureAwait(false);
    }

    public async Task<LoxoneMessage> ConnectAndAuthenticateAsync(string user, CancellationToken cancellationToken = default)
    {
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        var token = _http.LastToken?.Token ?? throw new InvalidOperationException("No JWT token available");
        return await AuthenticateWithTokenAsync(token, user, cancellationToken).ConfigureAwait(false);
    }

    public async Task ListenAsync(CancellationToken cancellationToken = default)
    {
        while (_ws is not null && _ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var msg = await ReceiveStringAsync(cancellationToken).ConfigureAwait(false);
            MessageReceived?.Invoke(this, msg);
        }
    }

    public async Task KeepAliveAsync(CancellationToken cancellationToken = default) => _ = await SendCommandAsync("keepalive", cancellationToken).ConfigureAwait(false);

    public async Task<LoxoneMessage> CommandAsync(string path, CancellationToken cancellationToken = default) => await SendCommandAsync(path, cancellationToken).ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
    }
}
