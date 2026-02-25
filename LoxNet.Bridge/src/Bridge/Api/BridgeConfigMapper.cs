using LoxNet.Bridge.Config;

namespace LoxNet.Bridge.Api;

public static class BridgeConfigMapper
{
    public static BridgeConfigDto ToDto(BridgeConfig config)
    {
        return new BridgeConfigDto
        {
            Loxone = new LoxoneSectionDto
            {
                Host = config.Loxone.Host,
                Port = config.Loxone.Port,
                UseHttps = config.Loxone.UseHttps,
                User = config.Loxone.User,
                Password = config.Loxone.Password
            },
            Mqtt = new MqttSectionDto
            {
                Host = config.Mqtt.Host,
                Port = config.Mqtt.Port,
                Username = config.Mqtt.Username,
                Password = config.Mqtt.Password,
                ClientId = config.Mqtt.ClientId,
                BaseTopic = config.Mqtt.BaseTopic
            },
            Mappings = config.Mappings
                .Select(m => new MappingSectionDto
                {
                    Name = m.Name,
                    MqttTopic = m.MqttTopic,
                    LoxoneUuidAction = m.LoxoneUuidAction
                })
                .ToArray()
        };
    }

    /// <summary>
    /// Builds a new <see cref="BridgeConfig"/> from <paramref name="dto"/>, preserving every
    /// section that is not exposed in the UI (e.g. <c>Sync</c>, <c>Logging</c>) from
    /// <paramref name="current"/> so they are never silently reset.
    /// </summary>
    public static BridgeConfig ToConfig(BridgeConfigDto dto, BridgeConfig current)
    {
        return new BridgeConfig
        {
            Loxone = new LoxoneSection
            {
                Host = dto.Loxone.Host,
                Port = dto.Loxone.Port,
                UseHttps = dto.Loxone.UseHttps,
                User = dto.Loxone.User,
                Password = dto.Loxone.Password
            },
            Mqtt = new MqttSection
            {
                Host = dto.Mqtt.Host,
                Port = dto.Mqtt.Port,
                Username = dto.Mqtt.Username,
                Password = dto.Mqtt.Password,
                ClientId = dto.Mqtt.ClientId,
                BaseTopic = dto.Mqtt.BaseTopic
            },
            Sync = new SyncSection
            {
                BrightnessTolerancePct = current.Sync.BrightnessTolerancePct,
                KelvinTolerance = current.Sync.KelvinTolerance,
                SuppressEchoWindowMs = current.Sync.SuppressEchoWindowMs
            },
            Logging = new LoggingSection
            {
                MinLevel = current.Logging.MinLevel
            },
            Mappings = dto.Mappings
                .Select(m => new MappingSection
                {
                    Name = m.Name,
                    MqttTopic = m.MqttTopic,
                    LoxoneUuidAction = m.LoxoneUuidAction
                })
                .ToList()
        };
    }
}
