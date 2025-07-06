using System.Text.Json.Serialization;

namespace LoxNet;

/// <summary>
/// Additional properties parsed for a <see cref="LightController"/>.
/// </summary>
public class LightControllerDetails
{
    /// <summary>Minimum dimming value.</summary>
    [JsonPropertyName("minValue")]
    public int? MinValue { get; set; }

    /// <summary>Maximum dimming value.</summary>
    [JsonPropertyName("maxValue")]
    public int? MaxValue { get; set; }

    /// <summary>Indicates whether the controller supports white channel.</summary>
    [JsonPropertyName("hasWhite")]
    public bool? HasWhite { get; set; }
}
