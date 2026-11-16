using ForgeCore.Demo;
namespace ForgeCore.Core.Tests;
public sealed class RecordedFootstepTests
{
    [Fact]public void AllSixRecordedTakesAreSelectedWithoutChangingSyntheticGunshots()
    {
        var keys=Enumerable.Range(0,6).Select(i=>RecordedFootstepBank.Key(DemoFootstepSurface.Asphalt,i)).ToArray();
        Assert.Equal(6,keys.Distinct().Count());Assert.Equal(RecordedFootstepBank.Keys.Order(),keys.Order());
        Assert.Equal(keys,Enumerable.Range(0,6).Select(i=>RecordedFootstepBank.Key(DemoFootstepSurface.Concrete,i)));
        Assert.Throws<ArgumentException>(()=>RecordedFootstepBank.Key(DemoFootstepSurface.Metal,0));
        Assert.Equal("forgecore_layered_surface_audio_v2",DemoAudioSynthesis.Recipe);Assert.Equal(28,DemoAudioSynthesis.ClipKeys.Length);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public void BoundedProcessorPreservesLengthFadesAndDoesNotChangeInput(int index)
    {
        var input=Enumerable.Range(0,RecordedFootstepBank.SourceFrames[index]).Select(i=>(float)(.05+.7*Math.Sin(i*.13))).ToArray();var original=input.ToArray();
        var processed=RecordedFootstepBank.Process(index,input,48000,1);
        Assert.Equal(original,input);Assert.Equal(input.Length,processed.Length);Assert.Equal(0,processed[0]);Assert.Equal(0,processed[^1]);
        Assert.All(processed,x=>Assert.True(float.IsFinite(x)&&Math.Abs(x)<.551));Assert.Contains(processed,x=>Math.Abs(x)>.1);
        Assert.Equal(processed,RecordedFootstepBank.Process(index,input,48000,1));
    }
    [Fact]public void DecoderFormatSilenceNonFiniteAndWrongIdentityFailClosed()
    {
        var data=new float[RecordedFootstepBank.SourceFrames[0]];
        Assert.Throws<ArgumentException>(()=>RecordedFootstepBank.Process(0,data,48000,1));
        data[1]=.5f;Assert.Throws<ArgumentException>(()=>RecordedFootstepBank.Process(0,data,44100,1));
        Assert.Throws<ArgumentException>(()=>RecordedFootstepBank.Process(0,data,48000,2));
        Assert.Throws<ArgumentException>(()=>RecordedFootstepBank.Process(0,data[..^1],48000,1));
        data[2]=float.NaN;Assert.Throws<ArgumentException>(()=>RecordedFootstepBank.Process(0,data,48000,1));
        Assert.False(RecordedFootstepBank.MatchesSource(0,new byte[100]));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RecordedFootstepBank.MatchesSource(6,new byte[100]));
    }
}
