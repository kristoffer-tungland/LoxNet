using LoxNet.Bridge.Loxone;
using LoxNet.Bridge.Sync;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bridge.Tests;

public class ColorPickerParsingTests
{
    private readonly LoxoneStateParser _parser = new(new NullLogger<LoxoneStateParser>());

    [Fact]
    public void ParsesHsv()
    {
        var state = _parser.FromColorPicker("hsv(0,100,100)");
        Assert.Equal((0, 100, 100), state.Hsv);
    }

    [Fact]
    public void ParsesTemp()
    {
        var state = _parser.FromColorPicker("temp(100,4483)");
        Assert.Equal(100, state.BrightnessPct);
        Assert.Equal(4483, state.Kelvin);
    }
}
