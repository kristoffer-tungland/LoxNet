namespace LoxNet.Users;

using System.Text.Json.Serialization;

/// <summary>
/// Request payload for editing an existing user. Inherits all fields from <see cref="AddUser"/> and adds the required UUID.
/// </summary>
public class EditUser : AddUser
{
    [JsonPropertyName("uuid")]
    public required string Uuid { get; init; }
}
