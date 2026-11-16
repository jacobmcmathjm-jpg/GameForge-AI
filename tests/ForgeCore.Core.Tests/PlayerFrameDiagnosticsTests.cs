using ForgeCore.Core;

namespace ForgeCore.Core.Tests;

public sealed class PlayerFrameDiagnosticsTests
{
    private static PlayerFrameDiagnostics Baseline() => new(1,"stopwatch_update_intervals_v1",1000,20,2,1100,2100,
        1000,60,20,10000,600,25,0,0,0,0,0,true,false,[]);
    [Fact] public void PhaseMetrics_KeepStartupSeparateButRejectLateFreezeDespiteGoodP95()
    {
        var d = Baseline(); d.Validate(600,10,25); Assert.True(d.SmoothnessPassed(25)); Assert.True(d.StartupPassed);
        var freeze = d with {MaximumStopwatchFrameMilliseconds=1727.84,FramesOver33Milliseconds=1,FramesOver50Milliseconds=1,
            FramesOver100Milliseconds=1,FrameSpikes=[new(300,5000,1727.84,1727.84,true,0,0,0)]};
        freeze.Validate(600,10,1727.84); Assert.False(freeze.SmoothnessPassed(1727.84));
        var slowStartup = d with {InteractiveSinceStartupMilliseconds=16000,MaximumWarmupFrameMilliseconds=1500};
        Assert.False(slowStartup.StartupPassed); Assert.True(slowStartup.SmoothnessPassed(25));
    }
    [Theory]
    [InlineData(-1)] [InlineData(1)] [InlineData(601)]
    public void ContradictoryLongFrameCounts_AreRejected(int count)
    {
        var d = Baseline() with {FramesOver33Milliseconds=count};
        Assert.Throws<ArgumentException>(()=>d.Validate(600,10,25));
    }
    [Fact] public void MissingSamples_NonFiniteValuesAndTruncatedMeasurement_AreRejected()
    {
        foreach(var d in new[] {Baseline() with {FrameSpikes=null!},Baseline() with {ThemeMilliseconds=double.NaN},
            Baseline() with {MeasuredWallMilliseconds=9999},Baseline() with {WarmupMilliseconds=999},
            Baseline() with {SampledFrames=599}})
            Assert.Throws<ArgumentException>(()=>d.Validate(600,10,25));
    }
}
