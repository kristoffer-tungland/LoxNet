namespace LoxNet.Users;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a user group as returned by getgrouplist.
/// </summary>
public record UserGroup(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("uuid")] string Uuid,
    [property: JsonPropertyName("type")] UserGroupType Type,
    [property: JsonPropertyName("userRights")] UserRights UserRights);
