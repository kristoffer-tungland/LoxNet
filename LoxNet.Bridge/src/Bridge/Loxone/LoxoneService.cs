using LoxNet.Bridge.Config;
using LoxNet.Bridge.Sync;
using Microsoft.Extensions.Logging;
using LoxNet;

namespace LoxNet.Bridge.Loxone;

public class LoxoneService : ILoxoneCommandExecutor
{
    private readonly ILogger<LoxoneService> _logger;
    private readonly ILogger<LoxoneClient> _loxoneClientLogger;
    private readonly ILogger<LoxoneStructureState> _structureLogger;
    private readonly LoxoneStateParser _stateParser;
    private LoxoneClient? _client;
    private LoxoneStructureState? _structure;
    private Func<string, NormalizedLightState, CancellationToken, Task>? _callback;

    public LoxoneService(ILogger<LoxoneService> logger, ILogger<LoxoneClient> loxoneClientLogger, ILogger<LoxoneStructureState> structureLogger, LoxoneStateParser stateParser)
    {
        _logger = logger;
        _loxoneClientLogger = loxoneClientLogger;
        _structureLogger = structureLogger;
        _stateParser = stateParser;
    }

    public LoxoneClient? Client => _client;

    public LoxoneStructureState? Structure => _structure;

    public async Task StartAsync(BridgeConfig config, Func<string, NormalizedLightState, CancellationToken, Task> callback, CancellationToken cancellationToken)
    {
        _callback = callback;
        var options = new LoxoneConnectionOptions(config.Loxone.Host, config.Loxone.Port, config.Loxone.UseHttps);
        _client = new LoxoneClient(_loxoneClientLogger, options);
        await _client.LoginAsync(config.Loxone.User, config.Loxone.Password, cancellationToken: cancellationToken).ConfigureAwait(false);
        _structure = new LoxoneStructureState(_structureLogger, _client.Http, options, wsClient: _client.WebSocket);
        await _structure.LoadAsync(useCacheOnly: false, cancellationToken).ConfigureAwait(false);
        await SubscribeToMappingsAsync(config, cancellationToken).ConfigureAwait(false);
        _ = Task.Run(() => _client.WebSocket.ListenAsync(cancellationToken), cancellationToken);
        await _client.WebSocket.CommandAsync("jdev/sps/enablebinstatusupdate", cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client is not null)
        {
            await _client.WebSocket.CloseAsync(cancellationToken).ConfigureAwait(false);
            await _client.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task SendCommandsAsync(MappingSection mapping, IEnumerable<string> commands, CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            throw new InvalidOperationException("Loxone client not initialized");
        }

        foreach (var command in commands)
        {
            using var doc = await _client.Http.RequestJsonAsync($"jdev/sps/io/{mapping.LoxoneUuidAction}/{command}", cancellationToken).ConfigureAwait(false);
            var msg = LoxoneMessageParser.Parse(doc);
            msg.EnsureSuccess();
        }
    }

    private async Task SubscribeToMappingsAsync(BridgeConfig config, CancellationToken cancellationToken)
    {
        if (_structure is null)
        {
            return;
        }

        foreach (var mapping in config.Mappings)
        {
            if (!_structure.TryGetControl(mapping.LoxoneUuidAction, out var control) || control is null)
            {
                _logger.LogWarning("Mapping {Name} references unknown uuidAction {Uuid} — state changes will not be forwarded to MQTT", mapping.Name, mapping.LoxoneUuidAction);
                continue;
            }

            _logger.LogInformation("Subscribed to state changes for mapping {Name} ({Uuid})", mapping.Name, mapping.LoxoneUuidAction);

            control.StateChanged += (_, args) =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleStateAsync(mapping, args.State, args.Value, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected on shutdown.
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling Loxone state change for mapping {Name} (state={State})", mapping.Name, args.State);
                    }
                }, cancellationToken);
            };
        }
    }

    private Task HandleStateAsync(MappingSection mapping, string stateName, string value, CancellationToken cancellationToken)
    {
        if (_callback is null)
        {
            return Task.CompletedTask;
        }

        _logger.LogTrace("Loxone state change: {Name} state={State} value={Value}", mapping.Name, stateName, value);

        var normalized = mapping.Kind.ToLowerInvariant() switch
        {
            "dimmer" => _stateParser.FromDimmer(stateName, value),
            "colorpickerv2" => _stateParser.FromColorPicker(stateName, value),
            _ => NormalizedLightState.Empty
        };

        if (normalized == NormalizedLightState.Empty)
        {
            return Task.CompletedTask;
        }

        return _callback(mapping.LoxoneUuidAction, normalized, cancellationToken);
    }
}
