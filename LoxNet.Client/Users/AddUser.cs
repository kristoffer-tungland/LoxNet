namespace LoxNet.Users;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Request payload for creating or editing a user when no UUID is required.
/// Null properties are omitted from the serialized JSON.
/// </summary>
public class AddUser
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("desc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonPropertyName("userid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserId { get; init; }

    [JsonPropertyName("firstname")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FirstName { get; init; }

    [JsonPropertyName("lastname")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastName { get; init; }

    [JsonPropertyName("email")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; init; }

    [JsonPropertyName("phone")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Phone { get; init; }

    [JsonPropertyName("uniqueUserId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UniqueUserId { get; init; }

    [JsonPropertyName("company")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Company { get; init; }

    [JsonPropertyName("department")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Department { get; init; }

    [JsonPropertyName("personalno")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PersonalNo { get; init; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    [JsonPropertyName("debitor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Debitor { get; init; }

    [JsonPropertyName("userState")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UserState? UserState { get; init; }

    [JsonPropertyName("isAdmin")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsAdmin { get; init; }

    [JsonPropertyName("changePassword")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ChangePassword { get; init; }

    [JsonPropertyName("masterAdmin")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? MasterAdmin { get; init; }

    [JsonPropertyName("userRights")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UserRights? UserRights { get; init; }

    [JsonPropertyName("scorePWD")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ScorePwd { get; init; }

    [JsonPropertyName("scoreVisuPWD")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ScoreVisuPwd { get; init; }

    [JsonPropertyName("trustMember")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TrustMember { get; init; }

    [JsonPropertyName("disabledBySource")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DisabledBySource { get; init; }

    [JsonPropertyName("validUntil")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(NullableUnixDateTimeConverter))]
    public DateTime? ValidUntil { get; init; }

    [JsonPropertyName("validFrom")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(NullableUnixDateTimeConverter))]
    public DateTime? ValidFrom { get; init; }

    [JsonPropertyName("expirationAction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpirationAction? ExpirationAction { get; init; }

    [JsonPropertyName("usergroups")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? UserGroups { get; init; }

    [JsonPropertyName("nfcTags")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? NfcTags { get; init; }

    [JsonPropertyName("keycodes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Keycodes { get; init; }

    /// <summary>
    /// Custom user field values ordered according to their index.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string?> CustomFields
        => Extra
            .Where(p => p.Key.StartsWith("customField", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Key)
            .Select(p => p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.GetString())
            .ToArray();

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extra { get; init; } = new();
}
