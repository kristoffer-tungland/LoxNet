using LoxNet.Bridge.Config;
using LoxNet.Bridge.Loxone;
using LoxNet.Bridge.Mqtt;
using LoxNet.Bridge.Logging;
using LoxNet;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace LoxNet.Bridge.Api;

public static class ApiEndpoints
{
    public static object BuildHealth(MqttService mqttService, LoxoneService loxoneService, ConfigStore configStore)
    {
        var mqttStatus = mqttService.Client.IsConnected ? "connected" : "disconnected";
        var loxoneStatus = loxoneService.Client is not null ? "connected" : "disconnected";
        var mappings = new List<object>();
        var degraded = false;
        
        foreach (var mapping in configStore.Current.Mappings)
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

    public static async Task<IResult> ConnectLoxoneAsync(LoxoneSectionDto payload, AppHost appHost, CancellationToken cancellationToken)
    {
        try
        {
            var section = new LoxoneSection
            {
                Host = payload.Host,
                Port = payload.Port,
                UseHttps = payload.UseHttps,
                User = payload.User,
                Password = payload.Password
            };

            await appHost.ReconnectLoxoneAsync(section, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { message = "Loxone connection established successfully" });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to connect to Loxone: {ex.Message}" });
        }
    }

    public static async Task<IResult> ConnectMqttAsync(MqttSectionDto payload, AppHost appHost, CancellationToken cancellationToken)
    {
        try
        {
            var section = new MqttSection
            {
                Host = payload.Host,
                Port = payload.Port,
                Username = payload.Username,
                Password = payload.Password,
                ClientId = payload.ClientId,
                BaseTopic = payload.BaseTopic
            };

            await appHost.ReconnectMqttAsync(section, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { message = "MQTT connection established successfully" });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to connect to MQTT: {ex.Message}" });
        }
    }

    public static async Task<IResult> SaveLoxoneSettingsAsync(LoxoneSectionDto payload, AppHost appHost, CancellationToken cancellationToken)
    {
        try
        {
            var section = new LoxoneSection
            {
                Host = payload.Host,
                Port = payload.Port,
                UseHttps = payload.UseHttps,
                User = payload.User,
                Password = payload.Password
            };

            await appHost.ReconnectLoxoneAsync(section, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { message = "Loxone settings saved and connected" });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to save Loxone settings: {ex.Message}" });
        }
    }

    public static async Task<IResult> SaveMqttSettingsAsync(MqttSectionDto payload, AppHost appHost, CancellationToken cancellationToken)
    {
        try
        {
            var section = new MqttSection
            {
                Host = payload.Host,
                Port = payload.Port,
                Username = payload.Username,
                Password = payload.Password,
                ClientId = payload.ClientId,
                BaseTopic = payload.BaseTopic
            };

            await appHost.ReconnectMqttAsync(section, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { message = "MQTT settings saved and connected" });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to save MQTT settings: {ex.Message}" });
        }
    }

    public static async Task<IResult> SaveSyncSettingsAsync(SyncSectionDto payload, AppHost appHost, CancellationToken cancellationToken)
    {
        try
        {
            var section = new SyncSection
            {
                BrightnessTolerancePct = payload.BrightnessTolerancePct,
                KelvinTolerance = payload.KelvinTolerance,
                SuppressEchoWindowMs = payload.SuppressEchoWindowMs
            };

            await appHost.UpdateSyncAsync(section, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { message = "Sync settings saved" });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to save sync settings: {ex.Message}" });
        }
    }

    public static async Task<IResult> SaveLoggingSettingsAsync(LoggingSectionDto payload, AppHost appHost, CancellationToken cancellationToken)
    {
        try
        {
            var section = new LoggingSection { MinLevel = payload.MinLevel };
            await appHost.UpdateLoggingAsync(section, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { message = "Log level updated" });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to update log level: {ex.Message}" });
        }
    }

    public static async Task<IResult> SaveMappingsAsync(MappingSectionDto[] payload, AppHost appHost, CancellationToken cancellationToken)
    {
        try
        {
            var mappings = payload
                .Select(m => new MappingSection
                {
                    Name = m.Name,
                    MqttTopic = m.MqttTopic,
                    LoxoneUuidAction = m.LoxoneUuidAction
                })
                .ToList();

            // Validate each mapping individually (avoids requiring Loxone/Mqtt fields)
            foreach (var m in mappings)
            {
                Validator.ValidateObject(m, new ValidationContext(m), true);
            }

            var duplicateUuid = mappings
                .GroupBy(m => m.LoxoneUuidAction, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1)?.Key;
            if (duplicateUuid is not null)
                throw new ValidationException($"Duplicate loxoneUuidAction detected: {duplicateUuid}");

            var duplicateTopic = mappings
                .GroupBy(m => m.MqttTopic, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1)?.Key;
            if (duplicateTopic is not null)
                throw new ValidationException($"Duplicate mqttTopic detected: {duplicateTopic}");

            await appHost.ApplyMappingsAsync(mappings, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { message = "Mappings saved and applied" });
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

}
