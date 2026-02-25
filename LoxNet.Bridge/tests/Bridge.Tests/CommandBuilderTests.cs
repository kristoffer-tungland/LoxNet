using LoxNet;
using LoxNet.Bridge.Config;
using LoxNet.Bridge.Loxone;
using LoxNet.Bridge.Sync;
using Xunit;

namespace Bridge.Tests;

public class CommandBuilderTests
{
    private readonly LoxoneCommandBuilder _builder = new();

    [Fact]
    public void DimmerCommandsRespectPower()
    {
        var commands = _builder.BuildCommands(ControlType.Dimmer, new NormalizedLightState(true, null, null, null));
        Assert.Contains("on", commands);
    }

    [Fact]
    public void DimmerCommandsIncludeBrightness()
    {
        var commands = _builder.BuildCommands(ControlType.Dimmer, new NormalizedLightState(null, 42, null, null));
        Assert.Contains("42", commands);
    }

    [Fact]
    public void ColorPickerTempCommand()
    {
        var commands = _builder.BuildCommands(ControlType.ColorPickerV2, new NormalizedLightState(null, 80, 3000, null));
        Assert.Contains("temp(80,3000)", commands);
    }

    [Fact]
    public void ColorPickerBrightnessCommand()
    {
        var commands = _builder.BuildCommands(ControlType.ColorPickerV2, new NormalizedLightState(null, 50, null, null));
        Assert.Contains("setBrightness/50", commands);
    }

    [Fact]
    public void ColorPickerHsvCommand()
    {
        var commands = _builder.BuildCommands(ControlType.ColorPickerV2, new NormalizedLightState(null, null, null, (10, 20, 30)));
        Assert.Contains("hsv(10,20,30)", commands);
    }
}
