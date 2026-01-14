using LoxNet.Bridge.Config;
using LoxNet.Bridge.Loxone;
using LoxNet.Bridge.Mqtt;
using LoxNet.Bridge.Sync;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LoxNet;
using LoxNet.Bridge.Ui;
using System.ComponentModel.DataAnnotations;

namespace LoxNet.Bridge.Api;

public class MinimalApiHost : IHostedService
{
    private readonly BridgeConfig _config;
    private readonly ConfigFileSettings _configFileSettings;
    private readonly MqttService _mqttService;
    private readonly LoxoneService _loxoneService;
    private IHost? _webHost;

    public MinimalApiHost(BridgeConfig config, ConfigFileSettings configFileSettings, MqttService mqttService, LoxoneService loxoneService)
    {
        _config = config;
        _configFileSettings = configFileSettings;
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

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddHttpClient();

        var app = builder.Build();

        app.UseStaticFiles();
        app.UseAntiforgery();

        app.MapGet("/health", () => BuildHealth());
        app.MapGet("/discover/loxone/subcontrols", () => DiscoverLoxoneSubcontrols());
        app.MapGet("/discover/mqtt/devices", () => new { devices = Array.Empty<string>(), hint = "Subscribe to zigbee2mqtt/bridge/devices for details" });
        app.MapGet("/config", () => Results.Ok(BridgeConfigMapper.ToDto(_config)));
        app.MapPost("/config", async (BridgeConfigDto payload) => await SaveConfigAsync(payload).ConfigureAwait(false));
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

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

    private async Task<IResult> SaveConfigAsync(BridgeConfigDto payload)
    {
        try
        {
            var updatedConfig = BridgeConfigMapper.ToConfig(payload, _config.Sync);
            updatedConfig.Validate();
            var yaml = ConfigYamlSerializer.Serialize(updatedConfig);
            var directory = Path.GetDirectoryName(_configFileSettings.Path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_configFileSettings.Path, yaml).ConfigureAwait(false);

            return Results.Ok(new ConfigSaveResult
            {
                Message = "Configuration saved. Restart the bridge to apply the new settings.",
                Path = _configFileSettings.Path,
                RequiresRestart = true
            });
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
