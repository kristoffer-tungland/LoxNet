using System.Text.Json.Serialization;

namespace LoxNet;

/// <summary>
/// Additional detail properties for a <see cref="SwitchControl"/>.
/// </summary>
public class SwitchControlDetails
{
    /// <summary>
    /// Indicates whether this switch behaves as a stairwell light switch.
    /// </summary>
    [JsonPropertyName("isStairwayLs")]
    public bool? IsStairwayLs { get; set; }
}
