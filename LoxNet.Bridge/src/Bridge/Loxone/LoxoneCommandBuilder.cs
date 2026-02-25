using LoxNet;
using LoxNet.Bridge.Sync;

namespace LoxNet.Bridge.Loxone;

public class LoxoneCommandBuilder
{
    public IReadOnlyList<string> BuildCommands(ControlType controlType, NormalizedLightState state)
    {
        var commands = new List<string>();
        if (controlType == ControlType.Dimmer)
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
        else if (controlType == ControlType.ColorPickerV2)
        {
            if (state.Kelvin.HasValue && state.BrightnessPct.HasValue)
            {
                commands.Add($"temp({state.BrightnessPct.Value},{state.Kelvin.Value})");
            }
            else if (state.BrightnessPct.HasValue)
            {
                commands.Add($"setBrightness/{state.BrightnessPct.Value}");
            }

            if (state.Hsv.HasValue)
            {
                var hsv = state.Hsv.Value;
                commands.Add($"hsv({hsv.H},{hsv.S},{hsv.V})");
            }
        }

        return commands;
    }
}
