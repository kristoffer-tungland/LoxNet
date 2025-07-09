namespace LoxNet.Users;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Result returned when discovering users on a peer.
/// </summary>
public record TrustDiscoveryResult(
    [property: JsonPropertyName("serial")] string Serial,
    [property: JsonPropertyName("users")] IReadOnlyList<TrustDiscoveredUser> Users);
