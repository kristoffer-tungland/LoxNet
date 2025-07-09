namespace LoxNet.Users;

using System.Text.Json.Serialization;

/// <summary>
/// Represents another Miniserver available for trust user management.
/// </summary>
public record TrustPeer(
    [property: JsonPropertyName("serial")] string Serial,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("intAddr")] string IntAddr,
    [property: JsonPropertyName("extAddr")] string ExtAddr);
