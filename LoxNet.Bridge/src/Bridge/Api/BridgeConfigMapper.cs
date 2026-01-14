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
                Password = config.Loxone.Password,
                Loxapp3Path = config.Loxone.Loxapp3Path,
                RefreshLoxapp3OnStart = config.Loxone.RefreshLoxapp3OnStart
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
                    Kind = m.Kind,
                    MqttTopic = m.MqttTopic,
                    LoxoneUuidAction = m.LoxoneUuidAction,
                    Options = new MappingOptionsDto
                    {
                        PreferMqttState = m.Options.PreferMqttState,
                        AllowColor = m.Options.AllowColor
                    }
                })
                .ToArray()
        };
    }

    public static BridgeConfig ToConfig(BridgeConfigDto dto, SyncSection syncSection)
    {
        return new BridgeConfig
        {
            Loxone = new LoxoneSection
            {
                Host = dto.Loxone.Host,
                Port = dto.Loxone.Port,
                UseHttps = dto.Loxone.UseHttps,
                User = dto.Loxone.User,
                Password = dto.Loxone.Password,
                Loxapp3Path = dto.Loxone.Loxapp3Path,
                RefreshLoxapp3OnStart = dto.Loxone.RefreshLoxapp3OnStart
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
                BrightnessTolerancePct = syncSection.BrightnessTolerancePct,
                KelvinTolerance = syncSection.KelvinTolerance,
                SuppressEchoWindowMs = syncSection.SuppressEchoWindowMs
            },
            Mappings = dto.Mappings
                .Select(m => new MappingSection
                {
                    Name = m.Name,
                    Kind = m.Kind,
                    MqttTopic = m.MqttTopic,
                    LoxoneUuidAction = m.LoxoneUuidAction,
                    Options = new MappingOptions
                    {
                        PreferMqttState = m.Options.PreferMqttState,
                        AllowColor = m.Options.AllowColor
                    }
                })
                .ToArray()
        };
    }
}
