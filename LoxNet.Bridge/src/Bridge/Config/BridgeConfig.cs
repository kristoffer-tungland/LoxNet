using System.ComponentModel.DataAnnotations;

namespace LoxNet.Bridge.Config;

public class BridgeConfig
{
    public LoxoneSection Loxone { get; init; } = new();

    public MqttSection Mqtt { get; init; } = new();

    public SyncSection Sync { get; init; } = new();

    public List<MappingSection> Mappings { get; init; } = new();

    public void Validate()
    {
        Validator.ValidateObject(Loxone, new ValidationContext(Loxone), true);
        Validator.ValidateObject(Mqtt, new ValidationContext(Mqtt), true);
        Validator.ValidateObject(Sync, new ValidationContext(Sync), true);

        foreach (var mapping in Mappings)
        {
            Validator.ValidateObject(mapping, new ValidationContext(mapping), true);
        }

        var duplicateUuid = Mappings
            .GroupBy(m => m.LoxoneUuidAction, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(duplicateUuid))
        {
            throw new ValidationException($"Duplicate loxoneUuidAction detected: {duplicateUuid}");
        }

        var duplicateTopic = Mappings
            .GroupBy(m => m.MqttTopic, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(duplicateTopic))
        {
            throw new ValidationException($"Duplicate mqttTopic detected: {duplicateTopic}");
        }
    }
}

public class LoxoneSection
{
    [Required]
    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 80;

    public bool UseHttps { get; init; }

    [Required]
    public string User { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    public string? Loxapp3Path { get; init; }

    public bool RefreshLoxapp3OnStart { get; init; } = true;
}

public class MqttSection
{
    [Required]
    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 1883;

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string ClientId { get; init; } = "loxone-z2m-bridge";

    public string BaseTopic { get; init; } = "zigbee2mqtt";
}

public class SyncSection
{
    public int BrightnessTolerancePct { get; init; } = 1;

    public int KelvinTolerance { get; init; } = 25;

    public int SuppressEchoWindowMs { get; init; }
}

public class MappingSection
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string Kind { get; init; } = string.Empty;

    [Required]
    public string MqttTopic { get; init; } = string.Empty;

    [Required]
    public string LoxoneUuidAction { get; init; } = string.Empty;

    public MappingOptions Options { get; init; } = new();
}

public class MappingOptions
{
    public bool PreferMqttState { get; init; }

    public bool AllowColor { get; init; } = true;
}
