using LoxNet.Bridge.Config;
using LoxNet.Bridge.Loxone;
using LoxNet.Bridge.Mqtt;
using LoxNet.Bridge.Sync;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoxNet.Bridge;

public class AppHost : IHostedService
{
    private readonly BridgeConfig _config;
    private readonly MqttService _mqttService;
    private readonly LoxoneService _loxoneService;
    private readonly SyncEngine _syncEngine;
    private readonly ILogger<AppHost> _logger;

    public AppHost(BridgeConfig config, MqttService mqttService, LoxoneService loxoneService, SyncEngine syncEngine, ILogger<AppHost> logger)
    {
        _config = config;
        _mqttService = mqttService;
        _loxoneService = loxoneService;
        _syncEngine = syncEngine;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting bridge with {MappingCount} mappings", _config.Mappings.Count);

        await _mqttService.StartAsync(_config, HandleMqttMessageAsync, cancellationToken).ConfigureAwait(false);
        await _loxoneService.StartAsync(_config, HandleLoxoneStateAsync, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _mqttService.StopAsync(cancellationToken).ConfigureAwait(false);
        await _loxoneService.StopAsync(cancellationToken).ConfigureAwait(false);
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
