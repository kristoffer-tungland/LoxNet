using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
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

    /// <summary>
    /// Subscribes to the Home Assistant MQTT discovery topic (<c>homeassistant/light/#</c>),
    /// collects all light config payloads for the specified timeout, then unsubscribes and
    /// returns the results.
    /// </summary>
    /// <param name="timeout">How long to wait for discovery messages. Defaults to 2 seconds.</param>
    /// <param name="cancellationToken">Cancellation token for the overall operation.</param>
    public async Task<IReadOnlyList<HaLightConfigDto>> DiscoverHaLightsAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected)
        {
            _logger.LogWarning("MQTT client is not connected; cannot perform HA light discovery.");
            return [];
        }

        const string discoveryTopic = "homeassistant/light/#";
        var results = new ConcurrentDictionary<string, HaLightConfigDto>(StringComparer.OrdinalIgnoreCase);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(2));

        Func<MqttApplicationMessageReceivedEventArgs, Task> handler = args =>
        {
            var topic = args.ApplicationMessage.Topic;
            if (!topic.EndsWith("/config", StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            try
            {
                var json = Encoding.UTF8.GetString(args.ApplicationMessage.Payload.ToArray());
                var config = JsonSerializer.Deserialize<HaLightConfigDto>(json);
                if (config is not null)
                {
                    results[topic] = config;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse HA discovery payload on {Topic}", topic);
            }

            return Task.CompletedTask;
        };

        _client.ApplicationMessageReceivedAsync += handler;

        var subscribeOptions = new MqttClientSubscribeOptions
        {
            TopicFilters = [new MqttTopicFilterBuilder().WithTopic(discoveryTopic).Build()]
        };

        await _client.SubscribeAsync(subscribeOptions, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Subscribed to {Topic} for HA light discovery", discoveryTopic);

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected – either the timeout fired or the caller cancelled.
        }
        finally
        {
            _client.ApplicationMessageReceivedAsync -= handler;

            if (_client.IsConnected)
            {
                var unsubscribeOptions = new MqttClientUnsubscribeOptions
                {
                    TopicFilters = [discoveryTopic]
                };
                await _client.UnsubscribeAsync(unsubscribeOptions, CancellationToken.None).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("HA light discovery complete – found {Count} device(s)", results.Count);
        return results.Values.ToList();
    }

    private Task SubscribeTopicsAsync(BridgeConfig config, CancellationToken cancellationToken)
    {
        var topics = config.Mappings.Select(m => m.MqttTopic).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var options = topics.Select(t => new MqttTopicFilterBuilder().WithTopic(t).Build()).ToList();
        if (options.Count == 0)
        {
            _logger.LogInformation("No MQTT topics configured; skipping subscribe.");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Subscribing to {Count} MQTT topics", options.Count);
        var subscribeOptions = new MqttClientSubscribeOptions { TopicFilters = options };
        return _client.SubscribeAsync(subscribeOptions, cancellationToken);
    }
}
