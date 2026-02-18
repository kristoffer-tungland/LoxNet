using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Websocket.Client;

namespace LoxNet;

public class LoxoneWebSocketClient : ILoxoneWebSocketClient
{
    private readonly ILoxoneHttpClient _http;
    private WebsocketClient? _wsClient;
    private LoxoneWebSocketEncryption? _encryption;
    private System.IO.MemoryStream? _receiveBuffer;
    private string? _cachedCertificate;
    
    // Channel for async message passing - properly handles queuing and async waiting
    private Channel<string>? _messageChannel;
    
    public event EventHandler<string>? MessageReceived;

    public LoxoneWebSocketClient(ILoxoneHttpClient httpClient)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var opts = _http.Options;
        string scheme = opts.Secure ? "wss" : "ws";
        var uri = new Uri($"{scheme}://{opts.Host}:{opts.Port}/ws/rfc6455");

        _wsClient = new WebsocketClient(uri);
        _receiveBuffer = new System.IO.MemoryStream();
        
        // Create unbounded channel for message passing - will never block or fail
        _messageChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions 
        { 
            SingleReader = false,  // Multiple readers (for broadcast)
            SingleWriter = true    // Single writer (the message handler)
        });
        
        // Configure Websocket.Client for better reliability
        _wsClient.ReconnectTimeout = null; // disable auto-reconnect for authentication flow
        _wsClient.ErrorReconnectTimeout = TimeSpan.FromSeconds(5); // but reconnect on errors
        
        // Monitor disconnections for diagnostics
        _wsClient.DisconnectionHappened.Subscribe(info =>
        {
            System.Diagnostics.Debug.WriteLine($"[WebSocket] Disconnected: Status={info.CloseStatus}, Reason={info.CloseStatusDescription}, Exception={info.Exception?.Message}");
        });

        _wsClient.MessageReceived.Subscribe(msg =>
        {
            try
            {
                if (!string.IsNullOrEmpty(msg.Text))
                {
                    // Write to channel (never blocks or fails on unbounded channel)
                    _messageChannel?.Writer.TryWrite(msg.Text);
                    
                    MessageReceived?.Invoke(this, msg.Text);
                    return;
                }

                if (msg.Binary != null && msg.Binary.Length > 0)
                {
                    lock (_receiveBuffer!)
                    {
                        // Append incoming bytes
                        _receiveBuffer!.Write(msg.Binary, 0, msg.Binary.Length);
                        var bufferArray = _receiveBuffer.ToArray();

                        // Try to parse as many complete messages as possible
                        while (BinaryProtocolParser.TryParseMessage(bufferArray, out var parsedJson, out var parsedLen))
                        {
                            try
                            {
                                // Write to channel (never blocks or fails on unbounded channel)
                                _messageChannel?.Writer.TryWrite(parsedJson);
                                
                                MessageReceived?.Invoke(this, parsedJson);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[WebSocket] Error processing parsed JSON: {ex.Message}");
                            }

                            // Remove parsed bytes from buffer
                            var remaining = bufferArray.Length - parsedLen;
                            if (remaining <= 0)
                            {
                                _receiveBuffer.SetLength(0);
                                bufferArray = Array.Empty<byte>();
                                break;
                            }

                            var tmp = new byte[remaining];
                            Array.Copy(bufferArray, parsedLen, tmp, 0, remaining);
                            _receiveBuffer.SetLength(0);
                            _receiveBuffer.Write(tmp, 0, tmp.Length);
                            bufferArray = _receiveBuffer.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebSocket] Error processing message: {ex.Message}");
            }
        });

        await _wsClient.Start();
        System.Diagnostics.Debug.WriteLine($"[WebSocket] Connected to {uri}");
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_wsClient is not null)
        {
            _wsClient.Dispose();
            _wsClient = null;
        }

        return Task.CompletedTask;
    }

    private async Task<string> ReceiveStringAsync(CancellationToken cancellationToken)
    {
        if (_messageChannel == null)
            throw new InvalidOperationException("WebSocket not connected. Call ConnectAsync first.");

        // Use ConfigureAwait(false) to avoid capturing synchronization context
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5000));
        
        var result = await _messageChannel.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
        return result;
    }

    private Task SendStringAsync(string text, CancellationToken cancellationToken)
    {
        if (_wsClient is null) throw new InvalidOperationException("WebSocket not connected");

        // Send as plain text message, not wrapped in binary frame with headers
        // The Python version sends the command directly: await self.connection.send([command])
        _wsClient.Send(text);
        return Task.CompletedTask;
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
            
            // Use the response parser to handle different response formats
            var parser = new LoxoneResponseParser(_encryption);
            var result = parser.Parse(response);
            
            return result;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("closed without receiving"))
        {
            System.Diagnostics.Debug.WriteLine($"[SendCommand] WebSocket closed before receiving response: {ex.Message}");
            throw new InvalidOperationException($"Server closed WebSocket for command '{command}': {ex.Message}", ex);
        }
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
        if (_wsClient is null) throw new InvalidOperationException("WebSocket not connected");
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
        // Note: Don't URL-encode the encrypted command! Send it raw like the keyexchange.
        var encryptedCmd = _encryption.EncryptCommand(getJwtCmd);
        var response = await SendCommandAsync($"jdev/sys/fenc/{encryptedCmd}", cancellationToken).ConfigureAwait(false);

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
    /// Prepares encryption by fetching the Miniserver certificate via HTTP.
    /// This should be called BEFORE ConnectAsync to keep the post-connect auth window short.
    /// Per Loxone documentation Step 2: Retrieve Certificate (before Step 3: Open WebSocket).
    /// </summary>
    public async Task PrepareEncryptionAsync(CancellationToken cancellationToken = default)
    {
        System.Diagnostics.Debug.WriteLine("[LoxoneWebSocketClient] Preparing encryption - fetching certificate from Miniserver...");
        
        try
        {
            _cachedCertificate = await _http.RequestTextAsync("jdev/sys/getcertificate", cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(_cachedCertificate))
            {
                System.Diagnostics.Debug.WriteLine("[LoxoneWebSocketClient] Warning: Empty certificate returned from server");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[LoxoneWebSocketClient] Certificate cached successfully, length={_cachedCertificate.Length}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoxoneWebSocketClient] Error preparing encryption: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Initializes encryption setup via keyexchange handshake.
    /// Must be called after WebSocket connection is established and before sending encrypted commands.
    /// Requires PrepareEncryptionAsync to have been called first (certificate must be cached).
    /// </summary>
    public async Task<bool> InitializeEncryptionAsync(CancellationToken cancellationToken = default)
    {
        if (_wsClient is null) throw new InvalidOperationException("WebSocket not connected");
        
        _encryption = new LoxoneWebSocketEncryption(_http, _cachedCertificate);
        
        // Use SendCommandAsync which returns the response
        // During keyexchange, the response is NOT AES-encrypted, so we need a special handler
        Func<string, CancellationToken, Task<string?>> sendCommandAndReceive = async (cmd, ct) =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[Keyexchange] Sending keyexchange via SendCommandAsync: {cmd.Substring(0, Math.Min(80, cmd.Length))}...");
                
                // For keyexchange, skip decryption since the response isn't AES-encrypted yet
                var parser = new LoxoneResponseParser(_encryption);
                parser.SetSkipDecryption(true);
                
                // Manually send and receive to use the parser with skip flag
                await SendStringAsync(cmd, ct).ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[Keyexchange] Command sent, waiting for response...");
                var response = await ReceiveStringAsync(ct).ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[Keyexchange] Response received: {response.Substring(0, Math.Min(100, response.Length))}...");
                
                var msg = parser.Parse(response);
                try
                {
                    // Check for errors first
                    if (msg.Code < 200 || msg.Code >= 300)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Keyexchange] Server returned error code {msg.Code}: {msg.Message}");
                        return null;
                    }

                    // Ensure document isn't disposed during async operations
                    msg.KeepAlive();
                    
                    // Extract value - might be string (error) or object (success)
                    if (msg.Value.ValueKind == System.Text.Json.JsonValueKind.Undefined)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Keyexchange] Server response has no value");
                        return null;
                    }

                    var rawText = msg.Value.GetRawText();
                    System.Diagnostics.Debug.WriteLine($"[Keyexchange] Response received and parsed: {rawText.Substring(0, Math.Min(100, rawText.Length))}...");
                    return rawText;
                }
                finally
                {
                    await msg.DisposeAsync().ConfigureAwait(false);
                }
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

        var success = await _encryption.PerformKeyExchangeAsync(sendCommandAndReceive, _cachedCertificate, cancellationToken).ConfigureAwait(false);
        
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
#if NET9_0_OR_GREATER
        while (_wsClient is not null && _wsClient.IsRunning && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
#else
        while (_ws is not null && _ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
#endif
    }

    public async Task KeepAliveAsync(CancellationToken cancellationToken = default) => _ = await SendCommandAsync("keepalive", cancellationToken).ConfigureAwait(false);

    public async Task<LoxoneMessage> CommandAsync(string path, CancellationToken cancellationToken = default) => await SendCommandAsync(path, cancellationToken).ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
    }
}
