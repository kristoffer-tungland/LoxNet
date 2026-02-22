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
    }

    public event EventHandler? ConnectionStatusChanged;

    public bool IsLoxoneConnected => _loxoneService.Client is not null;
    
    public bool IsMqttConnected => _mqttService.Client.IsConnected;

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
