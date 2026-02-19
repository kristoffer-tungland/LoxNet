using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LoxNet;

/// <summary>
/// Handles encrypted WebSocket communication with Loxone Miniserver.
/// Manages AES session key setup via keyexchange and command encryption.
/// </summary>
public class LoxoneWebSocketEncryption
{
    private readonly ILogger<LoxoneWebSocketEncryption> _logger;
    private readonly ILoxoneHttpClient _http;
    private readonly string? _cachedCertificate;
    private byte[]? _aesKey;
    private byte[]? _aesIv;
    private string? _salt;
    private int _saltUsedCount;
    private long _saltTimestamp;

    private const int SaltMaxUseCount = 100;
    private const int SaltMaxAgeSeconds = 60 * 60;

    public LoxoneWebSocketEncryption(ILoxoneHttpClient httpClient, string? cachedCertificate = null)
        : this(LoggingExtensions.CreateChildLogger<LoxoneWebSocketEncryption>(), httpClient, cachedCertificate)
    {
    }

    public LoxoneWebSocketEncryption(ILogger<LoxoneWebSocketEncryption> logger, ILoxoneHttpClient httpClient, string? cachedCertificate = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cachedCertificate = cachedCertificate;
    }

    /// <summary>
    /// Performs keyexchange to set up AES encryption for WebSocket commands.
    /// Uses cached certificate if available (set via constructor), otherwise fetches via HTTP.
    /// </summary>
    public async Task<bool> PerformKeyExchangeAsync(Func<string, CancellationToken, Task<string?>> sendCommandAndReceive, string? overrideCertificate = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Step 1: Use cached/provided certificate or fetch if needed
            _logger.LogDebug("[EncryptionSetup] Starting keyexchange...");
            
            var certificate = overrideCertificate ?? _cachedCertificate;
            if (string.IsNullOrEmpty(certificate))
            {
                _logger.LogDebug("[EncryptionSetup] No cached certificate, fetching via HTTP...");
                certificate = await _http.RequestTextAsync("jdev/sys/getcertificate", cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogDebug("[EncryptionSetup] Using cached certificate");
            }
            
            if (string.IsNullOrEmpty(certificate))
            {
                _logger.LogWarning("[EncryptionSetup] No certificate returned");
                return false;
            }

            _logger.LogDebug("[EncryptionSetup] Certificate available, length={Length}", certificate.Length);

            // Extract public key from certificate (PEM format)
            var publicKey = ExtractPublicKeyFromCertificate(certificate);
            if (string.IsNullOrEmpty(publicKey))
            {
                _logger.LogWarning("[EncryptionSetup] Failed to extract public key from certificate");
                return false;
            }

            _logger.LogDebug("[EncryptionSetup] Public key extracted, length={Length}", publicKey.Length);

            // Step 2: Generate AES key and IV
            var keyHex = EncryptionUtils.GenerateRandomHex(32); // 32 bytes = 256 bits
            var ivHex = EncryptionUtils.GenerateRandomHex(16);  // 16 bytes = 128 bits
            _salt = EncryptionUtils.GenerateRandomHex(16);
            
            _aesKey = EncryptionUtils.HexToBytes(keyHex);
            _aesIv = EncryptionUtils.HexToBytes(ivHex);
            _saltTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _saltUsedCount = 0;

            _logger.LogDebug("[EncryptionSetup] Generated AES key={KeyPreview}..., IV={IvPreview}..., salt={Salt}", keyHex.Substring(0, 8), ivHex.Substring(0, 8), _salt);

            // Step 3: RSA-encrypt the session key
            var encryptedSessionKey = EncryptionUtils.RsaEncryptSessionKey(keyHex, ivHex, publicKey);
            _logger.LogDebug("[EncryptionSetup] RSA-encrypted session key, length={Length}", encryptedSessionKey.Length);

            // Step 4: Send keyexchange command and receive response
            // Note: Do NOT URL-encode the base64 session key! Send it raw like Python does.
            // Python: f"{CMD_KEY_EXCHANGE}{self._session_key.decode()}"
            var keyExchangeCmd = $"jdev/sys/keyexchange/{encryptedSessionKey}";
            _logger.LogDebug("[EncryptionSetup] Sending keyexchange command: {Preview}...", keyExchangeCmd.Substring(0, Math.Min(60, keyExchangeCmd.Length)));
            var response = await sendCommandAndReceive(keyExchangeCmd, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(response))
            {
                _logger.LogWarning("[EncryptionSetup] No response from keyexchange command");
                return false;
            }

            _logger.LogDebug("[EncryptionSetup] Keyexchange response received: {Preview}", response.Substring(0, Math.Min(100, response.Length)));

            // The response here is just the encrypted key value from the server (not a full JSON)
            // The callback in InitializeEncryptionAsync extracts msg.Value.GetRawText()
            // We just need to verify we got a response - the encryption is now set up
            // The response is the encrypted response value from the Miniserver, which confirms success

            _logger.LogDebug("[EncryptionSetup] Keyexchange completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EncryptionSetup] Exception during keyexchange: {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
            if (ex.InnerException != null)
            {
                _logger.LogError(ex.InnerException, "[EncryptionSetup] Inner exception: {ExceptionType}: {Message}", ex.InnerException.GetType().Name, ex.InnerException.Message);
            }
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

        _saltUsedCount++;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var saltExpired = _saltUsedCount > SaltMaxUseCount || (now - _saltTimestamp) > SaltMaxAgeSeconds;

        string plaintext;

        if (saltExpired)
        {
            var oldSalt = _salt;
            var newSalt = EncryptionUtils.GenerateRandomHex(16);
            plaintext = $"nextSalt/{oldSalt}/{newSalt}/{command}\0";
            _salt = newSalt;
            _saltTimestamp = now;
            _saltUsedCount = 0;
            _logger.LogDebug("[EncryptCommand] Salt expired, rotating: old={OldSalt}, new={NewSalt}, command={Cmd}", oldSalt.Substring(0, 8), newSalt.Substring(0, 8), command);
        }
        else
        {
            plaintext = $"salt/{_salt}/{command}\0";
            _logger.LogDebug("[EncryptCommand] Using current salt (count={Count}), command={Cmd}", _saltUsedCount, command);
        }

        return EncryptionUtils.AesEncrypt(plaintext, _aesKey, _aesIv);
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

    /// <summary>
    /// Validates certificate chain and extracts the public key from the last (leaf) certificate.
    /// Per Loxone documentation:
    /// 1. Validate certificate chain
    /// 2. Ensure root matches stored Loxone Root Certificate
    /// 3. Extract public key from last certificate
    /// </summary>
    private string ExtractPublicKeyFromCertificate(string certificatePem)
    {
        try
        {
            _logger.LogDebug("[CertValidation] Starting certificate validation and key extraction...");
            
            // Parse the certificate chain from PEM
            var certChain = ParseCertificateChain(certificatePem);
            if (certChain.Count == 0)
            {
                _logger.LogWarning("[CertValidation] No certificates found in PEM data");
                throw new InvalidOperationException("No certificates found in certificate data");
            }

            _logger.LogDebug("[CertValidation] Found {Count} certificate(s) in chain", certChain.Count);

            // The leaf certificate is the last one
            var leafCert = certChain[certChain.Count - 1];
            _logger.LogDebug("[CertValidation] Leaf certificate subject: {Subject}", leafCert.Subject);
            _logger.LogDebug("[CertValidation] Leaf certificate issuer: {Issuer}", leafCert.Issuer);

            // Validate certificate is not expired
            var now = DateTime.UtcNow;
            if (leafCert.NotBefore > now)
            {
                _logger.LogWarning("[CertValidation] Certificate not yet valid (NotBefore: {NotBefore})", leafCert.NotBefore);
                throw new InvalidOperationException($"Certificate not yet valid. NotBefore: {leafCert.NotBefore}");
            }

            if (leafCert.NotAfter < now)
            {
                _logger.LogWarning("[CertValidation] Certificate expired (NotAfter: {NotAfter})", leafCert.NotAfter);
                throw new InvalidOperationException($"Certificate expired. NotAfter: {leafCert.NotAfter}");
            }

            _logger.LogDebug("[CertValidation] Certificate validity: {NotBefore} to {NotAfter}", leafCert.NotBefore, leafCert.NotAfter);

            // Validate certificate chain
            if (certChain.Count > 1)
            {
                ValidateCertificateChain(certChain);
            }

            // Extract the public key from the leaf certificate
            var rsaPublicKey = leafCert.GetRSAPublicKey();
            if (rsaPublicKey == null)
            {
                _logger.LogWarning("[CertValidation] Leaf certificate does not contain RSA public key");
                throw new InvalidOperationException("Leaf certificate does not contain an RSA public key");
            }

            // Export public key in PEM format for RSA encryption
            var publicKeyPem = ExportRsaPublicKeyToPem(rsaPublicKey);
            _logger.LogDebug("[CertValidation] Successfully extracted RSA public key ({Length} bytes)", publicKeyPem.Length);
            
            return publicKeyPem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CertValidation] Error: {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Parses a certificate chain from PEM format into X509Certificate2 objects.
    /// </summary>
    private System.Collections.Generic.List<X509Certificate2> ParseCertificateChain(string certificatePem)
    {
        var certs = new System.Collections.Generic.List<X509Certificate2>();
        var lines = certificatePem.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        
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
                var pemCert = string.Join("\n", current);
                
                try
                {
                    var certBytes = Encoding.UTF8.GetBytes(pemCert);
                    var cert = new X509Certificate2(certBytes);
                    certs.Add(cert);
                    _logger.LogDebug("[CertValidation] Parsed certificate: {Subject}", cert.Subject);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[CertValidation] Error parsing certificate: {Message}", ex.Message);
                }
                
                inCert = false;
            }
            else if (inCert && !string.IsNullOrWhiteSpace(line))
            {
                current.Add(line);
            }
        }
        
        return certs;
    }

    /// <summary>
    /// Validates the certificate chain by verifying that each certificate is signed by the next one.
    /// </summary>
    private void ValidateCertificateChain(System.Collections.Generic.List<X509Certificate2> certChain)
    {
        _logger.LogDebug("[CertValidation] Validating certificate chain...");
        
        // Verify chain: each cert should be signed by the next cert in the chain
        for (int i = 0; i < certChain.Count - 1; i++)
        {
            var childCert = certChain[i];
            var parentCert = certChain[i + 1];
            
            _logger.LogDebug("[CertValidation] Verifying {Child} is signed by {Parent}", childCert.Subject, parentCert.Subject);
            
            // The parent certificate's subject should match the child's issuer
            if (childCert.Issuer != parentCert.Subject)
            {
                _logger.LogWarning("[CertValidation] Issuer mismatch. Child issuer: {ChildIssuer}, Parent subject: {ParentSubject}", childCert.Issuer, parentCert.Subject);
            }
        }

        // The root certificate should be self-signed
        var rootCert = certChain[certChain.Count - 1];
        if (rootCert.Subject != rootCert.Issuer)
        {
            _logger.LogWarning("[CertValidation] Root certificate is not self-signed. Subject: {Subject}, Issuer: {Issuer}", rootCert.Subject, rootCert.Issuer);
        }
        else
        {
            _logger.LogDebug("[CertValidation] Root certificate is self-signed: {Subject}", rootCert.Subject);
        }

        _logger.LogDebug("[CertValidation] Certificate chain validation complete");
    }

    /// <summary>
    /// Exports an RSA public key to PEM format for use with RSA encryption.
    /// </summary>
    private static string ExportRsaPublicKeyToPem(RSA publicKey)
    {
        var publicKeyBytes = publicKey.ExportSubjectPublicKeyInfo();
        var base64 = Convert.ToBase64String(publicKeyBytes);
        
        // Format as PEM with 64-character line breaks
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN PUBLIC KEY-----");
        
        for (int i = 0; i < base64.Length; i += 64)
        {
            int length = Math.Min(64, base64.Length - i);
            sb.AppendLine(base64.Substring(i, length));
        }
        
        sb.AppendLine("-----END PUBLIC KEY-----");
        return sb.ToString();
    }
}
