using ForgeCore.Demo;
namespace ForgeCore.Core.Tests;
public sealed class DemoWorkAndMixTests
{
    [Fact]public void LegacySettingsMigrationPreservesUserValuesAndDoesNotMuteNewBuses()
    {
        var legacy=new DemoSettings{schemaVersion=1,sensitivity=2.3f,fieldOfView=80,masterVolume=.4f,invertY=true,quality=1,frameLimit=120,
            effectsVolume=0,footstepsVolume=0,ambienceVolume=0,interfaceVolume=0};
        var current=DemoStateRules.UpgradeSettings(legacy);Assert.True(DemoStateRules.SettingsValid(current));Assert.Equal(1,legacy.schemaVersion);
        Assert.Equal(2,current.schemaVersion);Assert.Equal(2.3f,current.sensitivity);Assert.Equal(.4f,current.masterVolume);Assert.True(current.invertY);
        foreach(var kind in new[]{"shot","footstep","ambient","ui"})Assert.Equal(1,DemoStateRules.AudioGain(current,kind));
        legacy.sensitivity=float.NaN;Assert.False(DemoStateRules.SettingsValid(DemoStateRules.UpgradeSettings(legacy)));
        Assert.False(DemoStateRules.SettingsValid(new DemoSettings{schemaVersion=3}));
    }
    [Theory][InlineData("shot")][InlineData("footstep")][InlineData("ambient")][InlineData("ui")]
    public void IndependentBusesAreBoundedAndDefaultsPreserveAcceptedLevels(string kind)
    {
        var settings=new DemoSettings();Assert.Equal(1,DemoStateRules.AudioGain(settings,kind));
        foreach(var bad in new[]{float.NaN,float.PositiveInfinity,-.1f,1.1f}){
            var s=new DemoSettings();if(kind=="shot")s.effectsVolume=bad;else if(kind=="footstep")s.footstepsVolume=bad;else if(kind=="ambient")s.ambienceVolume=bad;else s.interfaceVolume=bad;
            Assert.False(DemoStateRules.SettingsValid(s));Assert.InRange(DemoStateRules.AudioGain(s,kind),0,1);
        }
        settings.footstepsVolume=.2f;Assert.Equal(.2f,DemoStateRules.AudioGain(settings,"footstep"));Assert.Equal(1,DemoStateRules.AudioGain(settings,"shot"));
    }
    [Fact]public void JournalRetentionIsBoundedButTotalsPreserveAllMeasuredWork()
    {
        var journal=new DemoWorkJournal();for(var i=0;i<1000;i++)journal.Add(DemoWorkKind.StateFlush,i,i*10,2);
        var s=journal.Snapshot();Assert.Equal(1000,s.totalSamples);Assert.Equal(256,s.recent.Length);Assert.Equal(744,s.recent[0].frame);Assert.Equal(999,s.recent[^1].frame);
        var flush=s.statistics.Single(x=>x.kind=="StateFlush");Assert.Equal(1000,flush.count);Assert.Equal(2000,flush.totalMilliseconds);Assert.Equal(2,flush.maximumMilliseconds);
        s.recent[0].frame=-1;Assert.Equal(744,journal.Snapshot().recent[0].frame);
    }
    [Fact]public void NestedOverlapsAreReportedAsOverlapsNotSummedExclusiveCpuTime()
    {
        var journal=new DemoWorkJournal();journal.Add(DemoWorkKind.StateFlush,12,11,80);journal.Add(DemoWorkKind.StateWrite,12,10,85);
        Assert.Equal(2,journal.Overlapping(0,100).Length);Assert.Empty(journal.Overlapping(95,110));
        Assert.Single(journal.Overlapping(91,95));Assert.Throws<ArgumentOutOfRangeException>(()=>journal.Overlapping(2,1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>journal.Add((DemoWorkKind)99,0,0,1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>journal.Add(DemoWorkKind.StateWrite,-1,0,1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>journal.Add(DemoWorkKind.StateWrite,0,double.NaN,1));
    }
}
