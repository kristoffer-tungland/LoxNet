using LoxNet.Bridge.Config;
using LoxNet.Bridge.Sync;

namespace LoxNet.Bridge.Loxone;

public class LoxoneCommandBuilder
{
    public IReadOnlyList<string> BuildCommands(MappingSection mapping, NormalizedLightState state)
    {
        var commands = new List<string>();
        var kind = mapping.Kind.ToLowerInvariant();
        if (kind == "dimmer")
        {
            if (state.Power.HasValue)
            {
                commands.Add(state.Power.Value ? "on" : "off");
            }

            if (state.BrightnessPct.HasValue)
            {
                commands.Add(state.BrightnessPct.Value.ToString());
            }
        }
        else if (kind == "colorpickerv2")
        {
            if (state.Kelvin.HasValue && state.BrightnessPct.HasValue)
            {
                commands.Add($"temp({state.BrightnessPct.Value},{state.Kelvin.Value})");
            }
            else if (state.BrightnessPct.HasValue)
            {
                commands.Add($"setBrightness/{state.BrightnessPct.Value}");
            }

            if (state.Hsv.HasValue && mapping.Options.AllowColor)
            {
                var hsv = state.Hsv.Value;
                commands.Add($"hsv({hsv.H},{hsv.S},{hsv.V})");
            }
        }

        return commands;
    }
}
