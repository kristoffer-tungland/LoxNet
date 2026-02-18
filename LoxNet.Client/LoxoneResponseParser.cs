using System;
using System.Text.Json;

namespace LoxNet;

/// <summary>
/// Parses different types of responses from the Loxone Miniserver.
/// Handles:
/// - Regular JSON responses: {"LL": {"Code": "200", "value": {...}}}
/// - Encrypted responses: base64-encoded encrypted strings
/// - Plain encrypted values: base64 strings needing decryption
/// </summary>
public class LoxoneResponseParser
{
    private readonly LoxoneWebSocketEncryption? _encryption;
    private bool _skipDecryption = false;

    public LoxoneResponseParser(LoxoneWebSocketEncryption? encryption = null)
    {
        _encryption = encryption;
    }

    /// <summary>
    /// Sets whether to skip decryption attempts. Useful during keyexchange when
    /// responses aren't yet encrypted with the AES session key.
    /// </summary>
    public void SetSkipDecryption(bool skip) => _skipDecryption = skip;

    /// <summary>
    /// Parses a response from the Miniserver, handling both JSON and encrypted formats.
    /// </summary>
    public LoxoneMessage Parse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new ArgumentException("Response cannot be empty", nameof(response));
        }

        var normalized = NormalizePayload(response);

        // Try to parse as JSON first
        if (IsJsonResponse(normalized))
        {
            return ParseJsonResponse(normalized);
        }

        // If we're skipping decryption (e.g., during keyexchange), treat base64 as plain value
        if (_skipDecryption && IsBase64(normalized))
        {
            System.Diagnostics.Debug.WriteLine($"[ResponseParser] Decryption disabled, treating base64 as plain value");
            var doc = JsonDocument.Parse("\"" + normalized + "\"");
            return new LoxoneMessage(200, doc.RootElement, null, doc);
        }

        // Try to decrypt if it looks like an encrypted response
        if (IsEncryptedResponse(normalized))
        {
            return ParseEncryptedResponse(normalized);
        }

        // If it's a plain string that might be a JSON value
        if (normalized.StartsWith("{") || normalized.StartsWith("["))
        {
            try
            {
                var doc = JsonDocument.Parse(normalized);
                return new LoxoneMessage(200, doc.RootElement, null, doc);
            }
            catch
            {
                // Not valid JSON, treat as encrypted
                return ParseEncryptedResponse(normalized);
            }
        }

        // Assume it's encrypted or encoded
        return ParseEncryptedResponse(normalized);
    }

    private static bool IsJsonResponse(string payload)
    {
        var trimmed = payload.TrimStart();
        return trimmed.StartsWith("{") && payload.Contains("\"LL\"");
    }

    private static bool IsEncryptedResponse(string payload)
    {
        // Encrypted responses are typically base64-encoded and don't start with {
        var trimmed = payload.TrimStart();
        if (trimmed.StartsWith("{"))
            return false;
        
        if (trimmed.StartsWith("["))
            return false;

        // Only treat as encrypted if it's valid base64 AND doesn't look like a regular string
        // Avoid treating error messages or other plain text as encrypted
        if (!IsBase64(trimmed))
            return false;
        
        // Additional heuristic: base64 strings are typically quite long (encrypted data)
        // Short strings are likely not encryption
        if (trimmed.Length < 20)
            return false;

        System.Diagnostics.Debug.WriteLine($"[ResponseParser] Payload detected as encrypted (base64, length={trimmed.Length})");
        return true;
    }

    private static bool IsBase64(string value)
    {
        try
        {
            // Try to decode - if it succeeds, it's likely base64
            Convert.FromBase64String(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private LoxoneMessage ParseJsonResponse(string jsonPayload)
    {
        // Parse the document and don't dispose it - pass to LoxoneMessage to manage
        var doc = JsonDocument.Parse(jsonPayload);
        var root = doc.RootElement;

        // If there's no LL property, treat the entire response as a value
        if (!root.TryGetProperty("LL", out var ll))
        {
            // Return with default success code and the whole object as value
            return new LoxoneMessage(200, root, null, doc);
        }

        // Parse code (can be string or int)
        int code = ParseCode(ll);
        
        // Get message if present
        var message = ll.TryGetProperty("message", out var m) ? m.GetString() : null;

        // Extract value if present
        JsonElement valueElement = default;
        JsonDocument? valueDoc = null;

        if (ll.TryGetProperty("value", out var v) && v.ValueKind != JsonValueKind.Undefined)
        {
            try
            {
                valueDoc = JsonDocument.Parse(v.GetRawText());
                valueElement = valueDoc.RootElement;
            }
            catch
            {
                // If value isn't valid JSON, keep it as-is
                valueElement = v;
                valueDoc = doc; // Keep the original doc alive for this element
            }
        }

        // If we didn't create a separate value doc, pass the main doc so elements stay alive
        if (valueDoc == null)
        {
            valueDoc = doc;
        }

        return new LoxoneMessage(code, valueElement, message, valueDoc);
    }

    private LoxoneMessage ParseEncryptedResponse(string encryptedPayload)
    {
        if (_encryption == null)
        {
            throw new InvalidOperationException(
                "Cannot decrypt response - encryption not initialized. " +
                "Response appears to be encrypted: " + encryptedPayload.Substring(0, Math.Min(50, encryptedPayload.Length)));
        }

        // Check if encryption keys are actually initialized
        // During keyexchange, encryption object exists but keys aren't set yet
        // In that case, just return the response as-is (it's the keyexchange response)
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ResponseParser] Attempting to decrypt response: {encryptedPayload.Substring(0, Math.Min(50, encryptedPayload.Length))}...");
            System.Diagnostics.Debug.WriteLine($"[ResponseParser] Encrypted payload length: {encryptedPayload.Length}");
            
            var decrypted = _encryption.DecryptResponse(encryptedPayload);
            System.Diagnostics.Debug.WriteLine($"[ResponseParser] Successfully decrypted: {decrypted.Substring(0, Math.Min(100, decrypted.Length))}...");

            // Try to parse as JSON
            if (decrypted.StartsWith("{"))
            {
                return ParseJsonResponse(decrypted);
            }

            // If it's a plain value, wrap it as success response
            var doc = JsonDocument.Parse("\"" + decrypted + "\"");
            return new LoxoneMessage(200, doc.RootElement, null, doc);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Keyexchange not yet performed"))
        {
            // Encryption not yet initialized - this is likely the keyexchange response itself
            // Return it as a plain value (base64 string)
            System.Diagnostics.Debug.WriteLine($"[ResponseParser] Encryption not yet initialized, treating response as plain value (likely keyexchange response)");
            var doc = JsonDocument.Parse("\"" + encryptedPayload + "\"");
            return new LoxoneMessage(200, doc.RootElement, null, doc);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("AES decryption failed"))
        {
            // Decryption failed - likely wrong key/IV or the response isn't actually encrypted
            System.Diagnostics.Debug.WriteLine($"[ResponseParser] AES decryption failed: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ResponseParser] This could mean: 1) Wrong key/IV, 2) Response isn't encrypted, or 3) Data corrupted");
            
            // Try to parse as plain JSON in case it's just a regular JSON response
            if (encryptedPayload.TrimStart().StartsWith("{"))
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[ResponseParser] Attempting to parse as plain JSON...");
                    return ParseJsonResponse(encryptedPayload);
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine($"[ResponseParser] Plain JSON parse failed, treating as plain value");
                }
            }
            
            // If all else fails, return as plain value with error code
            System.Diagnostics.Debug.WriteLine($"[ResponseParser] Returning encrypted payload as plain value");
            var doc = JsonDocument.Parse("\"" + encryptedPayload + "\"");
            return new LoxoneMessage(200, doc.RootElement, null, doc);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ResponseParser] Failed to decrypt: {ex.GetType().Name}: {ex.Message}");
            
            // Last resort: return the raw payload as a value
            System.Diagnostics.Debug.WriteLine($"[ResponseParser] Returning as plain value after exception");
            try
            {
                var doc = JsonDocument.Parse("\"" + encryptedPayload + "\"");
                return new LoxoneMessage(200, doc.RootElement, null, doc);
            }
            catch
            {
                // Even that failed, throw the original error
                throw new InvalidOperationException(
                    $"Failed to decrypt response and couldn't parse as fallback: {ex.Message}", ex);
            }
        }
    }

    private static int ParseCode(JsonElement ll)
    {
        if (!ll.TryGetProperty("Code", out var codeElement))
        {
            return 0;
        }

        return codeElement.ValueKind == JsonValueKind.String
            ? int.Parse(codeElement.GetString() ?? "0")
            : codeElement.GetInt32();
    }

    private static string NormalizePayload(string payload)
    {
        // Trim leading/trailing whitespace and control characters
        var startIndex = 0;
        while (startIndex < payload.Length && 
               (char.IsWhiteSpace(payload[startIndex]) || char.IsControl(payload[startIndex])))
        {
            startIndex++;
        }

        var endIndex = payload.Length - 1;
        while (endIndex > startIndex && 
               (char.IsWhiteSpace(payload[endIndex]) || char.IsControl(payload[endIndex])))
        {
            endIndex--;
        }

        return payload[startIndex..(endIndex + 1)];
    }
}
