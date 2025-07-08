namespace LoxNet.Users;

using System.Text.Json.Serialization;

/// <summary>
/// Hashed keycode assigned to a user.
/// </summary>
public record Keycode(
    [property: JsonPropertyName("code")] string Code);
