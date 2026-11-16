using ForgeCore.Demo;
namespace ForgeCore.Core.Tests;
public sealed class DemoTimingTraceTests
{
    [Fact]public void TwoSecondWarmupAndReadyLatencyAreNotInventedStartupFrames(){
        var t=new DemoTimingTrace(500,default);t.Ready(600);t.BeginWindow(1,"gameplay");t.Observe(20,10,"warmup",false,default);
        var r=t.Snapshot();Assert.Equal(2000,r.warmupMilliseconds);Assert.Equal(500,r.unityAwakeMilliseconds);Assert.Equal(600,r.unityReadyMilliseconds);
        Assert.Equal(0,r.phases[0].frames);Assert.Equal(1,r.phases[1].frames);Assert.False(r.wholeSessionSmoothnessAccepted);Assert.False(r.measuredGameplayMaximumPassed);
    }
    [Fact]public void WarmupBoundaryHitchRemainsInGameplayAndWholeMaximum(){
        var t=new DemoTimingTrace(0,default);t.BeginWindow(0,"gameplay");t.Observe(1800,1800,"warmup",false,default);
        t.Observe(3527.84,1727.84,"gameplay",true,new(){gc0=1,managedBytes=99,allocatedBytes=100});
        var r=t.Snapshot();var e=r.events.Last();Assert.True(e.warmupBoundary);Assert.Equal(1800,e.startMilliseconds);Assert.Equal(3527.84,e.endMilliseconds);
        Assert.Equal(1727.84,e.wallMilliseconds,5);Assert.Equal(1,e.gc0);Assert.Equal(99,e.managedAfterBytes);Assert.False(r.measuredGameplayMaximumPassed);
    }
    [Fact]public void LaterWorstAndCountsSurviveTheFirstEventCap(){
        var t=new DemoTimingTrace(0,default);t.BeginWindow(0,"gameplay");
        for(var i=1;i<=500;i++)t.Observe(i*100,100,"gameplay",false,default);
        t.Observe(52000,2000,"gameplay",false,default);var r=t.Snapshot();
        Assert.Equal(64,r.events.Length);Assert.Equal(501,r.longFrames);Assert.Equal(437,r.droppedLongEvents);Assert.Equal(2000,r.worstByPhase.Single().wallMilliseconds);
        Assert.Equal(501,r.windows.Single().frames);Assert.False(r.measuredGameplayMaximumPassed);
    }
    [Fact]public void SupplementaryUnityDeltaDoesNotBecomeAFabricatedStopwatchGap(){
        var t=new DemoTimingTrace(100,default);t.Ready(200);t.BeginWindow(0,"gameplay");t.Observe(16,2500,"warmup",false,default);
        var e=t.Snapshot().events.Single();Assert.Equal(16,e.wallMilliseconds);Assert.Equal(2500,e.unityMilliseconds);Assert.Equal(0,e.startMilliseconds);
    }
    [Theory][InlineData(100,true)][InlineData(100.001,false)]public void ProvisionalMaximumUsesExactBoundary(double ms,bool pass){
        var t=new DemoTimingTrace(0,default);t.BeginWindow(0,"gameplay");t.Observe(ms,ms,"gameplay",false,default);Assert.Equal(pass,t.Snapshot().measuredGameplayMaximumPassed);
    }
    [Fact]public void WarmupAndIdleDoNotImplyWholeSessionAcceptance(){
        var t=new DemoTimingTrace(0,default);t.BeginWindow(0,"gameplay");t.Observe(2000,2000,"warmup",false,default);t.Observe(2016,16,"gameplay",true,default);
        t.BeginWindow(2016,"idle");t.Observe(4016,2000,"idle",false,default);var r=t.Snapshot();
        Assert.True(r.measuredGameplayMaximumPassed);Assert.False(r.wholeSessionSmoothnessAccepted);Assert.Equal(2,r.windows.Length);Assert.Equal(2000,r.windows[1].maximumWallMilliseconds);
    }
    [Fact]public void WorkContextIsBoundedAndExcessCountIsExplicit(){
        var t=new DemoTimingTrace(0,default);var work=new DemoWorkSample[20];t.Observe(200,200,"warmup",false,default,work);
        var e=t.Snapshot().events.Single();Assert.Equal(8,e.overlappingWork.Length);Assert.Equal(12,e.droppedWorkSamples);
    }
    [Fact]public void PausedIntervalsAreRecordedButDoNotEnterGameplayWindow(){
        var t=new DemoTimingTrace(0,default);t.BeginWindow(0,"gameplay");t.Observe(5000,5000,"paused",false,default);t.Observe(5016,16,"gameplay",false,default);
        Assert.Equal(16,t.Snapshot().windows.Single().maximumWallMilliseconds);Assert.Equal("paused",t.Snapshot().events.Single().phase);
    }
    [Fact]public void WindowOverflowIsNotAQuietPass(){
        var t=new DemoTimingTrace(0,default);for(var i=0;i<17;i++)t.BeginWindow(0,"gameplay");var r=t.Snapshot();Assert.Equal(16,r.windows.Length);Assert.Equal(1,r.droppedWindows);Assert.True(r.truncated);Assert.False(r.measuredGameplayMaximumPassed);
    }
    [Fact]public void SampleOverflowIsBoundedAndExplicit(){
        var t=new DemoTimingTrace(0,default);for(var i=0;i<=DemoTimingTrace.MaximumFrames;i++)t.Observe(i*.1,0,"paused",false,default);
        var r=t.Snapshot();Assert.Equal(DemoTimingTrace.MaximumFrames,r.observedFrames);Assert.Equal(1,r.droppedFrames);Assert.True(r.truncated);
    }
    [Theory][InlineData(double.NaN)][InlineData(double.PositiveInfinity)][InlineData(-1)]public void InvalidClocksStop(double now)=>Assert.Throws<ArgumentOutOfRangeException>(()=>new DemoTimingTrace(0,default).Observe(now,16,"gameplay",false,default));
    [Fact]public void InvalidPhaseAndReversedClockStop(){var t=new DemoTimingTrace(0,default);t.Observe(100,10,"warmup",false,default);Assert.Throws<ArgumentOutOfRangeException>(()=>t.Observe(99,10,"warmup",false,default));Assert.Throws<ArgumentException>(()=>t.Observe(101,10,"unknown",false,default));}
    [Fact]public void LargeSupplementaryDeltaCannotHideLaterWorstWallInterval(){
        var t=new DemoTimingTrace(0,default);t.BeginWindow(0,"gameplay");t.Observe(16,2500,"gameplay",false,default);
        for(var i=1;i<64;i++)t.Observe(16+i*40,40,"gameplay",false,default);
        t.Observe(3036,500,"gameplay",false,default);var r=t.Snapshot();
        Assert.Equal(500,r.worstByPhase.Single().wallMilliseconds);Assert.Equal(1,r.phases[2].over100);
        Assert.Equal(2,r.phases[2].supplementaryUnityOver100);Assert.False(r.measuredGameplayMaximumPassed);
    }
    [Fact]public void InvalidMemoryContextsStopAtBothBoundaries(){
        Assert.Throws<ArgumentOutOfRangeException>(()=>new DemoTimingTrace(0,new(){managedBytes=-1}));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new DemoTimingTrace(0,default).Observe(16,16,"warmup",false,new(){allocatedBytes=long.MaxValue}));
    }
}
