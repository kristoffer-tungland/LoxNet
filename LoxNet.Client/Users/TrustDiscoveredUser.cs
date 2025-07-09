namespace LoxNet.Users;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a user discovered on a peer during trust operations.
/// </summary>
public record TrustDiscoveredUser(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("uuid")] string Uuid,
    [property: JsonPropertyName("used")] bool Used,
    [property: JsonPropertyName("locked")] bool? Locked);
