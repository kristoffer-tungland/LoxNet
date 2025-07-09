namespace LoxNet.Users;

using System.Text.Json.Serialization;

/// <summary>
/// Minimal user information returned when looking up a user ID.
/// </summary>
public record UserLookup(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("uuid")] string Uuid);
