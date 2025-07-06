using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoxNet;

/// <summary>
/// Represents the root structure of a <c>LoxApp3.json</c> file.
/// </summary>
internal class StructureFileDto
{
    /// <summary>Dictionary of controls keyed by UUID.</summary>
    [JsonPropertyName("controls")]
    public Dictionary<string, ControlDto>? Controls { get; set; }

    /// <summary>Dictionary of rooms keyed by identifier.</summary>
    [JsonPropertyName("rooms")]
    public Dictionary<string, RoomDto>? Rooms { get; set; }

    /// <summary>Dictionary of categories keyed by identifier.</summary>
    [JsonPropertyName("cats")]
    public Dictionary<string, CategoryDto>? Categories { get; set; }
}

/// <summary>
/// Model for a control entry from the structure file.
/// </summary>
internal class ControlDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("room")]
    public string? Room { get; set; }

    [JsonPropertyName("cat")]
    public string? Category { get; set; }

    [JsonPropertyName("uuidAction")]
    public string? UuidAction { get; set; }

    [JsonPropertyName("defaultRating")]
    public int? DefaultRating { get; set; }

    [JsonPropertyName("isSecured")]
    public bool? IsSecured { get; set; }

    [JsonPropertyName("securedDetails")]
    public bool? SecuredDetails { get; set; }

    [JsonPropertyName("states")]
    public Dictionary<string, string>? States { get; set; }

    [JsonPropertyName("details")]
    public JsonElement? Details { get; set; }

    [JsonPropertyName("statistic")]
    public JsonElement? Statistic { get; set; }

    [JsonPropertyName("restrictions")]
    public int? Restrictions { get; set; }

    [JsonPropertyName("hasControlNotes")]
    public bool? HasControlNotes { get; set; }

    [JsonPropertyName("preset")]
    public PresetDto? Preset { get; set; }

    [JsonPropertyName("links")]
    public List<string>? Links { get; set; }

    [JsonPropertyName("subControls")]
    public Dictionary<string, ControlDto>? SubControls { get; set; }
}

internal record PresetDto(
    [property: JsonPropertyName("uuid")] string Uuid,
    [property: JsonPropertyName("name")] string? Name);

/// <summary>
/// Model for a room entry from the structure file.
/// </summary>
internal class RoomDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("defaultRating")]
    public int? DefaultRating { get; set; }
}

/// <summary>
/// Model for a category entry from the structure file.
/// </summary>
internal class CategoryDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }
}
