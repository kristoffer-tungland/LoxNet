using System.Text.Json;
using LoxNet.Bridge.Sync;

namespace LoxNet.Bridge.Mqtt;

public class Z2mMessageParser
{
    private readonly Converters _converters;

    public Z2mMessageParser(Converters converters)
    {
        _converters = converters;
    }

    public NormalizedLightState Parse(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        bool? power = null;
        int? brightnessPct = null;
        int? kelvin = null;
        (int H, int S, int V)? hsv = null;

        if (root.TryGetProperty("state", out var stateProp))
        {
            var state = stateProp.GetString();
            power = string.Equals(state, "ON", StringComparison.OrdinalIgnoreCase);
        }

        if (root.TryGetProperty("brightness", out var brightnessProp) && brightnessProp.TryGetInt32(out var brightness))
        {
            brightnessPct = _converters.ToBrightnessPercent(brightness);
        }

        if (root.TryGetProperty("color_temp", out var ctProp) && ctProp.TryGetInt32(out var colorTemp))
        {
            kelvin = _converters.ToKelvin(colorTemp);
        }

        if (root.TryGetProperty("color", out var colorProp))
        {
            var h = colorProp.TryGetProperty("h", out var hProp) && hProp.TryGetInt32(out var hVal) ? hVal : (int?)null;
            var s = colorProp.TryGetProperty("s", out var sProp) && sProp.TryGetInt32(out var sVal) ? sVal : (int?)null;
            var v = colorProp.TryGetProperty("v", out var vProp) && vProp.TryGetInt32(out var vVal) ? vVal : (int?)null;

            if (h.HasValue && s.HasValue && v.HasValue)
            {
                hsv = (h.Value, s.Value, v.Value);
            }
        }

        return new NormalizedLightState(power, brightnessPct, kelvin, hsv);
    }
}
