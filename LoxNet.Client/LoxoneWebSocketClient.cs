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
    private LoxoneWebSocketEncryption? _encryption;
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
        
        // Keep receiving until we have the complete message (header + payload)
        while (true)
        {
            var result = await _ws.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            
            System.Diagnostics.Debug.WriteLine(
                $"[WebSocket] Received frame: Count={result.Count}, EndOfMessage={result.EndOfMessage}, " +
                $"MessageType={result.MessageType}, TotalBuffered={ms.Length}");
            
            if (result.Count > 0)
            {
                ms.Write(buffer.Array!, buffer.Offset, result.Count);
            }
            
            // Check if we have at least the header to read payload length
            if (ms.Length >= 8)
            {
                var data = ms.ToArray();
                uint payloadLength = BitConverter.ToUInt32(data, 4);
                uint expectedTotalLength = 8 + payloadLength;
                
                System.Diagnostics.Debug.WriteLine(
                    $"[WebSocket] Header parsed: payloadLength={payloadLength}, expectedTotal={expectedTotalLength}, " +
                    $"currentLength={ms.Length}, EndOfMessage={result.EndOfMessage}");
                
                // Check if we have the complete message (header + payload)
                if (ms.Length >= expectedTotalLength)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebSocket] Message complete, parsing...");
                    return BinaryProtocolParser.ParseMessage(data);
                }
                
                // If we still need more data but got EndOfMessage, continue anyway
                // (server may send header in one frame, payload in another)
                if (result.EndOfMessage && ms.Length < expectedTotalLength)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[WebSocket] EndOfMessage but incomplete. Got {ms.Length}, need {expectedTotalLength}. " +
                        $"Continuing to receive...");
                    // Continue loop to receive next frame(s)
                    continue;
                }
            }
            else if (result.Count == 0 && result.EndOfMessage)
            {
                // Empty frame with EndOfMessage after we have some data likely means transmission complete
                if (ms.Length >= 8)
                {
                    var data = ms.ToArray();
                    uint payloadLength = BitConverter.ToUInt32(data, 4);
                    if (ms.Length >= 8 + payloadLength)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebSocket] Empty final frame, message complete.");
                        return BinaryProtocolParser.ParseMessage(data);
                    }
                }
                
                // Empty frame with EndOfMessage but no complete data means connection closed
                if (ms.Length == 0)
                {
                    throw new InvalidOperationException("WebSocket closed without receiving response (0 bytes).");
                }
                
                throw new InvalidOperationException(
                    $"WebSocket closed before receiving complete message. Got {ms.Length} bytes. " +
                    $"First 8 bytes: {BitConverter.ToString(ms.ToArray(), 0, Math.Min(8, (int)ms.Length))}");
            }
        }
    }

    private async Task SendStringAsync(string text, CancellationToken cancellationToken)
    {
        if (_ws is null) throw new InvalidOperationException("WebSocket not connected");
        var textData = Encoding.UTF8.GetBytes(text);
        
        // Build binary protocol message: [4 bytes: message type] [4 bytes: payload length] [payload]
        using var ms = new System.IO.MemoryStream();
        ms.Write(BitConverter.GetBytes(3u), 0, 4);  // Message type/flags (3 for command/text request)
        ms.Write(BitConverter.GetBytes((uint)textData.Length), 0, 4);  // Payload length
        ms.Write(textData, 0, textData.Length);  // Payload
        
        var binaryData = ms.ToArray();
        await _ws.SendAsync(new ArraySegment<byte>(binaryData), WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<LoxoneMessage> SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        System.Diagnostics.Debug.WriteLine($"[SendCommand] Sending: {command.Substring(0, Math.Min(80, command.Length))}...");
        await SendStringAsync(command, cancellationToken).ConfigureAwait(false);
        System.Diagnostics.Debug.WriteLine($"[SendCommand] Command sent, waiting for response...");
        
        try
        {
            var response = await ReceiveStringAsync(cancellationToken).ConfigureAwait(false);
            System.Diagnostics.Debug.WriteLine($"[SendCommand] Response received: {response.Substring(0, Math.Min(100, response.Length))}...");
            var jsonPayload = NormalizeJsonPayload(response);
            using var doc = JsonDocument.Parse(jsonPayload);
            return LoxoneMessageParser.Parse(doc);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("closed without receiving"))
        {
            System.Diagnostics.Debug.WriteLine($"[SendCommand] WebSocket closed before receiving response: {ex.Message}");
            // Server closed connection - return error message
            throw new InvalidOperationException($"Server closed WebSocket for command '{command}': {ex.Message}", ex);
        }
    }

    private static string NormalizeJsonPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new JsonException("Loxone response was empty.");
        }

        // Trim leading/trailing whitespace and control characters
        var startIndex = 0;
        while (startIndex < payload.Length && (char.IsWhiteSpace(payload[startIndex]) || char.IsControl(payload[startIndex])))
        {
            startIndex++;
        }

        var endIndex = payload.Length - 1;
        while (endIndex > startIndex && (char.IsWhiteSpace(payload[endIndex]) || char.IsControl(payload[endIndex])))
        {
            endIndex--;
        }

        return payload[startIndex..(endIndex + 1)];
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

    /// <summary>
    /// Acquires a JWT token via encrypted WebSocket communication.
    /// This is the recommended flow per Loxone protocol documentation.
    /// </summary>
    public async Task<TokenInfo> AcquireJwtTokenAsync(string user, string password, int permission, string info, CancellationToken cancellationToken = default)
    {
        if (_ws is null) throw new InvalidOperationException("WebSocket not connected");
        if (_encryption is null) throw new InvalidOperationException("Encryption not initialized; call PerformKeyExchangeAsync first");

        System.Diagnostics.Debug.WriteLine($"[LoxoneWebSocketClient] Acquiring JWT for user={user}, permission={permission}");

        // Build getjwt command (same as HTTP flow, but will be encrypted)
        var keyInfo = await _http.GetKey2Async(user, cancellationToken).ConfigureAwait(false);
        var keyBytes = HexUtils.FromHexString(keyInfo.Key);
        var algoName = keyInfo.HashAlg.Equals("sha256", StringComparison.OrdinalIgnoreCase) 
            ? System.Security.Cryptography.HashAlgorithmName.SHA256 
            : System.Security.Cryptography.HashAlgorithmName.SHA1;
        
        using var algo = algoName == System.Security.Cryptography.HashAlgorithmName.SHA256 
            ? (System.Security.Cryptography.HashAlgorithm)System.Security.Cryptography.SHA256.Create() 
            : System.Security.Cryptography.SHA1.Create();
        
        var pwHash = LoxoneHttpClient.HashToUpperInternal(Encoding.UTF8.GetBytes($"{password}:{keyInfo.Salt}"), algo);
        var userHash = LoxoneHttpClient.HmacHex(keyBytes, Encoding.UTF8.GetBytes($"{user}:{pwHash}"), algoName);
        var uid = Guid.NewGuid().ToString("N");
        var encInfo = Uri.EscapeDataString(info);
        
        var getJwtCmd = $"jdev/sys/getjwt/{userHash}/{Uri.EscapeDataString(user)}/{permission}/{uid}/{encInfo}";
        
        // Encrypt and send the command
        var encryptedCmd = _encryption.EncryptCommand(getJwtCmd);
        var response = await SendCommandAsync($"jdev/sys/fenc/{Uri.EscapeDataString(encryptedCmd)}", cancellationToken).ConfigureAwait(false);

        // Parse the JWT response
        var val = response.Value;
        var token = new TokenInfo(
            val.GetProperty("token").GetString()!,
            val.GetProperty("validUntil").GetInt64(),
            val.GetProperty("tokenRights").GetInt32(),
            val.GetProperty("unsecurePass").GetBoolean(),
            val.GetProperty("key").GetString()!
        );

        System.Diagnostics.Debug.WriteLine($"[LoxoneWebSocketClient] JWT acquired: rights={token.TokenRights}");
        return token;
    }

    /// <summary>
    /// Initializes encryption setup via keyexchange handshake.
    /// Must be called after WebSocket connection is established and before sending encrypted commands.
    /// </summary>
    public async Task<bool> InitializeEncryptionAsync(CancellationToken cancellationToken = default)
    {
        if (_ws is null) throw new InvalidOperationException("WebSocket not connected");
        
        _encryption = new LoxoneWebSocketEncryption(_http);
        
        // Use SendCommandAsync which returns the response
        Func<string, CancellationToken, Task<string?>> sendCommandAndReceive = async (cmd, ct) =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[Keyexchange] Sending keyexchange via SendCommandAsync: {cmd.Substring(0, Math.Min(80, cmd.Length))}...");
                var msg = await SendCommandAsync(cmd, ct).ConfigureAwait(false);
                // Convert LoxoneMessage back to JSON string for parsing
                var rawText = msg.Value.GetRawText();
                System.Diagnostics.Debug.WriteLine($"[Keyexchange] Response received and parsed: {rawText.Substring(0, Math.Min(100, rawText.Length))}...");
                return rawText;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Server closed WebSocket"))
            {
                System.Diagnostics.Debug.WriteLine($"[Keyexchange] Server closed connection during keyexchange. This typically means:");
                System.Diagnostics.Debug.WriteLine($"  1. Keyexchange command format is incorrect");
                System.Diagnostics.Debug.WriteLine($"  2. RSA-encrypted session key is invalid");
                System.Diagnostics.Debug.WriteLine($"  3. Miniserver doesn't support encrypted WebSocket communication");
                System.Diagnostics.Debug.WriteLine($"[Keyexchange] Exception: {ex.InnerException?.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Keyexchange] Error sending keyexchange: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        };

        var success = await _encryption.PerformKeyExchangeAsync(sendCommandAndReceive, cancellationToken).ConfigureAwait(false);
        
        if (success)
        {
            System.Diagnostics.Debug.WriteLine("[LoxoneWebSocketClient] Encryption initialized successfully");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[LoxoneWebSocketClient] Encryption initialization failed");
            _encryption = null;
        }

        return success;
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
