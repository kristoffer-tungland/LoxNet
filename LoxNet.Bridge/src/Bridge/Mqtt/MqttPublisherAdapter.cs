using LoxNet.Bridge.Config;
using LoxNet.Bridge.Sync;
using MQTTnet.Client;

namespace LoxNet.Bridge.Mqtt;

public class MqttPublisherAdapter : IMqttPublisher
{
    private readonly IMqttClientHost _host;
    private readonly Z2mPublisher _publisher;

    public MqttPublisherAdapter(IMqttClientHost host, Z2mPublisher publisher)
    {
        _host = host;
        _publisher = publisher;
    }

    public bool IsConnected => _host.Client.IsConnected;

    public Task PublishAsync(MappingSection mapping, NormalizedLightState desired, NormalizedLightState lastSent, CancellationToken cancellationToken)
    {
        return _publisher.PublishAsync(_host.Client, mapping, desired, lastSent, cancellationToken);
    }
}
