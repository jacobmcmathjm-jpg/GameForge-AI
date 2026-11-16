using ForgeCore.Core;
namespace ForgeCore.Core.Tests;
public sealed class IndustrialPlacementAuditTests
{
    [Fact]public void LegacyOverlapIsReportedWithoutPretendingTheExistingGameWasChanged()
    {
        var r=IndustrialPlacementAudit.Assess(IndustrialPlacementLayout.Placements);
        Assert.False(r.NominalLayoutClear);Assert.Contains(r.Conflicts,c=>c.First=="supplies-2"&&c.Second=="containers-2");
        Assert.Contains(r.Conflicts,c=>c.First=="containers-1"&&c.Second=="fixed_service_divider");
        Assert.False(r.RuntimeNavigationVerified);Assert.False(r.PlacementApproved);Assert.All(r.Routes,c=>Assert.True(c.Reachable));
    }
    [Fact]public void RefinedPlanSeparatelyRemovesOverlapAndRetainsAllTenSlotsAndThreeNominalRoutes()
    {
        var plan=IndustrialPlacementLayout.For("refined_v1");var r=IndustrialPlacementAudit.Assess(plan);
        Assert.Equal(10,plan.Count);Assert.Empty(r.Conflicts);Assert.True(r.NominalLayoutClear);Assert.Equal(3,r.Routes.Count);
        Assert.All(plan,p=>Assert.True(IndustrialPropRecipes.Find(p.Recipe)!.IsRefined));Assert.All(r.Routes,p=>Assert.True(p.Reachable&&p.GridSteps>0));
        Assert.Equal(-1,IndustrialPlacementLayout.Placements.Single(p=>p.PlacementId=="supplies-2").Z);
        Assert.Equal(0,IndustrialPlacementLayout.Placements.Single(p=>p.PlacementId=="containers-1").Z);
        Assert.NotEqual(IndustrialPlacementLayout.Version,IndustrialPlacementLayout.VersionFor("refined_v1"));
    }
    [Theory][InlineData(double.NaN)][InlineData(double.PositiveInfinity)][InlineData(101)]
    public void InvalidCoordinatesFailClosed(double x)=>Assert.Throws<ArgumentException>(()=>IndustrialPlacementAudit.Assess([IndustrialPlacementLayout.Placements[0] with{X=x}]));
    [Fact]public void DuplicateUnknownRotatedAndOutOfBoundsFootprintsAreNotSilentlyAccepted()
    {
        var p=IndustrialPlacementLayout.Placements[0];Assert.Throws<ArgumentException>(()=>IndustrialPlacementAudit.Assess([p,p]));
        Assert.Throws<ArgumentException>(()=>IndustrialPlacementAudit.Assess([p with{Recipe="unknown"}]));
        Assert.Throws<ArgumentException>(()=>IndustrialPlacementAudit.Assess([p with{Width=0}]));
        Assert.Throws<ArgumentException>(()=>IndustrialPlacementLayout.For("unknown"));
        Assert.Contains(IndustrialPlacementAudit.Assess([p with{X=15.8}]).Conflicts,c=>c.Kind=="outside_playable_bounds");
        var rotated=IndustrialPlacementLayout.Placements.Single(p=>p.PlacementId=="containers-1") with{YawDegrees=45,X=15};
        Assert.Contains(IndustrialPlacementAudit.Assess([rotated]).Conflicts,c=>c.Kind=="outside_playable_bounds");
    }
    [Fact]public void ConservativeGridRejectsASealedRouteInsteadOfCallingItNavigationProof()
    {
        // Thin geometry between grid centres must block swept edges, not only occupied nodes.
        var walls=Enumerable.Range(0,3).Select(i=>IndustrialPlacementLayout.Placements[0] with{PlacementId="wall-"+i,X=-10+i*10,Z=-10,Width=11,Depth=.05}).ToArray();
        var result=IndustrialPlacementAudit.Assess(walls);Assert.False(result.Routes[0].Reachable);Assert.False(result.NominalLayoutClear);
        Assert.False(result.RuntimeNavigationVerified);Assert.False(result.PlacementApproved);
    }
}
