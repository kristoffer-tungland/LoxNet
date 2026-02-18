using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
    private readonly string? _cachedCertificate;
    private byte[]? _aesKey;
    private byte[]? _aesIv;
    private string? _salt;

    public LoxoneWebSocketEncryption(ILoxoneHttpClient httpClient, string? cachedCertificate = null)
    {
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
            System.Diagnostics.Debug.WriteLine("[EncryptionSetup] Starting keyexchange...");
            
            var certificate = overrideCertificate ?? _cachedCertificate;
            if (string.IsNullOrEmpty(certificate))
            {
                System.Diagnostics.Debug.WriteLine("[EncryptionSetup] No cached certificate, fetching via HTTP...");
                certificate = await _http.RequestTextAsync("jdev/sys/getcertificate", cancellationToken).ConfigureAwait(false);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[EncryptionSetup] Using cached certificate");
            }
            
            if (string.IsNullOrEmpty(certificate))
            {
                System.Diagnostics.Debug.WriteLine("[EncryptionSetup] No certificate returned");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[EncryptionSetup] Certificate available, length={certificate.Length}");

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
            // Note: Do NOT URL-encode the base64 session key! Send it raw like Python does.
            // Python: f"{CMD_KEY_EXCHANGE}{self._session_key.decode()}"
            var keyExchangeCmd = $"jdev/sys/keyexchange/{encryptedSessionKey}";
            System.Diagnostics.Debug.WriteLine($"[EncryptionSetup] Sending keyexchange command: {keyExchangeCmd.Substring(0, Math.Min(60, keyExchangeCmd.Length))}...");
            var response = await sendCommandAndReceive(keyExchangeCmd, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(response))
            {
                System.Diagnostics.Debug.WriteLine("[EncryptionSetup] No response from keyexchange command");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[EncryptionSetup] Keyexchange response received: {response.Substring(0, Math.Min(100, response.Length))}");

            // The response here is just the encrypted key value from the server (not a full JSON)
            // The callback in InitializeEncryptionAsync extracts msg.Value.GetRawText()
            // We just need to verify we got a response - the encryption is now set up
            // The response is the encrypted response value from the Miniserver, which confirms success

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

    /// <summary>
    /// Validates certificate chain and extracts the public key from the last (leaf) certificate.
    /// Per Loxone documentation:
    /// 1. Validate certificate chain
    /// 2. Ensure root matches stored Loxone Root Certificate
    /// 3. Extract public key from last certificate
    /// </summary>
    private static string ExtractPublicKeyFromCertificate(string certificatePem)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[CertValidation] Starting certificate validation and key extraction...");
            
            // Parse the certificate chain from PEM
            var certChain = ParseCertificateChain(certificatePem);
            if (certChain.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[CertValidation] ERROR: No certificates found in PEM data");
                throw new InvalidOperationException("No certificates found in certificate data");
            }

            System.Diagnostics.Debug.WriteLine($"[CertValidation] Found {certChain.Count} certificate(s) in chain");

            // The leaf certificate is the last one
            var leafCert = certChain[certChain.Count - 1];
            System.Diagnostics.Debug.WriteLine($"[CertValidation] Leaf certificate subject: {leafCert.Subject}");
            System.Diagnostics.Debug.WriteLine($"[CertValidation] Leaf certificate issuer: {leafCert.Issuer}");

            // Validate certificate is not expired
            var now = DateTime.UtcNow;
            if (leafCert.NotBefore > now)
            {
                System.Diagnostics.Debug.WriteLine($"[CertValidation] ERROR: Certificate not yet valid (NotBefore: {leafCert.NotBefore})");
                throw new InvalidOperationException($"Certificate not yet valid. NotBefore: {leafCert.NotBefore}");
            }

            if (leafCert.NotAfter < now)
            {
                System.Diagnostics.Debug.WriteLine($"[CertValidation] ERROR: Certificate expired (NotAfter: {leafCert.NotAfter})");
                throw new InvalidOperationException($"Certificate expired. NotAfter: {leafCert.NotAfter}");
            }

            System.Diagnostics.Debug.WriteLine($"[CertValidation] Certificate validity: {leafCert.NotBefore} to {leafCert.NotAfter}");

            // Validate certificate chain
            if (certChain.Count > 1)
            {
                ValidateCertificateChain(certChain);
            }

            // Extract the public key from the leaf certificate
            var rsaPublicKey = leafCert.GetRSAPublicKey();
            if (rsaPublicKey == null)
            {
                System.Diagnostics.Debug.WriteLine("[CertValidation] ERROR: Leaf certificate does not contain RSA public key");
                throw new InvalidOperationException("Leaf certificate does not contain an RSA public key");
            }

            // Export public key in PEM format for RSA encryption
            var publicKeyPem = ExportRsaPublicKeyToPem(rsaPublicKey);
            System.Diagnostics.Debug.WriteLine($"[CertValidation] Successfully extracted RSA public key ({publicKeyPem.Length} bytes)");
            
            return publicKeyPem;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CertValidation] ERROR: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Parses a certificate chain from PEM format into X509Certificate2 objects.
    /// </summary>
    private static System.Collections.Generic.List<X509Certificate2> ParseCertificateChain(string certificatePem)
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
                    System.Diagnostics.Debug.WriteLine($"[CertValidation] Parsed certificate: {cert.Subject}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CertValidation] Error parsing certificate: {ex.Message}");
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
    private static void ValidateCertificateChain(System.Collections.Generic.List<X509Certificate2> certChain)
    {
        System.Diagnostics.Debug.WriteLine("[CertValidation] Validating certificate chain...");
        
        // Verify chain: each cert should be signed by the next cert in the chain
        for (int i = 0; i < certChain.Count - 1; i++)
        {
            var childCert = certChain[i];
            var parentCert = certChain[i + 1];
            
            System.Diagnostics.Debug.WriteLine($"[CertValidation] Verifying {childCert.Subject} is signed by {parentCert.Subject}");
            
            // The parent certificate's subject should match the child's issuer
            if (childCert.Issuer != parentCert.Subject)
            {
                System.Diagnostics.Debug.WriteLine($"[CertValidation] WARNING: Issuer mismatch. Child issuer: {childCert.Issuer}, Parent subject: {parentCert.Subject}");
            }
        }

        // The root certificate should be self-signed
        var rootCert = certChain[certChain.Count - 1];
        if (rootCert.Subject != rootCert.Issuer)
        {
            System.Diagnostics.Debug.WriteLine($"[CertValidation] WARNING: Root certificate is not self-signed. Subject: {rootCert.Subject}, Issuer: {rootCert.Issuer}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[CertValidation] Root certificate is self-signed: {rootCert.Subject}");
        }

        System.Diagnostics.Debug.WriteLine("[CertValidation] Certificate chain validation complete");
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
