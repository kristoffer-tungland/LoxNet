namespace LoxNet.Bridge.Api;

public sealed record BridgeConfigDto
{
    public required LoxoneSectionDto Loxone { get; init; }

    public required MqttSectionDto Mqtt { get; init; }

    public required SyncSectionDto Sync { get; init; }

    public required LoggingSectionDto Logging { get; init; }

    public required IReadOnlyList<MappingSectionDto> Mappings { get; init; }
}

public sealed record LoxoneSectionDto
{
    public required string Host { get; init; }

    public int Port { get; init; } = 80;

    public bool UseHttps { get; init; }

    public required string User { get; init; }

    public required string Password { get; init; }

}

public sealed record MqttSectionDto
{
    public required string Host { get; init; }

    public int Port { get; init; } = 1883;

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string ClientId { get; init; } = "loxone-z2m-bridge";

    public string BaseTopic { get; init; } = "zigbee2mqtt";
}

public sealed record MappingSectionDto
{
    public required string Name { get; init; }

    public required string MqttTopic { get; init; }

    public required string LoxoneUuidAction { get; init; }
}

public sealed record SyncSectionDto
{
    public int BrightnessTolerancePct { get; init; } = 1;

    public int KelvinTolerance { get; init; } = 25;

    public int SuppressEchoWindowMs { get; init; }
}

public sealed record LoggingSectionDto
{
    public required string MinLevel { get; init; }
}

public sealed record ConfigSaveResult
{
    public required string Message { get; init; }

    public required string Path { get; init; }

    public bool RequiresRestart { get; init; }
}
