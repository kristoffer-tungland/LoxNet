using MQTTnet;

namespace LoxNet.Bridge.Mqtt;

public interface IMqttClientHost
{
    IMqttClient Client { get; }
}
