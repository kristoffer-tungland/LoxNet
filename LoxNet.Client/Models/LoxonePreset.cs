namespace LoxNet;

using System.Text.Json.Serialization;

/// <summary>
/// Preset information linked to a control.
/// </summary>
public record LoxonePreset(
    [property: JsonPropertyName("uuid")] string Uuid,
    [property: JsonPropertyName("name")] string? Name);
