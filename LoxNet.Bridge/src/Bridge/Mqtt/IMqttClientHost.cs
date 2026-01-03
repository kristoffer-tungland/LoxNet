using MQTTnet.Client;

namespace LoxNet.Bridge.Mqtt;

public interface IMqttClientHost
{
    IMqttClient Client { get; }
}
