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
using Microsoft.FluentUI.AspNetCore.Components;
using LoxNet.Bridge.Ui.Services;

namespace LoxNet.Bridge.Api;

public class MinimalApiHost : IHostedService
{
    private readonly BridgeConfig _config;
    private readonly ConfigFileSettings _configFileSettings;
    private readonly MqttService _mqttService;
    private readonly LoxoneService _loxoneService;
    private readonly ConnectionStatusService _connectionStatusService;
    private IHost? _webHost;

    public MinimalApiHost(BridgeConfig config, ConfigFileSettings configFileSettings, MqttService mqttService, LoxoneService loxoneService, ConnectionStatusService connectionStatusService)
    {
        _config = config;
        _configFileSettings = configFileSettings;
        _mqttService = mqttService;
        _loxoneService = loxoneService;
        _connectionStatusService = connectionStatusService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(MinimalApiHost).Assembly.FullName,
            Args = Array.Empty<string>(),
            EnvironmentName = Environments.Production,
            WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddHttpClient("Default", client =>
        {
            client.BaseAddress = new Uri("http://localhost:5000");
        });
        
        // Register Fluent UI services
        builder.Services.AddFluentUIComponents();
        
        // Share parent services with web app
        builder.Services.AddSingleton(_connectionStatusService);

        var app = builder.Build();

        // Enable static files from wwwroot
        app.UseStaticFiles();
        
        // Map Razor components - this should register _content routes
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
        
        app.UseAntiforgery();

        app.MapGet("/health", () => BuildHealth());
        app.MapGet("/api/loxone/subcontrols", () => DiscoverLoxoneSubcontrols());
        app.MapGet("/api/mqtt/lights", async (CancellationToken ct) => await DiscoverMqttLightsAsync(ct).ConfigureAwait(false));
        app.MapPost("/api/loxone/connect", async (LoxoneSectionDto payload, CancellationToken ct) => await ConnectLoxoneAsync(payload, ct).ConfigureAwait(false));
        app.MapPost("/api/mqtt/connect", async (MqttSectionDto payload, CancellationToken ct) => await ConnectMqttAsync(payload, ct).ConfigureAwait(false));
        app.MapGet("/config", () => Results.Ok(BridgeConfigMapper.ToDto(_config)));
        app.MapPost("/config", async (BridgeConfigDto payload) => await SaveConfigAsync(payload).ConfigureAwait(false));

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
            if (_loxoneService.Structure is null || !_loxoneService.Structure.TryGetControl(mapping.LoxoneUuidAction, out _))
            {
                status = "missing_subcontrol";
            }

            degraded |= status != "ok";

            mappings.Add(new
            {
                mapping.Name,
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

    private async Task<IResult> DiscoverMqttLightsAsync(CancellationToken cancellationToken)
    {
        var haLights = await _mqttService.DiscoverHaLightsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var lights = haLights.Select(l =>
        {
            var features = new List<string>();
            if (l.Brightness) features.Add("brightness");
            if (l.Effect) features.Add("effect");
            if (l.SupportedColorModes is not null) features.AddRange(l.SupportedColorModes);

            var modelParts = new[] { l.Device?.Manufacturer, l.Device?.Model }
                .Where(s => !string.IsNullOrWhiteSpace(s));

            return new MqttDeviceDto
            {
                FriendlyName = l.Device?.Name ?? l.ObjectId ?? l.UniqueId ?? string.Empty,
                Topic = l.StateTopic ?? string.Empty,
                Model = string.Join(" ", modelParts),
                SupportedFeatures = features.ToArray()
            };
        }).ToList();

        return Results.Ok(new MqttLightsDiscoveryResult { Lights = lights });
    }

    private async Task<IResult> ConnectLoxoneAsync(LoxoneSectionDto payload, CancellationToken cancellationToken)
    {
        try
        {
            // Stop existing connection if any
            await _loxoneService.StopAsync(cancellationToken).ConfigureAwait(false);

            // Create temporary config with new credentials
            var tempConfig = new BridgeConfig
            {
                Loxone = new LoxoneSection
                {
                    Host = payload.Host,
                    Port = payload.Port,
                    UseHttps = payload.UseHttps,
                    User = payload.User,
                    Password = payload.Password
                }
            };

            // Start connection with empty callback (no sync)
            await _loxoneService.StartAsync(tempConfig, (_, _, _) => Task.CompletedTask, cancellationToken).ConfigureAwait(false);

            return Results.Ok(new { message = "Loxone connection established successfully" });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to connect to Loxone: {ex.Message}" });
        }
    }

    private async Task<IResult> ConnectMqttAsync(MqttSectionDto payload, CancellationToken cancellationToken)
    {
        try
        {
            // Stop existing connection if any
            await _mqttService.StopAsync(cancellationToken).ConfigureAwait(false);

            // Create temporary config with new credentials
            var tempConfig = new BridgeConfig
            {
                Mqtt = new MqttSection
                {
                    Host = payload.Host,
                    Port = payload.Port,
                    Username = payload.Username,
                    Password = payload.Password,
                    ClientId = payload.ClientId,
                    BaseTopic = payload.BaseTopic
                }
            };

            // Start connection with empty callback (no sync)
            await _mqttService.StartAsync(tempConfig, (_, _, _) => Task.CompletedTask, cancellationToken).ConfigureAwait(false);

            return Results.Ok(new { message = "MQTT connection established successfully" });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to connect to MQTT: {ex.Message}" });
        }
    }

    private async Task<IResult> SaveConfigAsync(BridgeConfigDto payload)
    {
        try
        {
            var updatedConfig = BridgeConfigMapper.ToConfig(payload, _config);
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
