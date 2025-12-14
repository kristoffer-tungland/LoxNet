namespace LoxNet.Bridge.Sync;

public record NormalizedLightState(bool? Power, int? BrightnessPct, int? Kelvin, (int H, int S, int V)? Hsv)
{
    public static NormalizedLightState Empty { get; } = new(null, null, null, null);
}
