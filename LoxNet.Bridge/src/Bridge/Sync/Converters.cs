namespace LoxNet.Bridge.Sync;

public class Converters
{
    public int ToBrightnessPercent(int mqttBrightness)
    {
        return (int)Math.Round(mqttBrightness / 254.0 * 100);
    }

    public int ToMqttBrightness(int percent)
    {
        var value = (int)Math.Round(percent / 100.0 * 254);
        return Math.Clamp(value, 0, 254);
    }

    public int ToKelvin(int mired)
    {
        return (int)Math.Round(1_000_000.0 / mired);
    }

    public int ToMired(int kelvin)
    {
        return (int)Math.Round(1_000_000.0 / kelvin);
    }
}
