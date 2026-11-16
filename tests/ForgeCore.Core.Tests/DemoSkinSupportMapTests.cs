using ForgeCore.Demo;
namespace ForgeCore.Core.Tests;
public sealed class DemoSkinSupportMapTests
{
    [Theory][InlineData("Hips",1)][InlineData("Spine",1)][InlineData("Chest",1)][InlineData("Hand.L",2)][InlineData("Hand.R",3)][InlineData("hand.r",0)][InlineData("",0)][InlineData(null,0)]
    public void BoneNamesAreExact(string? name,byte role)=>Assert.Equal(role,DemoSkinSupportMap.Role(name!));
    [Fact]public void StrictThresholdAndInfluenceOrderArePreserved(){
        byte[] roles=[1,2,3,0];
        Assert.False(DemoSkinSupportMap.Evaluate(roles,0,1,2,3,.5f,.5f,0,0).torso);
        Assert.Equal(0,DemoSkinSupportMap.Evaluate(roles,0,1,2,3,.5f,.5f,0,0).hand);
        Assert.Equal(2,DemoSkinSupportMap.Evaluate(roles,1,2,0,3,.6f,.7f,0,0).hand);
        Assert.True(DemoSkinSupportMap.Evaluate(roles,0,0,2,3,.3f,.3f,0,0).torso);
    }
    [Fact]public void RandomWeightsMatchTheOriginalLoopExactly(){
        string[] names=["Hips","Spine","Chest","Hand.L","Hand.R","Foot.L","UpperArm.R"];
        var roles=names.Select(DemoSkinSupportMap.Role).ToArray();var rng=new Random(493);
        for(var n=0;n<10000;n++){
            var ids=Enumerable.Range(0,4).Select(_=>rng.Next(names.Length)).ToArray();
            var weights=Enumerable.Range(0,4).Select(_=>(float)rng.NextDouble()).ToArray();
            float sum=0;byte hand=0;
            for(var j=0;j<4;j++){var name=names[ids[j]];if(name=="Hips"||name=="Spine"||name=="Chest")sum+=weights[j];if(weights[j]>.5f){if(name=="Hand.L")hand=1;if(name=="Hand.R")hand=2;}}
            var actual=DemoSkinSupportMap.Evaluate(roles,ids[0],ids[1],ids[2],ids[3],weights[0],weights[1],weights[2],weights[3]);
            Assert.Equal(sum>.5f,actual.torso);Assert.Equal(hand,actual.hand);
        }
    }
    [Fact]public void SteadyEvaluationAllocatesNoManagedObjects(){
        byte[] roles=[1,2,3,0];var value=0;
        for(var n=0;n<1000;n++)value+=DemoSkinSupportMap.Evaluate(roles,0,1,2,3,.6f,.4f,0,0).torso?1:0;
        var before=GC.GetAllocatedBytesForCurrentThread();
        for(var n=0;n<10000;n++)value+=DemoSkinSupportMap.Evaluate(roles,0,1,2,3,.6f,.4f,0,0).torso?1:0;
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(0,allocated);Assert.Equal(11000,value);
    }
    [Theory][InlineData(-1)][InlineData(1)]public void InvalidIndicesFail(int i)=>Assert.Throws<ArgumentOutOfRangeException>(()=>DemoSkinSupportMap.Evaluate([0],i,0,0,0,0,0,0,0));
}
