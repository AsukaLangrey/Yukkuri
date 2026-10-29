using System;
using System.Numerics;
using Yukkuri.Simulation;

public static class ActorDressingSurface
{
    public const int Rows=10,Sides=48;
    private static float Rounded(float value)=>MathF.CopySign(MathF.Pow(MathF.Abs(value),.48f),value);
    public static Vector3 Point(BodySurfaceMark site,float radius,float angle,bool pad=false)
    {
        var outline=new Vector2(Rounded(MathF.Cos(angle)),Rounded(MathF.Sin(angle)))*radius;
        // Both layers taper onto the surface below, so the edge is not a floating paper cutout.
        float bevel=Math.Clamp((radius-.88f)/.12f,0,1);bevel=bevel*bevel*(3-2*bevel);
        float offset=pad?.023f-.004f*bevel:.019f-.012f*bevel;
        if(pad)site=site with {Size=site.Size*new Vector2(.46f,.76f)};
        return ActorSurfaceMarks.Project(site,outline,offset);
    }
    public static float Weave(float radius,float angle)
    {
        float x=Rounded(MathF.Cos(angle))*radius,y=Rounded(MathF.Sin(angle))*radius;
        return .97f+.03f*MathF.Cos(x*18*MathF.PI)*MathF.Cos(y*12*MathF.PI);
    }
    private static readonly Vector3[][] Samples=BuildSamples();
    private static Vector3[][] BuildSamples()
    {
        var result=new Vector3[ActorSurfaceMarks.Dressing.Length][];
        for(int site=0;site<result.Length;site++)
        {
            var points=new Vector3[(Rows+1)*Sides*2];int index=0;
            for(int layer=0;layer<2;layer++)for(int row=0;row<=Rows;row++)for(int n=0;n<Sides;n++)
                points[index++]=Point(ActorSurfaceMarks.Dressing[site],row/(float)Rows,n*MathF.Tau/Sides,layer==1);
            result[site]=points;
        }
        return result;
    }
    public static float LowestY(InteractionLife life,Matrix4x4 transform)
    {
        float lowest=float.PositiveInfinity;
        for(int index=0;index<Samples.Length;index++)
        {
            var site=ActorSurfaceMarks.Dressing[index];
            if(!life.HasDressing(site.Part,site.Side))continue;
            foreach(var point in Samples[index])lowest=MathF.Min(lowest,Vector3.Transform(point,transform).Y);
        }
        return lowest;
    }
    public static float CushionLift(InteractionLife life,Vector3 cushionOrigin,Vector3 bodyOrigin,Vector3 scale,Quaternion rotation)
    {
        var transform=Matrix4x4.CreateScale(scale)*Matrix4x4.CreateFromQuaternion(rotation)*Matrix4x4.CreateTranslation(bodyOrigin);
        float lift=0;
        for(int index=0;index<Samples.Length;index++)
        {
            var site=ActorSurfaceMarks.Dressing[index];
            if(life.HasDressing(site.Part,site.Side))lift=MathF.Max(lift,ActorCushionSurface.PointLift(Samples[index],transform,cushionOrigin,.004f));
        }
        return lift;
    }
}
