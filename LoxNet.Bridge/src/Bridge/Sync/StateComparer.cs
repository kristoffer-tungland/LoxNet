using LoxNet.Bridge.Config;

namespace LoxNet.Bridge.Sync;

public class StateComparer
{
    private readonly BridgeConfig _config;

    public StateComparer(BridgeConfig config)
    {
        _config = config;
    }

    public bool AreEqual(NormalizedLightState left, NormalizedLightState right)
    {
        if (left.Power != right.Power)
        {
            return false;
        }

        if (!EqualWithTolerance(left.BrightnessPct, right.BrightnessPct, _config.Sync.BrightnessTolerancePct))
        {
            return false;
        }

        if (!EqualWithTolerance(left.Kelvin, right.Kelvin, _config.Sync.KelvinTolerance))
        {
            return false;
        }

        if (left.Hsv.HasValue != right.Hsv.HasValue)
        {
            return false;
        }

        if (left.Hsv.HasValue && right.Hsv.HasValue)
        {
            var l = left.Hsv.Value;
            var r = right.Hsv.Value;
            if (l.H != r.H || l.S != r.S || l.V != r.V)
            {
                return false;
            }
        }

        return true;
    }

    private static bool EqualWithTolerance(int? left, int? right, int tolerance)
    {
        if (!left.HasValue && !right.HasValue)
        {
            return true;
        }

        if (!left.HasValue || !right.HasValue)
        {
            return false;
        }

        return Math.Abs(left.Value - right.Value) <= tolerance;
    }
}
