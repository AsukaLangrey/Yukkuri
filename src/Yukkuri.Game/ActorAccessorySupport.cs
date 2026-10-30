using System;
using System.Collections.Generic;
using System.Numerics;

public static class ActorAccessorySupport
{
    private static readonly Vector3[] RawBowPoints=BuildBow(),BowPoints=ConformBow(),HatPoints=BuildHat();
    private static Vector3[] ConformBow()
    {
        var points=new Vector3[RawBowPoints.Length];
        for(int n=0;n<points.Length;n++)points[n]=ActorAccessorySurface.HeadBow(RawBowPoints[n]);
        return points;
    }
    public static ReadOnlySpan<Vector3> Points(bool hat)=>hat?HatPoints:BowPoints;
    private static Vector3[] BuildBow()
    {
        var points=new List<Vector3>();
        foreach(float sign in new[]{-1f,1f})for(int row=0;row<=16;row++)for(int n=0;n<=16;n++)
        {
            float u=row/16f,v=n/16f;var p=ActorAccessorySurface.Bow(sign,u,v);
            points.Add(p);points.Add(p-Vector3.UnitZ*ActorAccessorySurface.ClothThickness);
            var tail=ActorAccessorySurface.BowTail(sign,u,v);points.Add(tail);points.Add(tail-Vector3.UnitZ*ActorAccessorySurface.ClothThickness);
        }
        for(int row=0;row<=ActorAccessorySurface.BowKnotRows;row++)
            for(int n=0;n<=ActorAccessorySurface.BowKnotSides;n++)
                points.Add(ActorAccessorySurface.BowKnot(row/(float)ActorAccessorySurface.BowKnotRows,
                    n/(float)ActorAccessorySurface.BowKnotSides));
        return points.ToArray();
    }
    private static Vector3[] BuildHat()
    {
        var points=new List<Vector3>();
        for(int row=0;row<=24;row++)for(int n=0;n<64;n++)
        {
            float u=row/24f,angle=n*MathF.Tau/64;
            points.Add(ActorAccessorySurface.HatCone(u,angle));
        }
        // Sample the exact authored brim grid, including its shallow top folds.
        for(int row=0;row<=ActorAccessorySurface.BrimRows;row++)for(int n=0;n<=ActorAccessorySurface.BrimSides;n++)
        {
            float u=row/(float)ActorAccessorySurface.BrimRows,angle=n/(float)ActorAccessorySurface.BrimSides*MathF.Tau;
            points.Add(ActorAccessorySurface.HatBrim(u,angle));points.Add(ActorAccessorySurface.HatBrimUnderside(u,angle));
        }
        foreach(var p in RawBowPoints)points.Add(ActorAccessorySurface.HatBow(p*ActorAccessorySurface.HatBowScale));
        return points.ToArray();
    }
    public static Matrix4x4 Transform(bool hat,Vector3 bodyOrigin,Vector3 bodyScale,float ageScale,Quaternion rotation,float damageTilt)
    {
        var rest=ActorAccessoryPlacement.Rotation(hat?ActorAccessoryPlacement.HatRotation:ActorAccessoryPlacement.BowRotation)*Matrix4x4.CreateRotationZ(damageTilt*MathF.PI/180);
        var anchor=(hat?ActorAccessoryPlacement.HatOrigin:ActorAccessoryPlacement.BowOrigin)*bodyScale;
        if(hat)anchor.Y+=ActorAccessoryPlacement.HatClearance(bodyScale/ageScale,damageTilt)*ageScale;
        return rest*Matrix4x4.CreateScale(ageScale)*Matrix4x4.CreateFromQuaternion(rotation)*Matrix4x4.CreateTranslation(bodyOrigin+Vector3.Transform(anchor,rotation));
    }
}
