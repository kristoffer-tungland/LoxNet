using LoxNet.Bridge.Config;
using LoxNet.Bridge.Sync;
using Microsoft.Extensions.Logging;
using LoxNet;

namespace LoxNet.Bridge.Loxone;

public class LoxoneService : ILoxoneCommandExecutor
{
    private readonly ILogger<LoxoneService> _logger;
    private readonly LoxoneStateParser _stateParser;
    private LoxoneClient? _client;
    private LoxoneStructureState? _structure;
    private Func<string, NormalizedLightState, CancellationToken, Task>? _callback;

    public LoxoneService(ILogger<LoxoneService> logger, LoxoneStateParser stateParser)
    {
        _logger = logger;
        _stateParser = stateParser;
    }

    public LoxoneClient? Client => _client;

    public LoxoneStructureState? Structure => _structure;

    public async Task StartAsync(BridgeConfig config, Func<string, NormalizedLightState, CancellationToken, Task> callback, CancellationToken cancellationToken)
    {
        _callback = callback;
        _client = new LoxoneClient(new LoxoneConnectionOptions(config.Loxone.Host, config.Loxone.Port, config.Loxone.UseHttps));
        await _client.LoginAsync(config.Loxone.User, config.Loxone.Password, cancellationToken: cancellationToken).ConfigureAwait(false);
        _structure = new LoxoneStructureState(_client.Http, wsClient: _client.WebSocket);
        await _structure.LoadAsync(cancellationToken).ConfigureAwait(false);
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
            using var doc = await _client.Http.RequestJsonAsync($"dev/sps/io/{mapping.LoxoneUuidAction}/{command}", cancellationToken).ConfigureAwait(false);
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
                _logger.LogWarning("Mapping {Name} references unknown uuidAction {Uuid}", mapping.Name, mapping.LoxoneUuidAction);
                continue;
            }

            control.StateChanged += async (_, args) =>
            {
                await HandleStateAsync(mapping, args.Name, args.Value, cancellationToken).ConfigureAwait(false);
            };
        }
    }

    private Task HandleStateAsync(MappingSection mapping, string stateName, string value, CancellationToken cancellationToken)
    {
        if (_callback is null)
        {
            return Task.CompletedTask;
        }

        var normalized = mapping.Kind.ToLowerInvariant() switch
        {
            "dimmer" => _stateParser.FromDimmer(value),
            "colorpickerv2" => _stateParser.FromColorPicker(value),
            _ => NormalizedLightState.Empty
        };

        if (normalized == NormalizedLightState.Empty)
        {
            return Task.CompletedTask;
        }

        return _callback(mapping.LoxoneUuidAction, normalized, cancellationToken);
    }
}
