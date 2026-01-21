using System.Text.Json;

namespace LoxNet;

/// <summary>
/// Provides helpers for parsing server responses into <see cref="LoxoneMessage"/> instances.
/// </summary>
public static class LoxoneMessageParser
{
    /// <summary>Parses a JSON document returned by the server.</summary>
    /// <param name="doc">The document to parse.</param>
    public static LoxoneMessage Parse(JsonDocument doc)
    {
        if (doc is null)
        {
            throw new ArgumentNullException(nameof(doc));
        }

        if (!doc.RootElement.TryGetProperty("LL", out var ll))
        {
            throw new JsonException($"Loxone response missing 'LL' property. Body: {doc.RootElement.GetRawText()}");
        }

        if (!ll.TryGetProperty("Code", out var codeElement) && !ll.TryGetProperty("code", out codeElement))
        {
            throw new JsonException($"Loxone response missing 'LL.Code' property. Body: {doc.RootElement.GetRawText()}");
        }

        var value = ll.TryGetProperty("value", out var v) ? v : default;
        string? message = ll.TryGetProperty("message", out var m) ? m.GetString() : null;
        var code = codeElement.ValueKind switch
        {
            JsonValueKind.Number => codeElement.GetInt32(),
            JsonValueKind.String when int.TryParse(codeElement.GetString(), out var parsed) => parsed,
            _ => throw new JsonException($"Loxone response 'LL.Code' is not numeric. Body: {doc.RootElement.GetRawText()}")
        };

        return new LoxoneMessage(code, value, message);
    }
}
