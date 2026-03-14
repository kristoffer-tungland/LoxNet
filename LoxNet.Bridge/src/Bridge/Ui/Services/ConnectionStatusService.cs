using LoxNet.Bridge.Loxone;
using LoxNet.Bridge.Mqtt;

namespace LoxNet.Bridge.Ui.Services;

public class ConnectionStatusService
{
    private readonly LoxoneService _loxoneService;
    private readonly MqttService _mqttService;
    private System.Threading.Timer? _timer;

    public ConnectionStatusService(LoxoneService loxoneService, MqttService mqttService)
    {
        _loxoneService = loxoneService;
        _mqttService = mqttService;

        // Push-based: fire immediately on any Loxone state transition.
        _loxoneService.StateChanged += (_, _) => ConnectionStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ConnectionStatusChanged;

    // ── Loxone ──────────────────────────────────────────────────────────────

    public LoxoneConnectionState LoxoneState => _loxoneService.State;

    public bool IsLoxoneConnected => _loxoneService.State == LoxoneConnectionState.Connected;

    public int LoxoneReconnectAttempts => _loxoneService.ReconnectAttempts;

    public DateTimeOffset? LoxoneLastDisconnectedAt => _loxoneService.LastDisconnectedAt;

    // ── MQTT ─────────────────────────────────────────────────────────────────

    public bool IsMqttConnected => _mqttService.Client.IsConnected;

    // ── Polling timer (drives MQTT status updates which have no push event) ──

    public void StartMonitoring()
    {
        _timer = new System.Threading.Timer(
            _ => ConnectionStatusChanged?.Invoke(this, EventArgs.Empty),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2));
    }

    public void StopMonitoring()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
