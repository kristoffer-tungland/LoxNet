using LoxNet.Bridge.Config;
using LoxNet.Bridge.Sync;
using Microsoft.Extensions.Logging;
using LoxNet;

namespace LoxNet.Bridge.Loxone;

public class LoxoneService : ILoxoneCommandExecutor
{
    // Keepalive interval — well under the Miniserver's idle-disconnect threshold (~10 min).
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(25);

    // Reconnect backoff: 5 s, 10 s, 20 s, 40 s, then 60 s cap.
    private static readonly TimeSpan[] ReconnectDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(40),
        TimeSpan.FromSeconds(60)
    ];

    private readonly ILogger<LoxoneService> _logger;
    private readonly ILogger<LoxoneClient> _loxoneClientLogger;
    private readonly ILogger<LoxoneStructureState> _structureLogger;
    private readonly LoxoneStateParser _stateParser;
    private LoxoneClient? _client;
    private LoxoneStructureState? _structure;
    private Func<string, NormalizedLightState, CancellationToken, Task>? _callback;
    private BridgeConfig? _config;

    // Owns all background tasks (keepalive + reconnect loops). Cancelled by StopAsync.
    private CancellationTokenSource? _runCts;

    // Connection state observable by the UI.
    private LoxoneConnectionState _state = LoxoneConnectionState.Disconnected;

    public event EventHandler? StateChanged;

    public LoxoneConnectionState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int ReconnectAttempts { get; private set; }
    public DateTimeOffset? LastDisconnectedAt { get; private set; }

    public LoxoneService(ILogger<LoxoneService> logger, ILogger<LoxoneClient> loxoneClientLogger, ILogger<LoxoneStructureState> structureLogger, LoxoneStateParser stateParser)
    {
        _logger = logger;
        _loxoneClientLogger = loxoneClientLogger;
        _structureLogger = structureLogger;
        _stateParser = stateParser;
    }

    /// <remarks>Still exposed so the health endpoint can read it; prefer <see cref="State"/> for connection checks.</remarks>
    public LoxoneClient? Client => _client;

    public LoxoneStructureState? Structure => _structure;

    public bool TryGetControlType(string uuidAction, out ControlType controlType)
    {
        if (_structure is not null && _structure.TryGetControl(uuidAction, out var control) && control is not null)
        {
            controlType = control.Type;
            return true;
        }
        controlType = ControlType.Unknown;
        return false;
    }

    public async Task StartAsync(BridgeConfig config, Func<string, NormalizedLightState, CancellationToken, Task> callback, CancellationToken cancellationToken)
    {
        _callback = callback;
        _config = config;

        // Cancel any lingering background tasks from a previous session.
        await StopBackgroundTasksAsync().ConfigureAwait(false);
        _runCts = new CancellationTokenSource();

        await ConnectInternalAsync(config, _runCts.Token).ConfigureAwait(false);

        // Launch keepalive + reconnect loop as a long-running background task.
        _ = Task.Run(() => RunLoopAsync(_runCts.Token), _runCts.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopBackgroundTasksAsync().ConfigureAwait(false);
        await TearDownClientAsync().ConfigureAwait(false);
        State = LoxoneConnectionState.Disconnected;
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

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    /// <summary>Performs the full login + structure + subscribe sequence.</summary>
    private async Task ConnectInternalAsync(BridgeConfig config, CancellationToken cancellationToken)
    {
        State = LoxoneConnectionState.Connecting;

        var options = new LoxoneConnectionOptions(config.Loxone.Host, config.Loxone.Port, config.Loxone.UseHttps);
        _client = new LoxoneClient(_loxoneClientLogger, options);
        await _client.LoginAsync(config.Loxone.User, config.Loxone.Password, cancellationToken: cancellationToken).ConfigureAwait(false);

        _structure = new LoxoneStructureState(_structureLogger, _client.Http, options, wsClient: _client.WebSocket);
        await _structure.LoadAsync(useCacheOnly: false, cancellationToken).ConfigureAwait(false);
        await SubscribeToMappingsAsync(config, cancellationToken).ConfigureAwait(false);

        _ = Task.Run(() => _client.WebSocket.ListenAsync(cancellationToken), cancellationToken);

        await _client.WebSocket.CommandAsync("jdev/sps/enablebinstatusupdate", cancellationToken).ConfigureAwait(false);

        ReconnectAttempts = 0;
        State = LoxoneConnectionState.Connected;
    }

    /// <summary>
    /// Long-running background task. Sends keepalives and triggers a full reconnect
    /// when the socket drops — either detected by a failed keepalive or by the
    /// Disconnected event from the WebSocket client.
    /// </summary>
    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        // Wire up disconnect notification from the WebSocket client. This lets us react
        // immediately instead of waiting for the next keepalive timeout.
        using var disconnectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        void OnDisconnected(object? s, EventArgs e)
        {
            if (!disconnectCts.IsCancellationRequested)
                disconnectCts.Cancel();
        }

        if (_client is not null)
            _client.WebSocket.Disconnected += OnDisconnected;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(KeepAliveInterval, disconnectCts.Token).ConfigureAwait(false);
                    await _client!.WebSocket.KeepAliveAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Intentional stop — exit cleanly.
                    return;
                }
                catch (Exception ex)
                {
                    // Either a failed keepalive or the disconnect signal fired.
                    _logger.LogWarning(ex is OperationCanceledException
                        ? "[LoxoneService] WebSocket disconnected unexpectedly"
                        : "[LoxoneService] Keepalive failed: {Message}",
                        ex is OperationCanceledException ? (object)string.Empty : ex.Message);

                    // Unwire the old handler before the client is replaced.
                    if (_client is not null)
                        _client.WebSocket.Disconnected -= OnDisconnected;

                    LastDisconnectedAt = DateTimeOffset.UtcNow;
                    await ReconnectWithBackoffAsync(cancellationToken).ConfigureAwait(false);

                    // Wire up the new client's disconnect event.
                    if (_client is not null)
                    {
                        // Reset the disconnect token so keepalive can run on the new connection.
                        disconnectCts.TryReset();
                        _client.WebSocket.Disconnected += OnDisconnected;
                    }
                }
            }
        }
        finally
        {
            if (_client is not null)
                _client.WebSocket.Disconnected -= OnDisconnected;
        }
    }

    /// <summary>Performs full reconnect with exponential backoff until successful or cancelled.</summary>
    private async Task ReconnectWithBackoffAsync(CancellationToken cancellationToken)
    {
        State = LoxoneConnectionState.Reconnecting;

        await TearDownClientAsync().ConfigureAwait(false);

        int attempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            ReconnectAttempts++;
            attempt++;
            var delay = ReconnectDelays[Math.Min(attempt - 1, ReconnectDelays.Length - 1)];
            _logger.LogInformation("[LoxoneService] Reconnecting in {Delay}s (attempt {Attempt})", delay.TotalSeconds, attempt);

            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                await ConnectInternalAsync(_config!, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("[LoxoneService] Reconnected successfully");
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[LoxoneService] Reconnect attempt {Attempt} failed", attempt);
            }
        }
    }

    private async Task TearDownClientAsync()
    {
        if (_client is not null)
        {
            try
            {
                await _client.WebSocket.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch { /* ignore errors during teardown */ }

            try
            {
                await _client.DisposeAsync().ConfigureAwait(false);
            }
            catch { /* ignore */ }

            _client = null;
        }
    }

    private async Task StopBackgroundTasksAsync()
    {
        if (_runCts is not null)
        {
            await _runCts.CancelAsync().ConfigureAwait(false);
            _runCts.Dispose();
            _runCts = null;
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

            var controlType = control.Type;
            control.StateChanged += (_, args) =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleStateAsync(mapping, controlType, args.State, args.Value, cancellationToken).ConfigureAwait(false);
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

    private Task HandleStateAsync(MappingSection mapping, ControlType controlType, string stateName, string value, CancellationToken cancellationToken)
    {
        if (_callback is null)
        {
            return Task.CompletedTask;
        }

        _logger.LogTrace("Loxone state change: {Name} state={State} value={Value}", mapping.Name, stateName, value);

        var normalized = controlType switch
        {
            ControlType.Dimmer => _stateParser.FromDimmer(stateName, value),
            ControlType.ColorPickerV2 => _stateParser.FromColorPicker(stateName, value),
            _ => NormalizedLightState.Empty
        };

        if (normalized == NormalizedLightState.Empty)
        {
            return Task.CompletedTask;
        }

        return _callback(mapping.LoxoneUuidAction, normalized, cancellationToken);
    }
}
