namespace LoxNet.Users;

using System.Text.Json.Serialization;

/// <summary>
/// Basic information about a user as returned by getuserlist2.
/// </summary>
public record UserSummary(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("uuid")] string Uuid,
    [property: JsonPropertyName("isAdmin")] bool IsAdmin,
    [property: JsonPropertyName("userState")] UserState UserState,
    [property: JsonPropertyName("expirationAction")] ExpirationAction? ExpirationAction);
