using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using LoxNet.Bridge.Config;
using LoxNet.Bridge.Sync;

namespace LoxNet.Bridge.Mqtt;

public class Z2mPublisher
{
    private readonly Converters _converters;

    public Z2mPublisher(Converters converters)
    {
        _converters = converters;
    }

    public MqttApplicationMessage BuildMessage(MappingSection mapping, NormalizedLightState desired, NormalizedLightState lastSent)
    {
        var payload = BuildPayload(desired, lastSent);
        return new MqttApplicationMessageBuilder()
            .WithTopic($"{mapping.MqttTopic}/set")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
    }

    public string BuildPayload(NormalizedLightState desired, NormalizedLightState lastSent)
    {
        using var buffer = new MemoryStream();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();

        if (desired.Power != lastSent.Power && desired.Power.HasValue)
        {
            writer.WriteString("state", desired.Power.Value ? "ON" : "OFF");
        }

        if (desired.BrightnessPct.HasValue && (!lastSent.BrightnessPct.HasValue || desired.BrightnessPct.Value != lastSent.BrightnessPct.Value))
        {
            writer.WriteNumber("brightness", _converters.ToMqttBrightness(desired.BrightnessPct.Value));
        }

        if (desired.Kelvin.HasValue && (!lastSent.Kelvin.HasValue || desired.Kelvin.Value != lastSent.Kelvin.Value))
        {
            writer.WriteNumber("color_temp", _converters.ToMired(desired.Kelvin.Value));
        }

        if (desired.Hsv.HasValue && (!lastSent.Hsv.HasValue || desired.Hsv.Value != lastSent.Hsv.Value))
        {
            writer.WritePropertyName("color");
            writer.WriteStartObject();
            var hsv = desired.Hsv.Value;
            writer.WriteNumber("h", hsv.H);
            writer.WriteNumber("s", hsv.S);
            writer.WriteNumber("v", hsv.V);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    public async Task PublishAsync(IMqttClient client, MappingSection mapping, NormalizedLightState desired, NormalizedLightState lastSent, CancellationToken cancellationToken)
    {
        var message = BuildMessage(mapping, desired, lastSent);
        await client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
    }
}
