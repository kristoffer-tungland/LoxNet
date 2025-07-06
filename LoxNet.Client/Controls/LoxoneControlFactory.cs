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
    public static LoxoneControl Create(ControlType type)
    {
        return type switch
        {
            ControlType.Switch => new SwitchControl(),
            ControlType.LightController => new LightController(),
            ControlType.LightControllerV2 => new LightControllerV2(),
            _ => new LoxoneControl()
        };
    }

    /// <summary>
    /// Creates a typed control and populates detail properties when present.
    /// </summary>
    /// <param name="type">The control type to instantiate.</param>
    /// <param name="details">Optional details JSON element from the structure file.</param>
    /// <param name="options">Serializer options for deserializing the details.</param>
    public static LoxoneControl Create(ControlType type, JsonElement? details, JsonSerializerOptions options)
    {
        var ctrl = Create(type);

        if (details.HasValue)
        {
            switch (ctrl)
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

        return ctrl;
    }

    private static T? DeserializeDetails<T>(JsonElement? details, JsonSerializerOptions options)
    {
        return details.HasValue
            ? JsonSerializer.Deserialize<T>(details.Value.GetRawText(), options)
            : default;
    }
}
