using System;
using System.Collections.Generic;
using System.Numerics;

public static class ActorAccessorySupport
{
    private static readonly Vector3[] BowPoints=BuildBow(),HatPoints=BuildHat();
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
        for(int row=0;row<=12;row++)for(int n=0;n<32;n++)
        {
            float latitude=row*MathF.PI/12,angle=n*MathF.Tau/32;
            points.Add(new Vector3(0,0,.045f)+new Vector3(.08f*MathF.Sin(latitude)*MathF.Cos(angle),.07f*MathF.Cos(latitude),.065f*MathF.Sin(latitude)*MathF.Sin(angle)));
        }
        return points.ToArray();
    }
    private static Vector3[] BuildHat()
    {
        var points=new List<Vector3>();
        for(int row=0;row<=24;row++)for(int n=0;n<64;n++)
        {
            float u=row/24f,angle=n*MathF.Tau/64;
            points.Add(ActorAccessorySurface.HatCone(u,angle));points.Add(ActorAccessorySurface.HatBrim(u,angle));points.Add(ActorAccessorySurface.HatBrimUnderside(u,angle));
        }
        foreach(var p in BowPoints)points.Add(ActorAccessorySurface.HatBow(p*ActorAccessorySurface.HatBowScale));
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
