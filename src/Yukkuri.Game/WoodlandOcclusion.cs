using System;
using System.Linq;
using System.Numerics;
using Yukkuri.Simulation;

// Picking uses the same opaque triangles as the visible trees, not a large canopy box.
// This is separate from the creature's conservative ground-level resource perception.
public static class WoodlandOcclusion
{
    private readonly record struct Triangle(Vector3 A,Vector3 Edge1,Vector3 Edge2);
    private sealed record Patch(Vector3 Minimum,Vector3 Maximum,Triangle[] Triangles);
    private static readonly Lazy<Patch[]> Geometry=new(()=>WoodlandLayout.Trees.SelectMany(tree=>
        WoodlandSurface.Tree(new((float)tree.X,0,(float)tree.Z),(float)tree.Scale)).Select(Build).ToArray());

    private static Patch Build(WoodlandPatch source)
    {
        var points=new Vector3[(source.Rows+1)*(source.Columns+1)];
        var min=new Vector3(float.PositiveInfinity);var max=new Vector3(float.NegativeInfinity);
        for(int row=0;row<=source.Rows;row++)for(int column=0;column<=source.Columns;column++)
        {
            var p=source.Point((float)row/source.Rows,(float)column/source.Columns);
            points[row*(source.Columns+1)+column]=p;min=Vector3.Min(min,p);max=Vector3.Max(max,p);
        }
        var triangles=new Triangle[source.Rows*source.Columns*2];int index=0;
        for(int row=0;row<source.Rows;row++)for(int column=0;column<source.Columns;column++)
        {
            int a=row*(source.Columns+1)+column,b=a+source.Columns+1;
            triangles[index++]=new(points[a],points[b]-points[a],points[b+1]-points[a]);
            triangles[index++]=new(points[a],points[b+1]-points[a],points[a+1]-points[a]);
        }
        return new(min,max,triangles);
    }

    public static float RayDistance(Vector3 origin,Vector3 direction)
    {
        if(!Finite(origin)||!Finite(direction)||direction.LengthSquared()<.0000001f)return float.PositiveInfinity;
        float nearest=float.PositiveInfinity;
        foreach(var patch in Geometry.Value)
        {
            if(!BoundsHit(patch,origin,direction,nearest))continue;
            foreach(var triangle in patch.Triangles)
            {
                var h=Vector3.Cross(direction,triangle.Edge2);float determinant=Vector3.Dot(triangle.Edge1,h);
                if(MathF.Abs(determinant)<.0000001f)continue;
                float inverse=1/determinant;var s=origin-triangle.A;
                float u=Vector3.Dot(s,h)*inverse;if(u<0||u>1)continue;
                var q=Vector3.Cross(s,triangle.Edge1);float v=Vector3.Dot(direction,q)*inverse;if(v<0||u+v>1)continue;
                float distance=Vector3.Dot(triangle.Edge2,q)*inverse;
                if(distance>=0&&distance<nearest)nearest=distance;
            }
        }
        return nearest;
    }

    private static bool BoundsHit(Patch patch,Vector3 origin,Vector3 direction,float limit)
    {
        float near=0,far=limit;
        for(int axis=0;axis<3;axis++)
        {
            if(MathF.Abs(direction[axis])<.0000001f)
            {if(origin[axis]<patch.Minimum[axis]||origin[axis]>patch.Maximum[axis])return false;continue;}
            float a=(patch.Minimum[axis]-origin[axis])/direction[axis],b=(patch.Maximum[axis]-origin[axis])/direction[axis];
            near=MathF.Max(near,MathF.Min(a,b));far=MathF.Min(far,MathF.Max(a,b));
            if(far<near)return false;
        }
        return true;
    }
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
