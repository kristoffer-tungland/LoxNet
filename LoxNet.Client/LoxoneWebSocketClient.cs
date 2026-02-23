using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Websocket.Client;

namespace LoxNet;

public class LoxoneWebSocketClient : ILoxoneWebSocketClient
{
    private readonly ILogger<LoxoneWebSocketClient> _logger;
    private readonly ILoxoneHttpClient _http;
    private WebsocketClient? _wsClient;
    private LoxoneWebSocketEncryption? _encryption;
    // Two-frame state machine: Loxone sends the 8-byte header as one frame, then the payload
    // as the next separate frame. We store the pending header between the two callbacks.
    private byte[]? _pendingHeader;
    private readonly object _frameStateLock = new();
    private string? _cachedCertificate;
    private string? _tokenKeyHex;
    private string? _tokenHashAlg;
    
    // Channel for async message passing - properly handles queuing and async waiting
    private Channel<string>? _messageChannel;
    
    public event EventHandler<string>? MessageReceived;

    public LoxoneWebSocketClient(ILoxoneHttpClient httpClient)
        : this(LoggingExtensions.CreateChildLogger<LoxoneWebSocketClient>(), httpClient)
    {
    }

    public LoxoneWebSocketClient(ILogger<LoxoneWebSocketClient> logger, ILoxoneHttpClient httpClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var opts = _http.Options;
        string scheme = opts.Secure ? "wss" : "ws";
        var uri = new Uri($"{scheme}://{opts.Host}:{opts.Port}/ws/rfc6455");

        _wsClient = new WebsocketClient(uri);

        // The Miniserver sends binary protocol frames (headers + payloads) as WebSocket Text
        // frames, which is technically spec-violating but standard Loxone behaviour.
        // By default Websocket.Client decodes text frames as UTF-8, which corrupts bytes > 127.
        // Setting Latin-1 (ISO-8859-1) preserves every byte value 0-255 as a 1:1 character,
        // so we can reliably recover the original bytes with Encoding.Latin1.GetBytes().
        _wsClient.MessageEncoding = Encoding.Latin1;

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
            _logger.LogWarning("[WebSocket] Disconnected: Status={Status}, Reason={Reason}, Exception={ExceptionMessage}", info.CloseStatus, info.CloseStatusDescription, info.Exception?.Message);
        });

        _wsClient.MessageReceived.Subscribe(msg =>
        {
            try
            {
                // Recover the raw bytes for this WebSocket frame.
                // The Miniserver sends everything (including binary protocol frames) as WebSocket
                // Text frames. With MessageEncoding = Latin1 the bytes are preserved 1:1 as
                // char values 0-255, so Latin1.GetBytes() recovers the original byte values.
                byte[] frameBytes;
                if (msg.Binary != null && msg.Binary.Length > 0)
                {
                    frameBytes = msg.Binary;
                }
                else if (!string.IsNullOrEmpty(msg.Text))
                {
                    frameBytes = Encoding.Latin1.GetBytes(msg.Text);
                }
                else
                {
                    return;
                }

                HandleFrame(frameBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WebSocket] Error processing message");
            }
        });

        await _wsClient.Start();
        _logger.LogDebug("[WebSocket] Connected to {Uri}", uri);
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

    /// <summary>
    /// Handles a single raw WebSocket frame using the Loxone two-frame protocol:
    /// <list type="bullet">
    ///   <item>Frame 1 (8 bytes starting with 0x03): a binary protocol header — stored as
    ///   <see cref="_pendingHeader"/> until the payload frame arrives.</item>
    ///   <item>Frame 2 (N bytes, no magic prefix): the payload for the previously stored
    ///   header — combined with that header and dispatched.</item>
    ///   <item>Any frame that is not an 8-byte header and for which no header is pending is
    ///   treated as a plain text (JSON) command response.</item>
    /// </list>
    /// </summary>
    private void HandleFrame(byte[] frameBytes)
    {
        lock (_frameStateLock)
        {
            // Detect a binary protocol header: exactly 8 bytes starting with magic 0x03.
            // (The Miniserver can also send the header inside a larger frame that is still
            // only 8 bytes; payload is always a separate frame per the spec.)
            bool isHeader = frameBytes.Length == 8 && frameBytes[0] == 0x03;

            if (isHeader)
            {
                // Keepalive header has zero-length payload — dispatch immediately.
                var msgType = (LoxoneMessageType)frameBytes[1];
                uint payloadLength = BitConverter.ToUInt32(frameBytes, 4);

                if (msgType == LoxoneMessageType.Keepalive || payloadLength == 0)
                {
                    var immediateMsg = new LoxoneBinaryMessage(msgType, null, Array.Empty<(string, string)>(), ReadOnlyMemory<byte>.Empty);
                    DispatchBinaryMessage(immediateMsg);
                    _pendingHeader = null;
                }
                else
                {
                    // Store header and wait for the payload frame.
                    _pendingHeader = frameBytes;
                }
                return;
            }

            if (_pendingHeader != null)
            {
                // This frame is the payload for the stored header.
                var header = _pendingHeader;
                _pendingHeader = null;

                bool estimated = (header[2] & 0x80) != 0;
                var msgType = (LoxoneMessageType)header[1];

                if (estimated)
                {
                    // The estimated header was just a size hint; the real header immediately
                    // follows at the start of this "payload" frame.
                    // Re-process this frame from the beginning to find the real header.
                    HandleFrame(frameBytes);
                    return;
                }

                try
                {
                    var payload = new ReadOnlyMemory<byte>(frameBytes);
                    var parsed = BinaryProtocolParser.ParsePayloadPublic(msgType, payload);
                    DispatchBinaryMessage(parsed);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[WebSocket] Error parsing payload for {Type}", msgType);
                }
                return;
            }

            // No pending header — this is a plain text (JSON) command response.
            var text = Encoding.UTF8.GetString(frameBytes);
            _logger.LogDebug("[WebSocket] Text frame (JSON response): {Preview}", text.Substring(0, Math.Min(120, text.Length)));
            _messageChannel?.Writer.TryWrite(text);
            MessageReceived?.Invoke(this, text);
        }
    }

    /// <summary>
    /// Dispatches a fully-parsed binary protocol message:
    /// <list type="bullet">
    ///   <item>Text messages are forwarded to the command-response channel AND to MessageReceived.</item>
    ///   <item>Event-table messages (ValueStates / TextStates) emit one MessageReceived event per
    ///   state entry using the {"uuid":"…","value":"…"} JSON format consumed by LoxoneStructureState.</item>
    ///   <item>Keepalive messages are forwarded to the command-response channel.</item>
    ///   <item>All other message types are logged and silently dropped.</item>
    /// </list>
    /// </summary>
    private void DispatchBinaryMessage(LoxoneBinaryMessage msg)
    {
        switch (msg.MessageType)
        {
            case LoxoneMessageType.Text:
            {
                var text = msg.Text ?? string.Empty;
                _logger.LogDebug("[WebSocket] Text message received: {Preview}", text.Substring(0, Math.Min(120, text.Length)));
                _messageChannel?.Writer.TryWrite(text);
                MessageReceived?.Invoke(this, text);
                break;
            }

            case LoxoneMessageType.ValueStates:
            case LoxoneMessageType.TextStates:
            {
                _logger.LogDebug("[WebSocket] Event table ({Type}) with {Count} entries", msg.MessageType, msg.StateEvents.Count);
                foreach (var (uuid, value) in msg.StateEvents)
                {
                    var json = BinaryProtocolParser.BuildStateJson(uuid, value);
                    MessageReceived?.Invoke(this, json);
                }
                break;
            }

            case LoxoneMessageType.Keepalive:
                _logger.LogDebug("[WebSocket] Keepalive received");
                _messageChannel?.Writer.TryWrite("{\"LL\":{\"control\":\"keepalive\",\"Code\":\"200\",\"value\":\"\"}}");
                break;

            case LoxoneMessageType.OutOfService:
                _logger.LogWarning("[WebSocket] Miniserver sent OutOfService indicator");
                break;

            default:
                _logger.LogDebug("[WebSocket] Unhandled binary message type {Type}, payload {Length} bytes", msg.MessageType, msg.Payload.Length);
                break;
        }
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
        _logger.LogDebug("[SendString] Sending text message: {Preview}...", text.Substring(0, Math.Min(150, text.Length)));
        _wsClient.Send(text);
        return Task.CompletedTask;
    }

    private async Task<LoxoneMessage> SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        _logger.LogDebug("[SendCommand] Sending: {Preview}...", command.Substring(0, Math.Min(80, command.Length)));
        await SendStringAsync(command, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("[SendCommand] Command sent, waiting for response...");
        
        try
        {
            var response = await ReceiveStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("[SendCommand] Response received: {Preview}...", response.Substring(0, Math.Min(100, response.Length)));
            
            // Use the response parser to handle different response formats
            var parser = new LoxoneResponseParser(LoggingExtensions.CreateChildLogger<LoxoneResponseParser>(), _encryption);
            var result = parser.Parse(response);
            
            return result;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("closed without receiving"))
        {
            _logger.LogWarning(ex, "[SendCommand] WebSocket closed before receiving response: {Message}", ex.Message);
            throw new InvalidOperationException($"Server closed WebSocket for command '{command}': {ex.Message}", ex);
        }
    }

    public async Task<LoxoneMessage> AuthenticateWithTokenAsync(string token, string user, CancellationToken cancellationToken = default)
    {
        if (_encryption is null)
            throw new InvalidOperationException("Encryption not initialized; call InitializeEncryptionAsync first");

        byte[] keyBytes;
        var hashAlg = _tokenHashAlg;

        if (string.IsNullOrWhiteSpace(_tokenKeyHex) || string.IsNullOrWhiteSpace(hashAlg))
        {
            using var doc = await _http.RequestJsonAsync("jdev/sys/getkey", cancellationToken).ConfigureAwait(false);
            var msg = LoxoneMessageParser.Parse(doc);
            msg.EnsureSuccess();
            _tokenKeyHex = msg.Value.GetString();
            hashAlg = "sha1";
        }

        keyBytes = HexUtils.FromHexString(_tokenKeyHex!);
        var algoName = hashAlg!.Equals("sha256", StringComparison.OrdinalIgnoreCase)
            ? System.Security.Cryptography.HashAlgorithmName.SHA256
            : System.Security.Cryptography.HashAlgorithmName.SHA1;

        var digest = LoxoneHttpClient.HmacHex(keyBytes, Encoding.UTF8.GetBytes(token), algoName);
        var authCommand = $"authwithtoken/{digest}/{user}";

        _logger.LogDebug("[AuthenticateWithToken] Sending hashed auth (unencrypted) for user={User}, token_hash={Hash}, authCmd={Cmd}", user, digest.Substring(0, 16), authCommand);
        var authMsg = await SendCommandAsync(authCommand, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("[AuthenticateWithToken] Auth response code={Code}, value={Preview}", authMsg.Code, authMsg.Value.GetRawText().Substring(0, Math.Min(100, authMsg.Value.GetRawText().Length)));

        // 400 Bad Request might mean we're already authenticated (JWT was the auth)
        // 401 Unauthorized means invalid credentials
        if (authMsg.Code == 401)
        {
            _logger.LogWarning("[AuthenticateWithToken] Hashed token auth failed with 401 (invalid credentials). Retrying with plaintext token.");
            var rawAuthCommand = $"authwithtoken/{token}/{user}";
            _logger.LogDebug("[AuthenticateWithToken] Sending plaintext auth (unencrypted), rawCmd preview={Cmd}", rawAuthCommand.Substring(0, Math.Min(100, rawAuthCommand.Length)));
            var rawAuthMsg = await SendCommandAsync(rawAuthCommand, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("[AuthenticateWithToken] Raw token auth response code={Code}, value={Preview}", rawAuthMsg.Code, rawAuthMsg.Value.GetRawText().Substring(0, Math.Min(100, rawAuthMsg.Value.GetRawText().Length)));
            return rawAuthMsg;
        }
        
        // For 400 or 200, consider it a success - the JWT itself might be the authentication
        if (authMsg.Code >= 200 && authMsg.Code < 500)
        {
            _logger.LogDebug("[AuthenticateWithToken] Auth response code {Code} accepted. JWT token is the authentication.", authMsg.Code);
            return authMsg;
        }

        return authMsg;
    }

    /// <summary>
    /// Acquires a JWT token via encrypted WebSocket communication.
    /// This is the recommended flow per Loxone protocol documentation.
    /// </summary>
    public async Task<TokenInfo> AcquireJwtTokenAsync(string user, string password, int permission, string info, CancellationToken cancellationToken = default)
    {
        if (_wsClient is null) throw new InvalidOperationException("WebSocket not connected");
        if (_encryption is null) throw new InvalidOperationException("Encryption not initialized; call PerformKeyExchangeAsync first");

        _logger.LogDebug("[LoxoneWebSocketClient] Acquiring JWT for user={User}, permission={Permission}", user, permission);

        // Build getjwt command (same as HTTP flow, but will be encrypted)
        var keyInfo = await _http.GetKey2Async(user, cancellationToken).ConfigureAwait(false);
        var keyBytes = HexUtils.FromHexString(keyInfo.Key);
        _tokenKeyHex = keyInfo.Key;
        _tokenHashAlg = keyInfo.HashAlg;
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
        var encodedCmd = Uri.EscapeDataString(encryptedCmd);
        var response = await SendCommandAsync($"jdev/sys/enc/{encodedCmd}", cancellationToken).ConfigureAwait(false);

        // Parse the JWT response
        var val = response.Value;
        
        // If the response is an encrypted string, decrypt it first
        if (val.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var encryptedResponse = val.GetString()!;
            _logger.LogDebug("[LoxoneWebSocketClient] JWT response is encrypted, decrypting...");
            
            try
            {
                var decrypted = _encryption!.DecryptResponse(encryptedResponse);
                _logger.LogDebug("[LoxoneWebSocketClient] Decrypted JWT response: {Preview}...", decrypted.Substring(0, Math.Min(100, decrypted.Length)));
                
                // Parse the decrypted JSON
                using var doc = JsonDocument.Parse(decrypted);
                val = doc.RootElement;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LoxoneWebSocketClient] Failed to decrypt JWT response: {Message}", ex.Message);
                throw new InvalidOperationException($"Failed to decrypt JWT response: {ex.Message}", ex);
            }
        }
        
        var token = new TokenInfo(
            val.GetProperty("token").GetString()!,
            val.GetProperty("validUntil").GetInt64(),
            val.GetProperty("tokenRights").GetInt32(),
            val.GetProperty("unsecurePass").GetBoolean(),
            val.GetProperty("key").GetString()!
        );

        _logger.LogDebug("[LoxoneWebSocketClient] JWT acquired: rights={Rights}", token.TokenRights);
        return token;
    }

    /// <summary>
    /// Prepares encryption by fetching the Miniserver certificate via HTTP.
    /// This should be called BEFORE ConnectAsync to keep the post-connect auth window short.
    /// Per Loxone documentation Step 2: Retrieve Certificate (before Step 3: Open WebSocket).
    /// </summary>
    public async Task PrepareEncryptionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[LoxoneWebSocketClient] Preparing encryption - fetching certificate from Miniserver...");
        
        try
        {
            _cachedCertificate = await _http.RequestTextAsync("jdev/sys/getcertificate", cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(_cachedCertificate))
            {
                _logger.LogWarning("[LoxoneWebSocketClient] Empty certificate returned from server");
            }
            else
            {
                _logger.LogDebug("[LoxoneWebSocketClient] Certificate cached successfully, length={Length}", _cachedCertificate.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LoxoneWebSocketClient] Error preparing encryption: {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
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
        
        _encryption = new LoxoneWebSocketEncryption(LoggingExtensions.CreateChildLogger<LoxoneWebSocketEncryption>(), _http, _cachedCertificate);
        
        // Use SendCommandAsync which returns the response
        // During keyexchange, the response is NOT AES-encrypted, so we need a special handler
        Func<string, CancellationToken, Task<string?>> sendCommandAndReceive = async (cmd, ct) =>
        {
            try
            {
                _logger.LogDebug("[Keyexchange] Sending keyexchange via SendCommandAsync: {Preview}...", cmd.Substring(0, Math.Min(80, cmd.Length)));
                
                // For keyexchange, skip decryption since the response isn't AES-encrypted yet
                var parser = new LoxoneResponseParser(LoggingExtensions.CreateChildLogger<LoxoneResponseParser>(), _encryption);
                parser.SetSkipDecryption(true);
                
                // Manually send and receive to use the parser with skip flag
                await SendStringAsync(cmd, ct).ConfigureAwait(false);
                _logger.LogDebug("[Keyexchange] Command sent, waiting for response...");
                var response = await ReceiveStringAsync(ct).ConfigureAwait(false);
                _logger.LogDebug("[Keyexchange] Response received: {Preview}...", response.Substring(0, Math.Min(100, response.Length)));
                
                var msg = parser.Parse(response);
                try
                {
                    // Check for errors first
                    if (msg.Code < 200 || msg.Code >= 300)
                    {
                        _logger.LogWarning("[Keyexchange] Server returned error code {Code}: {Message}", msg.Code, msg.Message);
                        return null;
                    }

                    // Ensure document isn't disposed during async operations
                    msg.KeepAlive();
                    
                    // Extract value - might be string (error) or object (success)
                    if (msg.Value.ValueKind == System.Text.Json.JsonValueKind.Undefined)
                    {
                        _logger.LogWarning("[Keyexchange] Server response has no value");
                        return null;
                    }

                    var rawText = msg.Value.GetRawText();
                    _logger.LogDebug("[Keyexchange] Response received and parsed: {Preview}...", rawText.Substring(0, Math.Min(100, rawText.Length)));
                    return rawText;
                }
                finally
                {
                    await msg.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Server closed WebSocket"))
            {
                _logger.LogWarning(ex, "[Keyexchange] Server closed connection during keyexchange. This typically means: 1) Keyexchange command format is incorrect 2) RSA-encrypted session key is invalid 3) Miniserver doesn't support encrypted WebSocket communication. Inner={Inner}", ex.InnerException?.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Keyexchange] Error sending keyexchange: {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
                return null;
            }
        };

        var success = await _encryption.PerformKeyExchangeAsync(sendCommandAndReceive, _cachedCertificate, cancellationToken).ConfigureAwait(false);
        
        if (success)
        {
            _logger.LogDebug("[LoxoneWebSocketClient] Encryption initialized successfully");
        }
        else
        {
            _logger.LogWarning("[LoxoneWebSocketClient] Encryption initialization failed");
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
        while (_wsClient is not null && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
#endif
    }

    public async Task KeepAliveAsync(CancellationToken cancellationToken = default) => _ = await SendCommandAsync("keepalive", cancellationToken).ConfigureAwait(false);

    public async Task<LoxoneMessage> CommandAsync(string path, CancellationToken cancellationToken = default) => await SendCommandAsync(path, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Sends an encrypted command and waits for response (useful for refreshjwt, checktoken, killtoken).
    /// Encrypts the command using AES-256-CBC and sends via jdev/sys/enc/ endpoint.
    /// </summary>
    public async Task<LoxoneMessage> SendEncryptedCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (_encryption is null)
            throw new InvalidOperationException("Encryption not initialized. Call InitializeEncryptionAsync first.");

        _logger.LogDebug("[LoxoneWebSocketClient] Sending encrypted command: {Command}", command);

        // Encrypt the command
        var encryptedCommand = _encryption.EncryptCommand(command);
        
        // Send via the standard jdev/sys/enc endpoint
        var fullCommand = $"jdev/sys/enc/{encryptedCommand}";
        
        return await SendCommandAsync(fullCommand, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
    }
}
