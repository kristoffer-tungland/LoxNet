using LoxNet.Bridge.Sync;
using Microsoft.Extensions.Logging;

namespace LoxNet.Bridge.Loxone;

public class LoxoneStateParser
{
    private readonly ILogger<LoxoneStateParser> _logger;

    public LoxoneStateParser(ILogger<LoxoneStateParser> logger)
    {
        _logger = logger;
    }

    public NormalizedLightState FromDimmer(string? value)
    {
        if (int.TryParse(value, out var pos))
        {
            return new NormalizedLightState(pos > 0, pos, null, null);
        }

        _logger.LogWarning("Unable to parse dimmer state {Value}", value);
        return NormalizedLightState.Empty;
    }

    public NormalizedLightState FromColorPicker(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return NormalizedLightState.Empty;
        }

        if (value.StartsWith("hsv(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(')'))
        {
            var content = value[4..^1];
            var parts = content.Split(',');
            if (parts.Length == 3 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var s) && int.TryParse(parts[2], out var v))
            {
                return new NormalizedLightState(null, null, null, (h, s, v));
            }
        }

        if (value.StartsWith("temp(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(')'))
        {
            var content = value[5..^1];
            var parts = content.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out var brightness) && int.TryParse(parts[1], out var kelvin))
            {
                return new NormalizedLightState(null, brightness, kelvin, null);
            }
        }

        _logger.LogWarning("Unrecognized color picker state {Value}", value);
        return NormalizedLightState.Empty;
    }
}
