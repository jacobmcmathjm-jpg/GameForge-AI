using ForgeCore.Demo;
namespace ForgeCore.Core.Tests;
public sealed class DemoFrameWindowTests
{
    [Fact]public void EmptyWindowAndNearestRankPercentilesAreExplicit(){var w=new DemoFrameWindow();Assert.Equal(0,w.Snapshot().p95);foreach(var v in new[]{10d,20,30,40,110})w.Add(v);var s=w.Snapshot();Assert.Equal(30,s.p50);Assert.Equal(110,s.p95);Assert.Equal(2,s.over33);Assert.Equal(1,s.over100);Assert.Equal(210,s.measuredMilliseconds);}
    [Fact]public void RingIsBoundedWhileWholeRunSpikeAndTotalsAreRetained(){var w=new DemoFrameWindow();w.Add(150);for(var i=0;i<5000;i++)w.Add(10);var s=w.Snapshot();Assert.Equal(4096,s.windowFrames);Assert.Equal(5001,s.totalFrames);Assert.Equal(10,s.p99);Assert.Equal(150,s.maximum);Assert.Equal(1,s.over100);}
    [Theory][InlineData(double.NaN)][InlineData(double.PositiveInfinity)][InlineData(0)][InlineData(-1)][InlineData(120001)]public void InvalidFramesDoNotEnterMetrics(double value)=>Assert.Throws<ArgumentOutOfRangeException>(()=>new DemoFrameWindow().Add(value));
}
