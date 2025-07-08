namespace LoxNet.Users;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Detailed configuration of a user as returned by getuser.
/// Unknown properties are captured in <see cref="Extra"/>.
/// </summary>
public class UserDetails
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("desc")]
    public string? Description { get; init; }

    [JsonPropertyName("uuid")]
    public required string Uuid { get; init; }

    [JsonPropertyName("userid")]
    public string? UserId { get; init; }

    [JsonPropertyName("firstname")]
    public string? FirstName { get; init; }

    [JsonPropertyName("lastname")]
    public string? LastName { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    [JsonPropertyName("uniqueUserId")]
    public string? UniqueUserId { get; init; }

    [JsonPropertyName("company")]
    public string? Company { get; init; }

    [JsonPropertyName("department")]
    public string? Department { get; init; }

    [JsonPropertyName("personalno")]
    public string? PersonalNo { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("debitor")]
    public string? Debitor { get; init; }

    [JsonPropertyName("lastedit")]
    [JsonConverter(typeof(NullableUnixDateTimeConverter))]
    public DateTime? LastEdit { get; init; }

    [JsonPropertyName("userState")]
    public UserState UserState { get; init; }

    [JsonPropertyName("isAdmin")]
    public bool IsAdmin { get; init; }

    [JsonPropertyName("changePassword")]
    public bool? ChangePassword { get; init; }

    [JsonPropertyName("masterAdmin")]
    public bool? MasterAdmin { get; init; }

    [JsonPropertyName("userRights")]
    public UserRights? UserRights { get; init; }

    [JsonPropertyName("scorePWD")]
    public int? ScorePwd { get; init; }

    [JsonPropertyName("scoreVisuPWD")]
    public int? ScoreVisuPwd { get; init; }

    [JsonPropertyName("trustMember")]
    public string? TrustMember { get; init; }

    [JsonPropertyName("disabledBySource")]
    public bool? DisabledBySource { get; init; }

    [JsonPropertyName("validUntil")]
    [JsonConverter(typeof(NullableUnixDateTimeConverter))]
    public DateTime? ValidUntil { get; init; }

    [JsonPropertyName("validFrom")]
    [JsonConverter(typeof(NullableUnixDateTimeConverter))]
    public DateTime? ValidFrom { get; init; }

    [JsonPropertyName("expirationAction")]
    public ExpirationAction? ExpirationAction { get; init; }

    [JsonPropertyName("usergroups")]
    public IReadOnlyList<UserGroup>? UserGroups { get; init; }

    [JsonPropertyName("nfcTags")]
    public IReadOnlyList<NfcTag>? NfcTags { get; init; }

    [JsonPropertyName("keycodes")]
    public IReadOnlyList<Keycode>? Keycodes { get; init; }

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
