using System.Text.Json;

namespace LoxNet;

/// <summary>
/// Factory used to instantiate typed <see cref="LoxoneControl"/> instances.
/// </summary>
public static class LoxoneControlFactory
{
    /// <summary>
    /// Creates a control instance for the given type.
    /// </summary>
    public static LoxoneControl Create(ControlType type) => type switch
    {
        ControlType.Switch => new SwitchControl(),
        ControlType.LightController => new LightController(),
        ControlType.LightControllerV2 => new LightControllerV2(),
        _ => new LoxoneControl()
    };

    /// <summary>
    /// Populates control specific detail properties using the raw details from
    /// <see cref="LoxoneControl.RawDetails"/>.
    /// </summary>
    /// <param name="control">The control instance whose details should be parsed.</param>
    /// <param name="options">Serializer options for deserializing the details.</param>
    public static void ParseDetails(LoxoneControl control, JsonSerializerOptions options)
    {
        var details = control.RawDetails;
        if (!details.HasValue)
            return;

        switch (control)
        {
            case LightController light:
                light.Details = DeserializeDetails<LightControllerDetails>(details, options);
                break;
            case LightControllerV2 lightV2:
                lightV2.Details = DeserializeDetails<LightControllerV2Details>(details, options);
                break;
            case SwitchControl sw:
                sw.Details = DeserializeDetails<SwitchControlDetails>(details, options);
                break;
        }
    }

    private static T? DeserializeDetails<T>(JsonElement? details, JsonSerializerOptions options)
    {
        return details.HasValue
            ? JsonSerializer.Deserialize<T>(details.Value.GetRawText(), options)
            : default;
    }
}
