using ForgeCore.Core;
namespace ForgeCore.Core.Tests;
public sealed class IndustrialPlacementLayoutTests
{
    [Fact]public void FootprintsMatchAllTenAuthoredSlotsWithoutInventingScaleOrInteriorAccess()
    {
        var placements=IndustrialPlacementLayout.Placements;
        Assert.Equal(10,placements.Count);Assert.Equal(10,placements.Select(p=>p.PlacementId).Distinct().Count());
        foreach(var slot in IndustrialAssetPlan.Slots) {
            var matches=placements.Where(p=>p.SlotId==slot.Id).ToArray();Assert.Equal(slot.Count,matches.Length);
            foreach(var p in matches) {
                Assert.Equal(slot.Recipe,p.Recipe);Assert.Equal(1,p.UniformScale);Assert.Equal("bottom_center",p.Pivot);
                Assert.Equal(0,p.Y);Assert.Equal(0,p.YawDegrees);
                Assert.InRange(p.X-p.Width/2,-16,16);Assert.InRange(p.X+p.Width/2,-16,16);
                Assert.InRange(p.Z-p.Depth/2,-16,16);Assert.InRange(p.Z+p.Depth/2,-16,16);
                var r=IndustrialPropRecipes.Find(p.Recipe)!;Assert.True(IndustrialPlacementLayout.FitsSource(r.Id,r.DefaultHeight,r.IsBarrel?340:0));
            }
        }
    }
    [Theory][InlineData(250)][InlineData(339)][InlineData(341)][InlineData(500)]
    public void SameHeightBarrelsCannotSilentlyExceedOrAlterReservedFootprint(int radius)
    {Assert.False(IndustrialPlacementLayout.FitsSource("industrial_barrel_v2",1100,radius));}
    [Fact]public void UnsupportedRecipesHeightChangesAndUnexpectedRadiusFailClosed()
    {
        Assert.False(IndustrialPlacementLayout.FitsSource("unknown",1100,340));
        Assert.False(IndustrialPlacementLayout.FitsSource("wooden_crate_v1",900,0));
        Assert.False(IndustrialPlacementLayout.FitsSource("wooden_crate_v1",800,340));
        Assert.True(IndustrialPlacementLayout.FitsSource("wooden_crate_v1",800,0));
    }
}
