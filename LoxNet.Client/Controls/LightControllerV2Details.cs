using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LoxNet;

/// <summary>
/// Additional properties parsed for a <see cref="LightControllerV2"/>.
/// </summary>
public class LightControllerV2Details
{
    /// <summary>UUID of the master brightness control.</summary>
    [JsonPropertyName("masterValue")]
    public string? MasterValue { get; set; }

    /// <summary>UUID of the master RGB control.</summary>
    [JsonPropertyName("masterColor")]
    public string? MasterColor { get; set; }

    /// <summary>List of favorite mood identifiers.</summary>
    [JsonPropertyName("favoriteMoods")]
    public List<string>? FavoriteMoods { get; set; }

    /// <summary>List of additional mood identifiers.</summary>
    [JsonPropertyName("additionalMoods")]
    public List<string>? AdditionalMoods { get; set; }

    /// <summary>Mapping of light circuit UUIDs to their names.</summary>
    [JsonPropertyName("circuitNames")]
    public Dictionary<string, string>? CircuitNames { get; set; }

    /// <summary>Configuration for daylight mode.</summary>
    [JsonPropertyName("daylightConfig")]
    public DaylightConfig? DaylightConfig { get; set; }

    /// <summary>Presence configuration bitmask.</summary>
    [JsonPropertyName("presence")]
    public int? Presence { get; set; }
}

/// <summary>
/// Information about the daylight configuration of a light controller.
/// </summary>
public class DaylightConfig
{
    /// <summary>Start time in minutes since midnight.</summary>
    [JsonPropertyName("from")]
    public int? From { get; set; }

    /// <summary>End time in minutes since midnight.</summary>
    [JsonPropertyName("until")]
    public int? Until { get; set; }

    /// <summary>Time mode for daylight control.</summary>
    [JsonPropertyName("mode")]
    public int? Mode { get; set; }

    /// <summary>Lighting type per circuit.</summary>
    [JsonPropertyName("type")]
    public Dictionary<string, int>? Type { get; set; }
}
