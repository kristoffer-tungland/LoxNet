using LoxNet.Bridge.Config;
using LoxNet.Bridge.Loxone;
using LoxNet.Bridge.Mqtt;
using LoxNet.Bridge.Sync;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LoxNet;

namespace LoxNet.Bridge.Api;

public class MinimalApiHost : IHostedService
{
    private readonly BridgeConfig _config;
    private readonly MqttService _mqttService;
    private readonly LoxoneService _loxoneService;
    private IHost? _webHost;

    public MinimalApiHost(BridgeConfig config, MqttService mqttService, LoxoneService loxoneService)
    {
        _config = config;
        _mqttService = mqttService;
        _loxoneService = loxoneService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(MinimalApiHost).Assembly.FullName,
            Args = Array.Empty<string>()
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        var app = builder.Build();

        app.MapGet("/health", () => BuildHealth());
        app.MapGet("/discover/loxone/subcontrols", () => DiscoverLoxoneSubcontrols());
        app.MapGet("/discover/mqtt/devices", () => new { devices = Array.Empty<string>(), hint = "Subscribe to zigbee2mqtt/bridge/devices for details" });

        _webHost = app;
        await app.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_webHost is not null)
        {
            await _webHost.StopAsync(cancellationToken).ConfigureAwait(false);
            await _webHost.WaitForShutdownAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private object BuildHealth()
    {
        var mqttStatus = _mqttService.Client.IsConnected ? "connected" : "disconnected";
        var loxoneStatus = _loxoneService.Client is not null ? "connected" : "disconnected";
        var mappings = new List<object>();
        var degraded = false;
        foreach (var mapping in _config.Mappings)
        {
            var status = "ok";
            if (_loxoneService.Structure is null || !_loxoneService.Structure.TryGetControl(mapping.LoxoneUuidAction, out var control) || control is null)
            {
                status = "missing_subcontrol";
            }
            else if (!MatchesKind(control.Type, mapping.Kind))
            {
                status = "type_mismatch";
            }

            degraded |= status != "ok";

            mappings.Add(new
            {
                mapping.Name,
                mapping.Kind,
                mapping.MqttTopic,
                mapping.LoxoneUuidAction,
                status
            });
        }

        var bridgeStatus = degraded ? "degraded" : "ok";
        return new
        {
            status = bridgeStatus,
            mqtt = mqttStatus,
            loxone = loxoneStatus,
            mappings
        };
    }

    private IEnumerable<object> DiscoverLoxoneSubcontrols()
    {
        if (_loxoneService.Structure is null)
        {
            return Array.Empty<object>();
        }

        return _loxoneService.Structure.Controls.Values
            .Where(c => c.Type is ControlType.Dimmer or ControlType.ColorPickerV2)
            .Select(c => new
            {
                parentName = c.RoomName ?? string.Empty,
                subcontrolName = c.Name,
                type = c.Type.ToString(),
                uuidAction = c.UuidAction ?? c.Uuid
            })
            .DistinctBy(c => c.uuidAction);
    }

    private static bool MatchesKind(ControlType type, string kind)
    {
        return kind.ToLowerInvariant() switch
        {
            "dimmer" => type == ControlType.Dimmer,
            "colorpickerv2" => type == ControlType.ColorPickerV2,
            _ => false
        };
    }
}
