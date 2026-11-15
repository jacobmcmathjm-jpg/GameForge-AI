using ForgeCore.Diagnostics;
namespace ForgeCore.Core.Tests;

public sealed class ProfileTimingDiagnosticsTests
{
    private static ProfileTimingReport Valid()
    {
        var c = new ProfileTimingCollector(1, 2, 10, default, default);
        c.Record(2.8, 40, default);
        for (var i = 0; i < 100; i++) c.Record(3.1 + i * 10d / 99, 20, default);
        return c.Snapshot();
    }
    private static void Validate(ProfileTimingReport r) => ProfileTimingCollector.Validate(r, 10, 100, 20, 20);
    [Fact] public void StartupIsLatencyNotInventedRenderedFramesAndWarmupIsFixed()
    {
        var r = Valid(); Validate(r);
        Assert.Equal(0, r.phases[0].sampledFrames); Assert.Equal(2, r.phases[0].plannedEndSeconds);
        Assert.Equal(1, r.runtimeBeforeSceneLoadSeconds); Assert.Equal(2, r.profileInitializedSeconds);
        Assert.Equal(1, r.warmupSeconds); Assert.Equal(1, r.phases[1].sampledFrames);
        Assert.Equal(40, r.phases[1].maximumFrameMilliseconds); Assert.Equal(100, r.phases[2].sampledFrames);
    }
    [Theory][InlineData(3.0)][InlineData(3.01)]
    public void FreezeCrossingWarmupBoundaryBelongsToGameplay(double end)
    {
        var c = new ProfileTimingCollector(1, 2, 10, default, default);
        c.Record(2.5, 16, default); c.Record(end, 1727.84, new() { gc0 = 1, managedBytes = 2048, allocatedBytes = 4096 });
        var r = c.Snapshot(); var e = Assert.Single(r.longFrames);
        Assert.Equal("gameplay", e.phase); Assert.True(e.crossesWarmupBoundary); Assert.True(e.crossesStartupBoundary);
        Assert.Equal(1727.84, r.phases[2].maximumFrameMilliseconds); Assert.Equal(1, e.gc0Collections);
        Assert.Equal(2048, e.managedMemoryAfterBytes); Assert.Equal(4096, e.allocatedMemoryAfterBytes);
    }
    [Fact] public void EventCapKeepsDroppedCountAndLaterWorstRatherThanLosingTheFreeze()
    {
        var c = new ProfileTimingCollector(1, 2, 10, default, default);
        for (var i = 0; i < 500; i++) c.Record(3.1 + i * .02, i == 499 ? 1727.84 : 40, default);
        var r = c.Snapshot(); Assert.Equal(64, r.longFrames.Count); Assert.Equal(436, r.droppedLongFrameEvents);
        Assert.Equal(500, r.longFrameCount); Assert.Equal(500, r.worstLongFrame.frameIndex);
        Assert.Equal(1727.84, r.worstLongFrame.frameMilliseconds);
        ProfileTimingCollector.Validate(r, 10, 500, r.phases[2].averageFrameMilliseconds, 1727.84);
    }
    [Theory][InlineData(33.334,0,0)][InlineData(33.335,1,0)][InlineData(100,1,0)][InlineData(100.001,1,1)]
    public void ThresholdsAreStrictAndDoNotChangeTheExistingMaximumBoundary(double ms, int longCount, int over100)
    {
        var c = new ProfileTimingCollector(1, 2, 10, default, default); c.Record(3.1, ms, default);
        Assert.Equal(longCount, c.Snapshot().longFrameCount); Assert.Equal(over100, c.Snapshot().phases[2].over100Milliseconds);
    }
    [Theory][InlineData("clock")][InlineData("warmup")][InlineData("cap")][InlineData("missing_phase")][InlineData("phase_name")]
    [InlineData("phase_origin")][InlineData("startup_frames")][InlineData("warmup_end")][InlineData("gameplay_end")][InlineData("nan")]
    [InlineData("frames")][InlineData("average")][InlineData("maximum")][InlineData("negative_gc")][InlineData("memory")]
    [InlineData("dropped")][InlineData("event_time")][InlineData("event_duration")][InlineData("event_phase")][InlineData("event_crossing")]
    [InlineData("missing_worst")][InlineData("wrong_worst")][InlineData("event_gc")][InlineData("long_count")][InlineData("null_phase")]
    public void UntrustedTimingEvidenceIsRejected(string change)
    {
        var r = Valid();
        switch(change)
        {
            case "clock": r.clock="utc"; break;
            case "warmup": r.warmupSeconds=2; break;
            case "cap": r.maximumLongFrameEvents=100000; break;
            case "missing_phase": r.phases=r.phases.Take(2).ToArray(); break;
            case "null_phase": r.phases[1]=null!; break;
            case "phase_name": r.phases[1].phase="startup"; break;
            case "phase_origin": r.phases[1].startSeconds=3; break;
            case "startup_frames": r.phases[0].sampledFrames=1; break;
            case "warmup_end": r.phases[1].lastObservedSeconds=3; break;
            case "gameplay_end": r.phases[2].lastObservedSeconds=12; break;
            case "nan": r.profileInitializedSeconds=double.NaN; break;
            case "frames": r.phases[2].sampledFrames=99; break;
            case "average": r.phases[2].averageFrameMilliseconds=19; break;
            case "maximum": r.phases[2].maximumFrameMilliseconds=21; break;
            case "negative_gc": r.phases[2].gc0Collections=-1; break;
            case "memory": r.phases[2].peakAllocatedMemoryBytes=long.MaxValue; break;
            case "dropped": r.droppedLongFrameEvents=1; break;
            case "event_time": r.longFrames[0].endSeconds=double.NaN; break;
            case "event_duration": r.longFrames[0].frameMilliseconds=41; break;
            case "event_phase": r.longFrames[0].phase="gameplay"; break;
            case "event_crossing": r.longFrames[0].crossesWarmupBoundary=true; break;
            case "missing_worst": r.worstLongFrame=null!; break;
            case "wrong_worst": r.worstLongFrame=new ProfileLongFrame(); break;
            case "event_gc": r.longFrames[0].gc0Collections=-1; break;
            case "long_count": r.longFrameCount=0; break;
        }
        Assert.Throws<InvalidDataException>(()=>Validate(r));
    }
    [Theory][InlineData(double.NaN)][InlineData(double.PositiveInfinity)][InlineData(-1)]
    public void InvalidOriginsStopRatherThanSilentlyDroppingSamples(double origin) =>
        Assert.Throws<ArgumentException>(()=>new ProfileTimingCollector(0,origin,10,default,default));
    [Fact] public void BackwardsClockAndInvalidFrameStop()
    {
        var c=new ProfileTimingCollector(1,2,10,default,default);
        Assert.Throws<InvalidDataException>(()=>c.Record(1.9,16,default));
        Assert.Throws<InvalidDataException>(()=>c.Record(3,double.NaN,default));
    }
}
