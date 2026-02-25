using LoxNet;
using LoxNet.Bridge.Config;
using LoxNet.Bridge.Loxone;
using LoxNet.Bridge.Mqtt;
using Microsoft.Extensions.Logging;

namespace LoxNet.Bridge.Sync;

public class SyncEngine
{
    private readonly ConfigStore _configStore;
    private readonly StateCache _cache;
    private readonly StateComparer _comparer;
    private readonly LoxoneCommandBuilder _commandBuilder;
    private readonly ILoxoneCommandExecutor _loxoneService;
    private readonly Z2mPublisher _publisher;
    private readonly IMqttPublisher _mqttPublisher;
    private readonly ILogger<SyncEngine> _logger;

    public SyncEngine(ConfigStore configStore, StateCache cache, StateComparer comparer, LoxoneCommandBuilder commandBuilder, ILoxoneCommandExecutor loxoneService, Z2mPublisher publisher, IMqttPublisher mqttPublisher, ILogger<SyncEngine> logger)
    {
        _configStore = configStore;
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
        var mapping = _configStore.Current.Mappings.FirstOrDefault(m => string.Equals(m.MqttTopic, topic, StringComparison.OrdinalIgnoreCase));
        if (mapping is null)
        {
            _logger.LogDebug("Ignoring MQTT topic {Topic} without mapping", topic);
            return Task.CompletedTask;
        }

        var cache = _cache.GetOrCreate(mapping);
        cache.LastObservedMqtt = state;

        if (_comparer.AreEqual(state, cache.LastSentToLoxone))
        {
            _logger.LogDebug("[{Name}] MQTT echo suppressed — state unchanged {State}", mapping.Name, FormatState(state));
            return Task.CompletedTask;
        }

        _logger.LogInformation("[{Name}] MQTT → Loxone  {Prev} → {Next}",
            mapping.Name, FormatState(cache.LastSentToLoxone), FormatState(state));

        if (!_loxoneService.TryGetControlType(mapping.LoxoneUuidAction, out var controlType))
        {
            _logger.LogWarning("Cannot build commands for {Name}: control type unknown", mapping.Name);
            return Task.CompletedTask;
        }
        var commands = _commandBuilder.BuildCommands(controlType, state);
        return ForwardToLoxoneAsync(mapping, cache, commands, state, cancellationToken);
    }

    public Task ProcessLoxoneAsync(string uuidAction, NormalizedLightState state, CancellationToken cancellationToken)
    {
        var mapping = _configStore.Current.Mappings.FirstOrDefault(m => string.Equals(m.LoxoneUuidAction, uuidAction, StringComparison.OrdinalIgnoreCase));
        if (mapping is null)
        {
            _logger.LogDebug("Ignoring Loxone state for unmapped uuidAction {Uuid}", uuidAction);
            return Task.CompletedTask;
        }

        var cache = _cache.GetOrCreate(mapping);
        cache.LastObservedLoxone = state;

        if (_comparer.AreEqual(state, cache.LastSentToMqtt))
        {
            _logger.LogDebug("[{Name}] Loxone echo suppressed — state unchanged {State}", mapping.Name, FormatState(state));
            return Task.CompletedTask;
        }

        _logger.LogInformation("[{Name}] Loxone → MQTT  {Prev} → {Next}",
            mapping.Name, FormatState(cache.LastSentToMqtt), FormatState(state));

        return ForwardToMqttAsync(mapping, cache, state, cancellationToken);
    }

    private async Task ForwardToLoxoneAsync(MappingSection mapping, MappingState cache, IReadOnlyList<string> commands, NormalizedLightState state, CancellationToken cancellationToken)
    {
        if (commands.Count == 0)
        {
            _logger.LogDebug("[{Name}] No Loxone commands generated", mapping.Name);
            return;
        }

        _logger.LogDebug("[{Name}] Sending {Count} Loxone command(s): {Commands}",
            mapping.Name, commands.Count, string.Join(", ", commands));

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
            _logger.LogDebug("[{Name}] MQTT payload empty — nothing to publish", mapping.Name);
            return;
        }

        _logger.LogDebug("[{Name}] Publishing MQTT payload: {Payload}", mapping.Name, payload);

        await _mqttPublisher.PublishAsync(mapping, state, cache.LastSentToMqtt, cancellationToken).ConfigureAwait(false);
        cache.LastSentToMqtt = state;
    }

    private static string FormatState(NormalizedLightState? s)
    {
        if (s is null) return "null";
        var parts = new List<string>();
        if (s.Power.HasValue)       parts.Add($"power={s.Power.Value}");
        if (s.BrightnessPct.HasValue) parts.Add($"bri={s.BrightnessPct.Value}%");
        if (s.Kelvin.HasValue)      parts.Add($"kelvin={s.Kelvin.Value}K");
        if (s.Hsv.HasValue)         parts.Add($"hsv=({s.Hsv.Value.H},{s.Hsv.Value.S},{s.Hsv.Value.V})");
        return parts.Count > 0 ? string.Join(" ", parts) : "empty";
    }
}
