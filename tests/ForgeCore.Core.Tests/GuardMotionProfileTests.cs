using ForgeCore.Motion;

namespace ForgeCore.Core.Tests;

public sealed class GuardMotionProfileTests
{
    [Theory]
    [InlineData(GuardMotionClip.Idle)][InlineData(GuardMotionClip.Walk)]
    [InlineData(GuardMotionClip.Run)][InlineData(GuardMotionClip.Aim)]
    [InlineData(GuardMotionClip.Fire)][InlineData(GuardMotionClip.Reload)]
    [InlineData(GuardMotionClip.Hit)][InlineData(GuardMotionClip.Death)]
    public void EveryActionHasFiniteBoundedSamples(GuardMotionClip clip)
    {
        for(var i=0;i<=240;i++)
        {
            var p=GuardMotionProfile.Sample(clip,i/240f);
            foreach(var field in typeof(GuardMotionSample).GetFields())
            {
                var value=(float)field.GetValue(p)!;
                Assert.True(float.IsFinite(value),field.Name);
                Assert.InRange(value,-90,90);
            }
            Assert.InRange(p.PelvisHeight,-.73f,.01f);
            Assert.InRange(p.ReloadReach,0,1.00001f);
            Assert.InRange(p.Recoil,0,1.00001f);
        }
    }
    [Theory]
    [InlineData(GuardMotionClip.Idle)][InlineData(GuardMotionClip.Walk)]
    [InlineData(GuardMotionClip.Run)][InlineData(GuardMotionClip.Aim)]
    public void LoopingBodyChannelsHaveNoEndpointSeam(GuardMotionClip clip)
    {
        var first=GuardMotionProfile.Sample(clip,0);var last=GuardMotionProfile.Sample(clip,1);
        foreach(var field in typeof(GuardMotionSample).GetFields())
            Assert.InRange(Math.Abs((float)field.GetValue(first)!-(float)field.GetValue(last)!),0,.00002f);
    }
    [Theory][InlineData(GuardMotionClip.Fire)][InlineData(GuardMotionClip.Reload)][InlineData(GuardMotionClip.Hit)]
    public void NonlethalActionsReturnToNeutralInsteadOfFreezingAnOffset(GuardMotionClip clip)
    {
        foreach(var time in new[]{0f,1f})
        {
            var p=GuardMotionProfile.Sample(clip,time);
            foreach(var field in typeof(GuardMotionSample).GetFields())
                Assert.InRange(Math.Abs((float)field.GetValue(p)!),0,.00001f);
        }
    }
    [Fact]public void FireRecoilPeaksAtTheExistingShotMarker()
    {
        var marker=.08f/.25f;
        var peak=GuardMotionProfile.Sample(GuardMotionClip.Fire,marker).Recoil;
        Assert.InRange(peak,.999f,1.001f);
        for(var i=0;i<=240;i++)Assert.True(GuardMotionProfile.Sample(GuardMotionClip.Fire,i/240f).Recoil<=peak+.00001f);
    }
    [Fact]public void ReloadReachesBeforeWithdrawalHoldsMagazineAndReturnsBeforeCommit()
    {
        Assert.Equal(0,GuardMotionProfile.ReloadContact(0));
        Assert.True(GuardMotionProfile.ReloadContact(.12f)>.2f);
        foreach(var seconds in new[]{.3f,.56f,.92f,1.28f})Assert.InRange(GuardMotionProfile.ReloadContact(seconds),.999f,1.001f);
        Assert.InRange(GuardMotionProfile.ReloadContact(1.5f),.01f,.2f);
        Assert.Equal(0,GuardMotionProfile.ReloadContact(1.6f));
        Assert.Equal(0,GuardMotionProfile.ReloadContact(10));
    }
    [Fact]public void DeathFallsAndSettlesRatherThanLoopingOrReturningToStanding()
    {
        var previous=GuardMotionProfile.Sample(GuardMotionClip.Death,0);
        for(var i=1;i<=240;i++)
        {
            var p=GuardMotionProfile.Sample(GuardMotionClip.Death,i/240f);
            Assert.True(p.PelvisHeight<=previous.PelvisHeight+.00001f);
            if(i/240f>=.3f)Assert.True(p.PelvisPitch>=previous.PelvisPitch-.00001f);
            previous=p;
        }
        Assert.InRange(previous.PelvisHeight,-.721f,-.719f);
        Assert.InRange(previous.PelvisPitch,77.99f,78.01f);
        Assert.True(previous.KneeFold>0);
        Assert.True(GuardMotionProfile.Sample(GuardMotionClip.Death,.18f).PelvisPitch<-.5f,"Initial impact reaction must precede the forward collapse.");
    }
    [Fact]public void FootLiftAndTravelEaseHaveSoftTakeoffAndLanding()
    {
        const float h=.0001f;
        Assert.Equal(0,GuardMotionProfile.SwingLift(0));
        Assert.InRange(GuardMotionProfile.SwingLift(1),0,.000001f);
        Assert.InRange(GuardMotionProfile.SwingLift(.5f),.999f,1.001f);
        Assert.InRange(GuardMotionProfile.SwingLift(h)/h,0,.002f);
        Assert.InRange(GuardMotionProfile.SwingLift(1-h)/h,0,.002f);
        Assert.InRange(GuardMotionProfile.Ease(h)/h,0,.00001f);
        Assert.Equal(0,GuardMotionProfile.Ease(-1));Assert.Equal(1,GuardMotionProfile.Ease(2));
    }
    [Theory][InlineData(float.NaN)][InlineData(float.PositiveInfinity)][InlineData(float.NegativeInfinity)]
    public void NonfiniteTimesAreRejected(float time)
    {
        Assert.Throws<ArgumentException>(()=>GuardMotionProfile.Sample(GuardMotionClip.Idle,time));
        Assert.Throws<ArgumentException>(()=>GuardMotionProfile.ReloadContact(time));
        Assert.Throws<ArgumentException>(()=>GuardMotionProfile.SwingLift(time));
        Assert.Throws<ArgumentException>(()=>GuardMotionProfile.Ease(time));
    }
    [Fact]public void UnsupportedClipsAndOutOfRangeSampleTimesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>GuardMotionProfile.Sample((GuardMotionClip)8,0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>GuardMotionProfile.Sample(GuardMotionClip.Idle,-.1f));
        Assert.Throws<ArgumentOutOfRangeException>(()=>GuardMotionProfile.Sample(GuardMotionClip.Idle,1.1f));
        Assert.Throws<ArgumentOutOfRangeException>(()=>GuardMotionProfile.ReloadContact(-1));
    }
}
