using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace LoxNet;

/// <summary>
/// Utilities for AES encryption/decryption used in Loxone protocol.
/// </summary>
public static class EncryptionUtils
{
    /// <summary>
    /// Encrypts data using AES256-CBC.
    /// </summary>
    /// <param name="plaintext">Plain data to encrypt.</param>
    /// <param name="key">AES key (32 bytes for AES256).</param>
    /// <param name="iv">Initialization vector (16 bytes).</param>
    /// <returns>Encrypted data (Base64-encoded).</returns>
    public static string AesEncrypt(string plaintext, byte[] key, byte[] iv)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (iv == null) throw new ArgumentNullException(nameof(iv));
        if (key.Length != 32) throw new ArgumentException("Key must be 32 bytes for AES256", nameof(key));
        if (iv.Length != 16) throw new ArgumentException("IV must be 16 bytes", nameof(iv));

        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (var encryptor = aes.CreateEncryptor())
            {
                var data = Encoding.UTF8.GetBytes(plaintext);
                var encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);
                return Convert.ToBase64String(encrypted);
            }
        }
    }

    /// <summary>
    /// Decrypts data using AES256-CBC.
    /// </summary>
    /// <param name="ciphertext">Encrypted data (Base64-encoded).</param>
    /// <param name="key">AES key (32 bytes for AES256).</param>
    /// <param name="iv">Initialization vector (16 bytes).</param>
    /// <returns>Decrypted plaintext.</returns>
    public static string AesDecrypt(string ciphertext, byte[] key, byte[] iv)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (iv == null) throw new ArgumentNullException(nameof(iv));
        if (key.Length != 32) throw new ArgumentException("Key must be 32 bytes for AES256", nameof(key));
        if (iv.Length != 16) throw new ArgumentException("IV must be 16 bytes", nameof(iv));

        try
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                {
                    var data = Convert.FromBase64String(ciphertext);
                    var decrypted = decryptor.TransformFinalBlock(data, 0, data.Length);
                    return Encoding.UTF8.GetString(decrypted);
                }
            }
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"Failed to decode base64 ciphertext. Input length: {ciphertext.Length}, first 50 chars: {ciphertext.Substring(0, Math.Min(50, ciphertext.Length))}",
                ex);
        }
        catch (CryptographicException ex)
        {
            // Padding errors usually indicate wrong key/IV or corrupted data
            throw new InvalidOperationException(
                $"AES decryption failed - likely wrong key/IV or corrupted data. Key length: {key.Length}, IV length: {iv.Length}, ciphertext length: {ciphertext.Length}",
                ex);
        }
    }

    /// <summary>
    /// RSA-encrypts session key using the Miniserver's public key.
    /// </summary>
    /// <param name="sessionKeyHex">AES key in hex format.</param>
    /// <param name="ivHex">IV in hex format.</param>
    /// <param name="publicKeyPem">RSA public key or certificate in PEM format.</param>
    /// <returns>RSA-encrypted session key (Base64-encoded).</returns>
    public static string RsaEncryptSessionKey(string sessionKeyHex, string ivHex, string publicKeyPem)
    {
        if (string.IsNullOrEmpty(sessionKeyHex)) throw new ArgumentNullException(nameof(sessionKeyHex));
        if (string.IsNullOrEmpty(ivHex)) throw new ArgumentNullException(nameof(ivHex));
        if (string.IsNullOrEmpty(publicKeyPem)) throw new ArgumentNullException(nameof(publicKeyPem));

        var sessionKeyAndIv = $"{sessionKeyHex}:{ivHex}";
        var data = Encoding.UTF8.GetBytes(sessionKeyAndIv);

        // Try loading as X509 certificate first (contains public key)
        if (publicKeyPem.Contains("BEGIN CERTIFICATE"))
        {
            try
            {
                using (var cert = LoadCertificateFromPem(publicKeyPem))
                {
#if NET48
                    var rsaKey = cert.PublicKey.Key as RSA;
#else
                    var rsaKey = cert.GetRSAPublicKey();
#endif
                    if (rsaKey != null)
                    {
                        var encrypted = rsaKey.Encrypt(data, RSAEncryptionPadding.Pkcs1);
                        return Convert.ToBase64String(encrypted);
                    }
                }
            }
            catch { }
        }

        // Try as raw PEM public key
        using (var rsa = RSA.Create())
        {
#if NET48
            // .NET 4.8 doesn't support ImportFromPem on RSA, but we already tried certificate above
            throw new InvalidOperationException("Failed to extract RSA public key from certificate or PEM");
#else
            rsa.ImportFromPem(publicKeyPem.ToCharArray());
            var encrypted = rsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
            return Convert.ToBase64String(encrypted);
#endif
        }
    }

    private static byte[] ExtractPublicKeyFromPem(string pem)
    {
        // Remove PEM headers and line breaks
        var keyString = pem
            .Replace("-----BEGIN CERTIFICATE-----", "")
            .Replace("-----END CERTIFICATE-----", "")
            .Replace("-----BEGIN PUBLIC KEY-----", "")
            .Replace("-----END PUBLIC KEY-----", "")
            .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
            .Replace("-----END RSA PRIVATE KEY-----", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Trim();
        
        return Convert.FromBase64String(keyString);
    }

    private static X509Certificate2 LoadCertificateFromPem(string pem)
    {
        // If this is a certificate chain, extract the leaf certificate (last one)
        var leafCertPem = ExtractLeafCertificateFromChain(pem);
        var certBytes = ExtractPublicKeyFromPem(leafCertPem);
#if NET9_0_OR_GREATER
        return System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(certBytes);
#else
        return new X509Certificate2(certBytes);
#endif
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

    /// <summary>
    /// Generates a random hex string of the specified byte length.
    /// </summary>
    public static string GenerateRandomHex(int byteCount)
    {
        var bytes = new byte[byteCount];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    /// <summary>
    /// Converts a hex string to bytes.
    /// </summary>
    public static byte[] HexToBytes(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0)
            throw new ArgumentException("Invalid hex string", nameof(hex));

        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }

    /// <summary>
    /// Converts bytes to a hex string.
    /// </summary>
    public static string BytesToHex(byte[] bytes)
    {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
