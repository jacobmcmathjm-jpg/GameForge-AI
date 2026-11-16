using ForgeCore.Demo;
namespace ForgeCore.Core.Tests;
public sealed class DemoStateTests
{
    private static DemoCheckpoint Valid(int stage=0)=>new(){schemaVersion=1,profile=DemoStateRules.Profile,stage=stage,defeatedMask=stage==2?3:0,health=83,reserve=54,magazine=4,x=-10,y=.03f,z=-8.8f,yaw=0,pitch=0};
    [Fact]public void DefaultSettingsAndAllLegalCheckpointStagesValidate(){Assert.True(DemoStateRules.SettingsValid(new()));foreach(var s in new[]{0,1,2})Assert.True(DemoStateRules.CheckpointValid(Valid(s)));}
    [Theory][InlineData(float.NaN)][InlineData(float.PositiveInfinity)][InlineData(-1)][InlineData(10)]
    public void InvalidSensitivityFailsClosed(float value)=>Assert.False(DemoStateRules.SettingsValid(new(){sensitivity=value}));
    [Fact]public void BadSchemaProfileAmmoHealthPositionAndStageCannotRestore()
    {
        foreach(var mutate in new Action<DemoCheckpoint>[] {c=>c.schemaVersion=2,c=>c.profile="other-game",c=>c.magazine=7,c=>c.reserve=61,c=>c.health=0,c=>c.x=100,c=>c.y=float.NaN,c=>c.stage=3,c=>c.defeatedMask=4}){var c=Valid();mutate(c);Assert.False(DemoStateRules.CheckpointValid(c));}
        var gated=Valid(2);gated.defeatedMask=0;Assert.False(DemoStateRules.CheckpointValid(gated));Assert.False(DemoStateRules.CheckpointValid(new()));
    }
    [Theory][InlineData("shot")][InlineData("impact")][InlineData("footstep")][InlineData("reload")][InlineData("ui")][InlineData("ambient")]
    public void OriginalAudioIsDeterministicFiniteBoundedAndNonSilent(string kind)
    {
        var a=DemoAudioSynthesis.Generate(kind);Assert.Equal(a,DemoAudioSynthesis.Generate(kind));
        Assert.All(a,x=>Assert.True(float.IsFinite(x)&&Math.Abs(x)<=.87f));Assert.InRange(a.Length,4800,192000);
        Assert.True(Math.Sqrt(a.Average(x=>(double)x*x))>.001);Assert.Equal(48000,DemoAudioSynthesis.SampleRate);
    }
    [Fact]public void UnknownAudioKindIsNotAnArbitraryGenerator()=>Assert.Throws<ArgumentException>(()=>DemoAudioSynthesis.Generate("external/path"));
}
