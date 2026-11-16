using ForgeCore.Demo;
namespace ForgeCore.Core.Tests;
public sealed class LayeredAudioTests
{
    [Fact]public void FullBankHasFourShotAndFourVariantsOnEverySurfaceWithinPcmBudget()
    {
        Assert.Equal(28,DemoAudioSynthesis.ClipKeys.Length);Assert.Equal(28,DemoAudioSynthesis.ClipKeys.Distinct().Count());long bytes=0;
        foreach(var key in DemoAudioSynthesis.ClipKeys){var pcm=DemoAudioSynthesis.Generate(key);Assert.Equal(pcm,DemoAudioSynthesis.Generate(key));
            Assert.All(pcm,s=>Assert.True(float.IsFinite(s)&&Math.Abs(s)<.87));Assert.Equal(0,pcm[0]);Assert.InRange(Math.Abs(pcm[^1]),0,.001);bytes+=44+pcm.Length*2;}
        Assert.InRange(bytes,500000,2*1024*1024);
        foreach(var surface in Enum.GetValues<DemoFootstepSurface>()){
            var variants=Enumerable.Range(0,4).Select(v=>DemoAudioSynthesis.Generate(DemoAudioSynthesis.Key("footstep",surface,v))).ToArray();
            for(var i=1;i<4;i++)Assert.False(variants[i].SequenceEqual(variants[i-1]));
        }
    }
    [Fact]public void GunIsAnImpulsiveBroadbandCandidateRatherThanTheOldBassNote()
    {
        var x=DemoAudioSynthesis.Generate("shot");Assert.Equal(28800,x.Length);
        static double Energy(float[] data,int a,int b)=>data.Skip(a).Take(b-a).Average(s=>(double)s*s);
        Assert.True(Energy(x,0,1920)>Energy(x,14400,21600)*10);
        var lag=667;double cross=0,powerA=0,powerB=0;for(var i=lag;i<4800;i++){cross+=x[i]*x[i-lag];powerA+=x[i]*x[i];powerB+=x[i-lag]*x[i-lag];}
        Assert.InRange(Math.Abs(cross/Math.Sqrt(powerA*powerB)),0,.3);
        Assert.False(x.SequenceEqual(DemoAudioSynthesis.Generate("shot-1")));
    }
    [Theory][InlineData(30)][InlineData(60)][InlineData(120)]
    public void MeasuredGroundedStepsHaveBoundedConsistentCadence(int rate)
    {
        var walk=new DemoFootstepClock();var sprint=new DemoFootstepClock();
        for(var i=0;i<rate*10;i++){walk.Advance(4f/rate,1f/rate,true,true,1.7f);sprint.Advance(6.5f/rate,1f/rate,true,true,2.2f);}
        Assert.InRange(walk.Steps,22,24);Assert.InRange(sprint.Steps,28,30);Assert.True(sprint.Steps>walk.Steps);
    }
    [Fact]public void NoStepsFromPauseAirIdleTeleportOrZeroTime()
    {
        var clock=new DemoFootstepClock();for(var i=0;i<500;i++){
            Assert.False(clock.Advance(.1f,.02f,false,true,1));Assert.False(clock.Advance(.1f,.02f,true,false,1));
            Assert.False(clock.Advance(0,.02f,true,true,1));Assert.False(clock.Advance(20,.02f,true,true,1));Assert.False(clock.Advance(.1f,0,true,true,1));}
        Assert.Equal(0,clock.Steps);Assert.Throws<ArgumentOutOfRangeException>(()=>clock.Advance(float.NaN,.1f,true,true,1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>DemoAudioSynthesis.Key("shot",DemoFootstepSurface.Asphalt,4));
        Assert.Throws<ArgumentException>(()=>DemoAudioSynthesis.Generate("footstep-unknown-0"));
    }
}
