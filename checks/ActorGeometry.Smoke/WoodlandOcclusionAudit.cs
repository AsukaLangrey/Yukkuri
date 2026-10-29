using System.Diagnostics;
using System.Numerics;
using Yukkuri.Simulation;

public static class WoodlandOcclusionAudit
{
    public static bool Rays()
    {
        foreach(var tree in WoodlandLayout.Trees)
        {
            float scale=(float)tree.Scale;var trunk=tree.TrunkAt(.4);
            var origin=new Vector3((float)trunk.X,.4f,(float)trunk.Z+2);
            float distance=WoodlandOcclusion.RayDistance(origin,-Vector3.UnitZ);
            if(MathF.Abs(distance-(2-(float)trunk.Radius))>.004f)return false;
            var top=new Vector3((float)tree.X,4,(float)tree.Z);
            float crown=WoodlandOcclusion.RayDistance(top,-Vector3.UnitY);
            if(crown<4-2.55f*scale||crown>4-1.5f*scale)return false;
            if(!float.IsPositiveInfinity(WoodlandOcclusion.RayDistance(top,Vector3.UnitY)))return false;
            // This passes through a crown bounding-box corner, outside the rounded leaves.
            var corner=new Vector3((float)tree.X+.78f*scale,2.4f*scale,(float)tree.Z+2*scale);
            if(WoodlandOcclusion.RayDistance(corner,-Vector3.UnitZ)<3*scale)return false;
        }
        return float.IsPositiveInfinity(WoodlandOcclusion.RayDistance(new(0,5,0),-Vector3.UnitY))&&
            float.IsPositiveInfinity(WoodlandOcclusion.RayDistance(Vector3.Zero,Vector3.Zero))&&
            float.IsPositiveInfinity(WoodlandOcclusion.RayDistance(new(float.NaN,0,0),Vector3.UnitX));
    }
    public static bool DepthOrder()
    {
        var tree=WoodlandLayout.Trees[0];var trunk=tree.TrunkAt(.4);
        var origin=new Vector3((float)trunk.X,.4f,(float)trunk.Z+2);
        float barrier=WoodlandOcclusion.RayDistance(origin,-Vector3.UnitZ);
        foreach(bool inFront in new[]{true,false})
        {
            var position=new Vector3(origin.X,.4f-InteractionFixtureGeometry.BallRadius,(float)tree.Z+(inFront?1:-1));
            if(!InteractionPropContact.RayHit(RoomObjectKind.Ball,position,origin,-Vector3.UnitZ,out float hit))return false;
            if(inFront?hit>=barrier:hit<=barrier)return false;
        }
        return true;
    }
    public static bool Allocation()
    {
        var tree=WoodlandLayout.Trees[0];var origin=new Vector3((float)tree.X,4,(float)tree.Z+2);
        const int count=4096;var directions=new Vector3[count];
        for(int n=0;n<count;n++)directions[n]=Vector3.Normalize(new Vector3(MathF.Sin(n*.17f)*.25f,-1,-.5f));
        for(int n=0;n<100;n++)WoodlandOcclusion.RayDistance(origin,directions[n]);
        var timer=new Stopwatch();long before=GC.GetAllocatedBytesForCurrentThread();float total=0;
        timer.Start();for(int n=0;n<count;n++){float d=WoodlandOcclusion.RayDistance(origin,directions[n]);if(float.IsFinite(d))total+=d;}timer.Stop();
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Console.WriteLine($"Tree picking CPU: {count} rays, {timer.Elapsed.TotalMilliseconds/count:F5} ms/ray, {allocated} bytes after warmup; checksum {total:F3}. Not native frame timing.");
        return allocated==0&&total>0;
    }
}
