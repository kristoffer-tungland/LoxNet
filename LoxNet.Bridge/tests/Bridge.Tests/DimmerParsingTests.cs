using LoxNet.Bridge.Loxone;
using LoxNet.Bridge.Sync;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bridge.Tests;

public class DimmerParsingTests
{
    private readonly LoxoneStateParser _parser = new(new NullLogger<LoxoneStateParser>());

    [Fact]
    public void ParsesWholeNumber()
    {
        var state = _parser.FromDimmer(LoxoneStateParser.DimmerPositionState, "77");
        Assert.Equal(77, state.BrightnessPct);
        Assert.True(state.Power);
    }

    [Fact]
    public void ParsesFloatWithRounding()
    {
        var state = _parser.FromDimmer(LoxoneStateParser.DimmerPositionState, "74.99999999");
        Assert.Equal(75, state.BrightnessPct);
        Assert.True(state.Power);
    }

    [Fact]
    public void ZeroIsOff()
    {
        var state = _parser.FromDimmer(LoxoneStateParser.DimmerPositionState, "0");
        Assert.Equal(0, state.BrightnessPct);
        Assert.False(state.Power);
    }

    [Fact]
    public void IgnoresNonPositionState()
    {
        var state = _parser.FromDimmer("min", "0");
        Assert.Equal(NormalizedLightState.Empty, state);
    }
}
