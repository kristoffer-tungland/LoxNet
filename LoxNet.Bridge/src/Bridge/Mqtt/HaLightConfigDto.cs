using System.Text.Json.Serialization;

namespace LoxNet.Bridge.Mqtt;

/// <summary>
/// Represents a Home Assistant MQTT discovery payload for a light device,
/// as published to <c>homeassistant/light/{id}/light/config</c>.
/// </summary>
public sealed record HaLightConfigDto
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("object_id")]
    public string? ObjectId { get; init; }

    [JsonPropertyName("state_topic")]
    public string? StateTopic { get; init; }

    [JsonPropertyName("command_topic")]
    public string? CommandTopic { get; init; }

    [JsonPropertyName("brightness")]
    public bool Brightness { get; init; }

    [JsonPropertyName("brightness_scale")]
    public int? BrightnessScale { get; init; }

    [JsonPropertyName("supported_color_modes")]
    public string[]? SupportedColorModes { get; init; }

    [JsonPropertyName("effect")]
    public bool Effect { get; init; }

    [JsonPropertyName("effect_list")]
    public string[]? EffectList { get; init; }

    [JsonPropertyName("min_mireds")]
    public int? MinMireds { get; init; }

    [JsonPropertyName("max_mireds")]
    public int? MaxMireds { get; init; }

    [JsonPropertyName("device")]
    public HaDeviceInfoDto? Device { get; init; }

    [JsonPropertyName("unique_id")]
    public string? UniqueId { get; init; }
}

/// <summary>
/// Device information embedded in the Home Assistant MQTT discovery payload.
/// </summary>
public sealed record HaDeviceInfoDto
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("model_id")]
    public string? ModelId { get; init; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; init; }

    [JsonPropertyName("sw_version")]
    public string? SwVersion { get; init; }

    [JsonPropertyName("hw_version")]
    public object? HwVersion { get; init; }
}
