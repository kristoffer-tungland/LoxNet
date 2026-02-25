using LoxNet.Bridge.Config;
using LoxNet.Bridge.Loxone;
using LoxNet.Bridge.Mqtt;
using LoxNet.Bridge.Sync;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoxNet.Bridge;

public class AppHost : IHostedService
{
    private readonly ConfigStore _configStore;
    private readonly MqttService _mqttService;
    private readonly LoxoneService _loxoneService;
    private readonly SyncEngine _syncEngine;
    private readonly ILogger<AppHost> _logger;

    public AppHost(ConfigStore configStore, MqttService mqttService, LoxoneService loxoneService, SyncEngine syncEngine, ILogger<AppHost> logger)
    {
        _configStore = configStore;
        _mqttService = mqttService;
        _loxoneService = loxoneService;
        _syncEngine = syncEngine;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting bridge with {MappingCount} mappings", _configStore.Current.Mappings.Count);

        await _mqttService.StartAsync(_configStore.Current, HandleMqttMessageAsync, cancellationToken).ConfigureAwait(false);
        await _loxoneService.StartAsync(_configStore.Current, HandleLoxoneStateAsync, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _mqttService.StopAsync(cancellationToken).ConfigureAwait(false);
        await _loxoneService.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Persists a new Loxone section to the YAML file, then reconnects the Loxone
    /// service with the real sync handler so state changes keep flowing.
    /// </summary>
    public async Task ReconnectLoxoneAsync(LoxoneSection newSection, CancellationToken cancellationToken)
    {
        var updated = BuildConfig(loxone: newSection);
        await _configStore.UpdateAndSaveAsync(updated).ConfigureAwait(false);
        await _loxoneService.StopAsync(cancellationToken).ConfigureAwait(false);
        await _loxoneService.StartAsync(updated, HandleLoxoneStateAsync, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Persists a new MQTT section to the YAML file, then reconnects the MQTT
    /// service with the real sync handler so state changes keep flowing.
    /// </summary>
    public async Task ReconnectMqttAsync(MqttSection newSection, CancellationToken cancellationToken)
    {
        var updated = BuildConfig(mqtt: newSection);
        await _configStore.UpdateAndSaveAsync(updated).ConfigureAwait(false);
        await _mqttService.StopAsync(cancellationToken).ConfigureAwait(false);
        await _mqttService.StartAsync(updated, HandleMqttMessageAsync, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Persists a new mapping list to the YAML file, then restarts both services so
    /// MQTT topic subscriptions and Loxone state watchers reflect the new mappings.
    /// </summary>
    public async Task ApplyMappingsAsync(List<MappingSection> newMappings, CancellationToken cancellationToken)
    {
        var updated = BuildConfig(mappings: newMappings);
        await _configStore.UpdateAndSaveAsync(updated).ConfigureAwait(false);
        await _mqttService.StopAsync(cancellationToken).ConfigureAwait(false);
        await _loxoneService.StopAsync(cancellationToken).ConfigureAwait(false);
        await _mqttService.StartAsync(updated, HandleMqttMessageAsync, cancellationToken).ConfigureAwait(false);
        await _loxoneService.StartAsync(updated, HandleLoxoneStateAsync, cancellationToken).ConfigureAwait(false);
    }

    // Builds a new BridgeConfig by swapping only the supplied sections; everything
    // else is copied verbatim from the current in-memory config so nothing is lost.
    private BridgeConfig BuildConfig(
        LoxoneSection? loxone = null,
        MqttSection? mqtt = null,
        List<MappingSection>? mappings = null)
    {
        var c = _configStore.Current;
        return new BridgeConfig
        {
            Loxone = loxone ?? c.Loxone,
            Mqtt = mqtt ?? c.Mqtt,
            Sync = c.Sync,
            Logging = c.Logging,
            Mappings = mappings ?? c.Mappings
        };
    }

    private Task HandleMqttMessageAsync(string topic, NormalizedLightState state, CancellationToken cancellationToken)
    {
        return _syncEngine.ProcessMqttAsync(topic, state, cancellationToken);
    }

    private Task HandleLoxoneStateAsync(string uuidAction, NormalizedLightState state, CancellationToken cancellationToken)
    {
        return _syncEngine.ProcessLoxoneAsync(uuidAction, state, cancellationToken);
    }
}
