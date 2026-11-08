using System;
using System.Numerics;

public static class ActorBodyBounds
{
    public static Vector3 LowestSkinPoint(Vector3 scale,Quaternion rotation,Vector3 offset)
    {
        var matrix=Matrix4x4.CreateFromQuaternion(rotation);
        var radii=new Vector3(ActorFaceSurface.RadiusX,ActorFaceSurface.RadiusY,ActorFaceSurface.RadiusZ)*scale;
        var vertical=new Vector3(matrix.M12,matrix.M22,matrix.M32);
        float extent=(vertical*radii).Length();
        var point=new Vector3(0,ActorFaceSurface.CenterY,ActorFaceSurface.CenterZ)*scale-radii*radii*vertical/extent;
        return Vector3.Transform(point,matrix)+offset;
    }
    public static float LowestSkinY(Vector3 scale,Quaternion rotation,Vector3 offset)
    {
        var matrix=Matrix4x4.CreateFromQuaternion(rotation);
        var center=Vector3.Transform(new Vector3(0,ActorFaceSurface.CenterY,ActorFaceSurface.CenterZ)*scale,matrix)+offset;
        var radii=new Vector3(ActorFaceSurface.RadiusX,ActorFaceSurface.RadiusY,ActorFaceSurface.RadiusZ)*scale;
        float extent=MathF.Sqrt(MathF.Pow(matrix.M12*radii.X,2)+MathF.Pow(matrix.M22*radii.Y,2)+MathF.Pow(matrix.M32*radii.Z,2));
        return center.Y-extent;
    }
}
