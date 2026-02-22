namespace LoxNet.Bridge.Api;

public sealed record MqttDeviceDto
{
    public required string FriendlyName { get; init; }
    public required string Topic { get; init; }
    public required string Model { get; init; }
    public required string[] SupportedFeatures { get; init; }
}

public sealed record MqttLightsDiscoveryResult
{
    public required IReadOnlyList<MqttDeviceDto> Lights { get; init; }
}
