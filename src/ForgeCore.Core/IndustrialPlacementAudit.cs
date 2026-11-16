namespace ForgeCore.Core;

public sealed record IndustrialPlacementConflict(string First,string Second,string Kind);
public sealed record IndustrialRouteCheck(string From,string To,bool Reachable,int GridSteps);
public sealed record IndustrialPlacementAuditResult(string Algorithm,int PlacementCount,
    IReadOnlyList<IndustrialPlacementConflict> Conflicts,IReadOnlyList<IndustrialRouteCheck> Routes,
    bool NominalLayoutClear,bool RuntimeNavigationVerified=false,bool PlacementApproved=false)
{
    public string Qualification=>"Read-only conservative nominal 2D solid-box/grid check, including fixed divider and terminal cabinets. Not measured prefab geometry, Unity physics/NavMesh, combat balance, art acceptance or permission to place assets.";
}

/// <summary>Bounded host-side planning check only; real measured prefab collision and Unity runtime tests remain mandatory.</summary>
public static class IndustrialPlacementAudit
{
    private sealed record Box(string Id,double X,double Z,double HalfX,double HalfZ);
    public static IndustrialPlacementAuditResult Assess(IReadOnlyList<IndustrialPlacementFootprint> placements)
    {
        if(placements.Count is <1 or >100)throw new ArgumentException("Audit requires 1-100 bounded footprints.");
        var boxes=new List<Box>();var conflicts=new List<IndustrialPlacementConflict>();
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var p in placements) {
            if(string.IsNullOrWhiteSpace(p.PlacementId)||p.PlacementId.Length>96||!ids.Add(p.PlacementId)||
                IndustrialPropRecipes.Find(p.Recipe) is null||
                new[]{p.X,p.Y,p.Z,p.YawDegrees,p.Width,p.Height,p.Depth}.Any(n=>!double.IsFinite(n))||
                Math.Abs(p.X)>100||Math.Abs(p.Z)>100||p.Y!=0||Math.Abs(p.YawDegrees)>360||
                p.Width is <=0 or >20||p.Depth is <=0 or >20||p.Height is <=0 or >10)
                throw new ArgumentException("Invalid, duplicated, nonfinite or unbounded placement footprint.");
            var angle=p.YawDegrees*Math.PI/180;var c=Math.Abs(Math.Cos(angle));var s=Math.Abs(Math.Sin(angle));
            // Conservative AABB of a rotated solid collider, plus 6mm source measurement tolerance.
            var b=new Box(p.PlacementId,p.X,p.Z,(p.Width*c+p.Depth*s)/2+.006,(p.Width*s+p.Depth*c)/2+.006);
            if(Math.Abs(b.X)+b.HalfX>16||Math.Abs(b.Z)+b.HalfZ>16)conflicts.Add(new(b.Id,"world_boundary","outside_playable_bounds"));
            boxes.Add(b);
        }
        var propCount=boxes.Count;
        boxes.AddRange([new("fixed_service_divider",-2,2,6,.4),new("fixed_power_cabinet",-10,-9.5,.4,.3),
            new("fixed_manifest_terminal",-10,9.5,.4,.3),new("fixed_extraction_terminal",10,13.5,.4,.3)]);
        for(var i=0;i<propCount;i++)for(var j=i+1;j<boxes.Count;j++) {
            var a=boxes[i];var b=boxes[j];
            if(Math.Abs(a.X-b.X)<a.HalfX+b.HalfX&&Math.Abs(a.Z-b.Z)<a.HalfZ+b.HalfZ)
                conflicts.Add(new(a.Id,b.Id,"conservative_solid_box_overlap"));
        }
        const int side=32;const double clearance=.39;var blocked=new bool[side*side];
        for(var z=0;z<side;z++)for(var x=0;x<side;x++) {
            var px=x-15.5;var pz=z-15.5;
            blocked[z*side+x]=boxes.Any(b=>Math.Abs(px-b.X)<=b.HalfX+clearance&&Math.Abs(pz-b.Z)<=b.HalfZ+clearance);
        }
        var checkpoints=new[]{("spawn",0d,-12d),("power_approach",-10d,-8.8),("manifest_approach",-10d,8.8),("extraction_approach",10d,12.8)};
        var routes=new List<IndustrialRouteCheck>();
        for(var i=1;i<checkpoints.Length;i++) {
            var a=checkpoints[i-1];var b=checkpoints[i];var start=Cell(a.Item2,a.Item3);var finish=Cell(b.Item2,b.Item3);
            var distance=Enumerable.Repeat(-1,side*side).ToArray();var queue=new Queue<int>();
            if(!blocked[start]&&!blocked[finish]&&SegmentClear(a.Item2,a.Item3,start%side-15.5,start/side-15.5)&&
                SegmentClear(finish%side-15.5,finish/side-15.5,b.Item2,b.Item3)){distance[start]=0;queue.Enqueue(start);}
            while(queue.Count>0&&distance[finish]<0) {
                var current=queue.Dequeue();var x=current%side;var z=current/side;
                foreach(var(dx,dz)in new[]{(1,0),(0,1),(-1,0),(0,-1)}) {
                    var nx=x+dx;var nz=z+dz;if(nx<0||nz<0||nx>=side||nz>=side)continue;var next=nz*side+nx;
                    if(blocked[next]||distance[next]>=0||!SegmentClear(x-15.5,z-15.5,nx-15.5,nz-15.5))continue;
                    distance[next]=distance[current]+1;queue.Enqueue(next);
                }
            }
            routes.Add(new(a.Item1,b.Item1,distance[finish]>=0,Math.Max(0,distance[finish])));
        }
        return new("nominal_solid_aabb_6mm_grid1m_clearance039_v1",placements.Count,conflicts.AsReadOnly(),routes.AsReadOnly(),conflicts.Count==0&&routes.All(r=>r.Reachable));
        static int Cell(double x,double z)=>(int)Math.Round(z+15.5,MidpointRounding.AwayFromZero)*side+(int)Math.Round(x+15.5,MidpointRounding.AwayFromZero);
        bool SegmentClear(double ax,double az,double bx,double bz)
        {
            if(new[]{ax,az,bx,bz}.Any(v=>Math.Abs(v)+clearance>16))return false;
            foreach(var box in boxes) {
                double enter=0,leave=1;
                if(Clip(ax,bx-ax,box.X-box.HalfX-clearance,box.X+box.HalfX+clearance,ref enter,ref leave)&&
                    Clip(az,bz-az,box.Z-box.HalfZ-clearance,box.Z+box.HalfZ+clearance,ref enter,ref leave))return false;
            }
            return true;
        }
        static bool Clip(double origin,double delta,double min,double max,ref double enter,ref double leave)
        {
            if(Math.Abs(delta)<1e-12)return origin>=min&&origin<=max;
            var a=(min-origin)/delta;var b=(max-origin)/delta;if(a>b)(a,b)=(b,a);
            enter=Math.Max(enter,a);leave=Math.Min(leave,b);return enter<=leave;
        }
    }
}
