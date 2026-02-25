using LoxNet.Bridge.Config;
using LoxNet.Bridge.Loxone;
using LoxNet.Bridge.Mqtt;
using LoxNet;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace LoxNet.Bridge.Api;

public static class ApiEndpoints
{
    public static object BuildHealth(MqttService mqttService, LoxoneService loxoneService, BridgeConfig config)
    {
        var mqttStatus = mqttService.Client.IsConnected ? "connected" : "disconnected";
        var loxoneStatus = loxoneService.Client is not null ? "connected" : "disconnected";
        var mappings = new List<object>();
        var degraded = false;
        
        foreach (var mapping in config.Mappings)
        {
            var status = "ok";
            if (loxoneService.Structure is null || !loxoneService.Structure.TryGetControl(mapping.LoxoneUuidAction, out _))
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

    public static IEnumerable<object> DiscoverLoxoneSubcontrols(LoxoneService loxoneService)
    {
        if (loxoneService.Structure is null)
        {
            return Array.Empty<object>();
        }

        return loxoneService.Structure.Controls.Values
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

    public static async Task<IResult> DiscoverMqttLightsAsync(MqttService mqttService, CancellationToken cancellationToken)
    {
        var haLights = await mqttService.DiscoverHaLightsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

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

    public static async Task<IResult> ConnectLoxoneAsync(LoxoneSectionDto payload, LoxoneService loxoneService, CancellationToken cancellationToken)
    {
        try
        {
            // Stop existing connection if any
            await loxoneService.StopAsync(cancellationToken).ConfigureAwait(false);

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
            await loxoneService.StartAsync(tempConfig, (_, _, _) => Task.CompletedTask, cancellationToken).ConfigureAwait(false);

            return Results.Ok(new { message = "Loxone connection established successfully" });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to connect to Loxone: {ex.Message}" });
        }
    }

    public static async Task<IResult> ConnectMqttAsync(MqttSectionDto payload, MqttService mqttService, CancellationToken cancellationToken)
    {
        try
        {
            // Stop existing connection if any
            await mqttService.StopAsync(cancellationToken).ConfigureAwait(false);

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
            await mqttService.StartAsync(tempConfig, (_, _, _) => Task.CompletedTask, cancellationToken).ConfigureAwait(false);

            return Results.Ok(new { message = "MQTT connection established successfully" });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to connect to MQTT: {ex.Message}" });
        }
    }

    public static async Task<IResult> SaveConfigAsync(BridgeConfigDto payload, BridgeConfig currentConfig, ConfigFileSettings configFileSettings)
    {
        try
        {
            var updatedConfig = BridgeConfigMapper.ToConfig(payload, currentConfig.Sync);
            updatedConfig.Validate();
            var yaml = ConfigYamlSerializer.Serialize(updatedConfig);
            var directory = Path.GetDirectoryName(configFileSettings.Path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(configFileSettings.Path, yaml).ConfigureAwait(false);

            return Results.Ok(new ConfigSaveResult
            {
                Message = "Configuration saved. Restart the bridge to apply the new settings.",
                Path = configFileSettings.Path,
                RequiresRestart = true
            });
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

}
