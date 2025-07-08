namespace LoxNet.Users;

using System.Text.Json.Serialization;

/// <summary>
/// NFC tag assigned to a user.
/// </summary>
public record NfcTag(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("id")] string Id);
