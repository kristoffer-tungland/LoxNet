using System.Buffers;
using LoxNet.Bridge.Config;
using LoxNet.Bridge.Sync;
using Microsoft.Extensions.Logging;
using MQTTnet;

namespace LoxNet.Bridge.Mqtt;

public class MqttService : IMqttClientHost
{
    private readonly ILogger<MqttService> _logger;
    private readonly Z2mMessageParser _parser;
    private readonly IMqttClient _client;

    public MqttService(ILogger<MqttService> logger, Z2mMessageParser parser, IMqttClient? client = null)
    {
        _logger = logger;
        _parser = parser;
        _client = client ?? new MqttClientFactory().CreateMqttClient();
    }

    public IMqttClient Client => _client;

    public async Task StartAsync(BridgeConfig config, Func<string, NormalizedLightState, CancellationToken, Task> handler, CancellationToken cancellationToken)
    {
        var mqttOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(config.Mqtt.Host, config.Mqtt.Port)
            .WithClientId(config.Mqtt.ClientId)
            .WithCredentials(config.Mqtt.Username, config.Mqtt.Password)
            .Build();

        _client.ApplicationMessageReceivedAsync += args =>
        {
            var topic = args.ApplicationMessage.Topic;
            try
            {
                var payload = args.ApplicationMessage.Payload.ToArray();
                var json = System.Text.Encoding.UTF8.GetString(payload);
                var state = _parser.Parse(json);
                return handler(topic, state, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process MQTT message on {Topic}", topic);
                return Task.CompletedTask;
            }
        };

        _client.ConnectedAsync += _ =>
        {
            _logger.LogInformation("Connected to MQTT at {Host}:{Port}", config.Mqtt.Host, config.Mqtt.Port);
            return SubscribeTopicsAsync(config, cancellationToken);
        };

        await _client.ConnectAsync(mqttOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private Task SubscribeTopicsAsync(BridgeConfig config, CancellationToken cancellationToken)
    {
        var topics = config.Mappings.Select(m => m.MqttTopic).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var options = topics.Select(t => new MqttTopicFilterBuilder().WithTopic(t).Build()).ToList();
        _logger.LogInformation("Subscribing to {Count} MQTT topics", options.Count);
        var subscribeOptions = new MqttClientSubscribeOptions { TopicFilters = options };
        return _client.SubscribeAsync(subscribeOptions, cancellationToken);
    }
}
