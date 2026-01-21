using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LoxNet;

/// <summary>
/// Handles encrypted WebSocket communication with Loxone Miniserver.
/// Manages AES session key setup via keyexchange and command encryption.
/// </summary>
public class LoxoneWebSocketEncryption
{
    private readonly ILoxoneHttpClient _http;
    private byte[]? _aesKey;
    private byte[]? _aesIv;
    private string? _salt;

    public LoxoneWebSocketEncryption(ILoxoneHttpClient httpClient)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Performs keyexchange to set up AES encryption for WebSocket commands.
    /// </summary>
    public async Task<bool> PerformKeyExchangeAsync(Func<string, CancellationToken, Task<string?>> sendCommandAndReceive, CancellationToken cancellationToken = default)
    {
        try
        {
            // Step 1: Fetch Miniserver certificate to extract public key
            System.Diagnostics.Debug.WriteLine("[EncryptionSetup] Starting keyexchange...");
            var certificate = await _http.RequestTextAsync("jdev/sys/getcertificate", cancellationToken).ConfigureAwait(false);
            
            if (string.IsNullOrEmpty(certificate))
            {
                System.Diagnostics.Debug.WriteLine("[EncryptionSetup] No certificate returned");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[EncryptionSetup] Certificate obtained, length={certificate.Length}, starts with: {certificate.Substring(0, Math.Min(50, certificate.Length))}");

            // Extract public key from certificate (PEM format)
            var publicKey = ExtractPublicKeyFromCertificate(certificate);
            if (string.IsNullOrEmpty(publicKey))
            {
                System.Diagnostics.Debug.WriteLine("[EncryptionSetup] Failed to extract public key from certificate");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[EncryptionSetup] Public key extracted, length={publicKey.Length}");

            // Step 2: Generate AES key and IV
            var keyHex = EncryptionUtils.GenerateRandomHex(32); // 32 bytes = 256 bits
            var ivHex = EncryptionUtils.GenerateRandomHex(16);  // 16 bytes = 128 bits
            _salt = EncryptionUtils.GenerateRandomHex(16);
            
            _aesKey = EncryptionUtils.HexToBytes(keyHex);
            _aesIv = EncryptionUtils.HexToBytes(ivHex);

            System.Diagnostics.Debug.WriteLine($"[EncryptionSetup] Generated AES key={keyHex.Substring(0, 8)}..., IV={ivHex.Substring(0, 8)}..., salt={_salt}");

            // Step 3: RSA-encrypt the session key
            var encryptedSessionKey = EncryptionUtils.RsaEncryptSessionKey(keyHex, ivHex, publicKey);
            System.Diagnostics.Debug.WriteLine($"[EncryptionSetup] RSA-encrypted session key, length={encryptedSessionKey.Length}");

            // Step 4: Send keyexchange command and receive response
            var keyExchangeCmd = $"jdev/sys/keyexchange/{Uri.EscapeDataString(encryptedSessionKey)}";
            System.Diagnostics.Debug.WriteLine($"[EncryptionSetup] Sending keyexchange command: {keyExchangeCmd.Substring(0, Math.Min(60, keyExchangeCmd.Length))}...");
            var response = await sendCommandAndReceive(keyExchangeCmd, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(response))
            {
                System.Diagnostics.Debug.WriteLine("[EncryptionSetup] No response from keyexchange command");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[EncryptionSetup] Keyexchange response received: {response.Substring(0, Math.Min(100, response.Length))}");

            // Parse keyexchange response (should be JSON with success code)
            try
            {
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                
                // Check for LL.Code indicating success (typically 200)
                if (root.TryGetProperty("LL", out var ll) && ll.TryGetProperty("Code", out var code))
                {
                    var codeValue = code.GetInt32();
                    if (codeValue != 200)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EncryptionSetup] Keyexchange failed with code {codeValue}");
                        return false;
                    }
                }
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EncryptionSetup] Failed to parse keyexchange response: {ex.Message}");
                return false;
            }

            System.Diagnostics.Debug.WriteLine("[EncryptionSetup] Keyexchange completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EncryptionSetup] Exception during keyexchange: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
                System.Diagnostics.Debug.WriteLine($"[EncryptionSetup] Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            return false;
        }
    }

    /// <summary>
    /// Encrypts a command for transmission over WebSocket.
    /// </summary>
    public string EncryptCommand(string command)
    {
        if (_aesKey == null || _aesIv == null || string.IsNullOrEmpty(_salt))
            throw new InvalidOperationException("Keyexchange not yet performed");

        // Format: salt/{salt}/{command}
        var plaintext = $"salt/{_salt}/{command}";
        var encrypted = EncryptionUtils.AesEncrypt(plaintext, _aesKey, _aesIv);
        
        // Update salt after each command for security
        _salt = EncryptionUtils.GenerateRandomHex(16);

        return encrypted;
    }

    /// <summary>
    /// Decrypts a response received over WebSocket.
    /// </summary>
    public string DecryptResponse(string ciphertext)
    {
        if (_aesKey == null || _aesIv == null)
            throw new InvalidOperationException("Keyexchange not yet performed");

        return EncryptionUtils.AesDecrypt(ciphertext, _aesKey, _aesIv);
    }

    private static string ExtractPublicKeyFromCertificate(string certificate)
    {
        // If certificate chain (multiple certificates), extract the leaf (last) one
        var leafCert = ExtractLeafCertificateFromChain(certificate);
        
        // The certificate should be in PEM format with BEGIN/END markers
        if (leafCert.Contains("-----BEGIN"))
            return leafCert;

        // If it's not PEM-formatted, try to wrap it
        if (!leafCert.StartsWith("-----BEGIN"))
        {
            return $"-----BEGIN CERTIFICATE-----\n{leafCert}\n-----END CERTIFICATE-----";
        }

        return leafCert;
    }

    private static string ExtractLeafCertificateFromChain(string certificateChain)
    {
        // Split by certificate boundaries to handle certificate chains
        var certs = new System.Collections.Generic.List<string>();
        var lines = certificateChain.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        
        var current = new System.Collections.Generic.List<string>();
        bool inCert = false;
        
        foreach (var line in lines)
        {
            if (line.Contains("-----BEGIN CERTIFICATE-----"))
            {
                inCert = true;
                current.Clear();
                current.Add(line);
            }
            else if (line.Contains("-----END CERTIFICATE-----"))
            {
                current.Add(line);
                certs.Add(string.Join("\n", current));
                inCert = false;
            }
            else if (inCert && !string.IsNullOrWhiteSpace(line))
            {
                current.Add(line);
            }
        }
        
        // Return the last certificate (leaf) or the whole input if no chain detected
        return certs.Count > 0 ? certs[certs.Count - 1] : certificateChain;
    }
}
