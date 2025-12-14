using LoxNet.Bridge.Sync;
using Xunit;

namespace Bridge.Tests;

public class ConvertersTests
{
    private readonly Converters _converters = new();

    [Theory]
    [InlineData(0, 0)]
    [InlineData(254, 100)]
    public void ToBrightnessPercent_MapsRange(int input, int expected)
    {
        var percent = _converters.ToBrightnessPercent(input);
        Assert.Equal(expected, percent);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 254)]
    public void ToMqttBrightness_MapsRange(int pct, int expected)
    {
        var brightness = _converters.ToMqttBrightness(pct);
        Assert.Equal(expected, brightness);
    }

    [Fact]
    public void Brightness_RoundTripWithinTolerance()
    {
        for (var pct = 0; pct <= 100; pct += 10)
        {
            var mqtt = _converters.ToMqttBrightness(pct);
            var roundTrip = _converters.ToBrightnessPercent(mqtt);
            Assert.InRange(roundTrip, pct - 1, pct + 1);
        }
    }

    [Fact]
    public void Kelvin_Mired_RoundTrip()
    {
        var kelvin = 4483;
        var mired = _converters.ToMired(kelvin);
        var roundTrip = _converters.ToKelvin(mired);
        Assert.InRange(roundTrip, kelvin - 25, kelvin + 25);
    }
}
