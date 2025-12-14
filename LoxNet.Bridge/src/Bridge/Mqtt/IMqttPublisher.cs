using LoxNet.Bridge.Config;
using LoxNet.Bridge.Sync;

namespace LoxNet.Bridge.Mqtt;

public interface IMqttPublisher
{
    bool IsConnected { get; }

    Task PublishAsync(MappingSection mapping, NormalizedLightState desired, NormalizedLightState lastSent, CancellationToken cancellationToken);
}
