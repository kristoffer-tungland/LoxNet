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
        var ll = doc.RootElement.GetProperty("LL");
        JsonElement? value = ll.TryGetProperty("value", out var v) ? v : (JsonElement?)null;
        string? message = ll.TryGetProperty("message", out var m) ? m.GetString() : null;
        return new LoxoneMessage(ll.GetProperty("Code").GetInt32(), value, message);
    }
}

