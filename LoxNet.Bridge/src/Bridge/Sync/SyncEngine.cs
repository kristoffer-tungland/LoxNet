using LoxNet.Bridge.Config;
using LoxNet.Bridge.Loxone;
using LoxNet.Bridge.Mqtt;
using Microsoft.Extensions.Logging;

namespace LoxNet.Bridge.Sync;

public class SyncEngine
{
    private readonly BridgeConfig _config;
    private readonly StateCache _cache;
    private readonly StateComparer _comparer;
    private readonly LoxoneCommandBuilder _commandBuilder;
    private readonly ILoxoneCommandExecutor _loxoneService;
    private readonly Z2mPublisher _publisher;
    private readonly IMqttPublisher _mqttPublisher;
    private readonly ILogger<SyncEngine> _logger;

    public SyncEngine(BridgeConfig config, StateCache cache, StateComparer comparer, LoxoneCommandBuilder commandBuilder, ILoxoneCommandExecutor loxoneService, Z2mPublisher publisher, IMqttPublisher mqttPublisher, ILogger<SyncEngine> logger)
    {
        _config = config;
        _cache = cache;
        _comparer = comparer;
        _commandBuilder = commandBuilder;
        _loxoneService = loxoneService;
        _publisher = publisher;
        _mqttPublisher = mqttPublisher;
        _logger = logger;
    }

    public Task ProcessMqttAsync(string topic, NormalizedLightState state, CancellationToken cancellationToken)
    {
        var mapping = _config.Mappings.FirstOrDefault(m => string.Equals(m.MqttTopic, topic, StringComparison.OrdinalIgnoreCase));
        if (mapping is null)
        {
            _logger.LogDebug("Ignoring MQTT topic {Topic} without mapping", topic);
            return Task.CompletedTask;
        }

        var cache = _cache.GetOrCreate(mapping);
        cache.LastObservedMqtt = state;

        if (_comparer.AreEqual(state, cache.LastSentToLoxone))
        {
            _logger.LogDebug("Ignoring MQTT echo for {Topic}", topic);
            return Task.CompletedTask;
        }

        var commands = _commandBuilder.BuildCommands(mapping, state);
        return ForwardToLoxoneAsync(mapping, cache, commands, state, cancellationToken);
    }

    public Task ProcessLoxoneAsync(string uuidAction, NormalizedLightState state, CancellationToken cancellationToken)
    {
        var mapping = _config.Mappings.FirstOrDefault(m => string.Equals(m.LoxoneUuidAction, uuidAction, StringComparison.OrdinalIgnoreCase));
        if (mapping is null)
        {
            _logger.LogDebug("Ignoring Loxone state for unmapped uuidAction {Uuid}", uuidAction);
            return Task.CompletedTask;
        }

        var cache = _cache.GetOrCreate(mapping);
        cache.LastObservedLoxone = state;

        if (_comparer.AreEqual(state, cache.LastSentToMqtt))
        {
            _logger.LogDebug("Ignoring Loxone echo for {Uuid}", uuidAction);
            return Task.CompletedTask;
        }

        return ForwardToMqttAsync(mapping, cache, state, cancellationToken);
    }

    private async Task ForwardToLoxoneAsync(MappingSection mapping, MappingState cache, IReadOnlyList<string> commands, NormalizedLightState state, CancellationToken cancellationToken)
    {
        if (commands.Count == 0)
        {
            _logger.LogDebug("No Loxone commands generated for {Name}", mapping.Name);
            return;
        }

        await _loxoneService.SendCommandsAsync(mapping, commands, cancellationToken).ConfigureAwait(false);
        cache.LastSentToLoxone = state;
    }

    private async Task ForwardToMqttAsync(MappingSection mapping, MappingState cache, NormalizedLightState state, CancellationToken cancellationToken)
    {
        if (!_mqttPublisher.IsConnected)
        {
            _logger.LogWarning("MQTT client is not connected; skipping publish for {Topic}", mapping.MqttTopic);
            return;
        }

        var payload = _publisher.BuildPayload(state, cache.LastSentToMqtt);
        if (string.Equals(payload, "{}", StringComparison.Ordinal))
        {
            _logger.LogDebug("MQTT payload empty for {Topic}", mapping.MqttTopic);
            return;
        }

        await _mqttPublisher.PublishAsync(mapping, state, cache.LastSentToMqtt, cancellationToken).ConfigureAwait(false);
        cache.LastSentToMqtt = state;
    }
}
