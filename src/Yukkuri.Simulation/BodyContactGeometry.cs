using System.Numerics;

namespace Yukkuri.Simulation;

// The visible skin and wrapping share these surfaces with physical reach checks.
public static class BodyContactGeometry
{
    public const float RadiusX=.565f,RadiusY=.445f,RadiusZ=.51f,CenterY=-.015f,CenterZ=.04f;
    public static float FaceHeight(float x,float y,float offset=0)
    {
        float q=1-x*x/(RadiusX*RadiusX)-(y-CenterY)*(y-CenterY)/(RadiusY*RadiusY);
        return CenterZ+RadiusZ*MathF.Sqrt(MathF.Max(0,q))+offset;
    }
    public static float LowestSkinY(Vector3 scale,Quaternion rotation)
    {
        var matrix=Matrix4x4.CreateFromQuaternion(rotation);
        var center=Vector3.Transform(new Vector3(0,CenterY,CenterZ)*scale,matrix);
        var vertical=new Vector3(matrix.M12,matrix.M22,matrix.M32);
        return center.Y-(vertical*scale*new Vector3(RadiusX,RadiusY,RadiusZ)).Length();
    }
}

public static class SoftWrapGeometry
{
    public const int Rows=20,Sides=64;
    public const float HemRadius=.014f;
    public static Vector3 Point(float row,float angle,bool inner=false)
    {
        float front=MathF.Max(0,MathF.Sin(angle));
        float top=.015f-.325f*front*front,bottom=inner?-.49f:-.515f;
        float y=bottom+(top-bottom)*row;
        float radius=MathF.Sqrt(MathF.Max(0,1-y*y/(bottom*bottom)));
        return new(MathF.Cos(angle)*(inner?.646f:.67f)*radius,y,.04f+MathF.Sin(angle)*(inner?.586f:.61f)*radius);
    }
    public static Vector3 Hem(float angle)=>(Point(1,angle)+Point(1,angle,true))*.5f;
    private static readonly Vector3[] SupportPoints=BuildSupportPoints();
    public static ReadOnlySpan<Vector3> Vertices=>SupportPoints;
    private static Vector3[] BuildSupportPoints()
    {
        var points=new Vector3[(Rows+1)*Sides+Sides];int n=0;
        for(int row=0;row<=Rows;row++)for(int side=0;side<Sides;side++)points[n++]=Point(row/(float)Rows,side*MathF.Tau/Sides);
        for(int side=0;side<Sides;side++)points[n++]=Hem(side*MathF.Tau/Sides);
        return points;
    }
    public static float LowestY(Vector3 scale,Quaternion rotation,Vector3 offset)
    {
        var m=Matrix4x4.CreateFromQuaternion(rotation);float lowest=float.PositiveInfinity;
        foreach(var point in SupportPoints)
            lowest=MathF.Min(lowest,point.X*scale.X*m.M12+point.Y*scale.Y*m.M22+point.Z*scale.Z*m.M32+offset.Y);
        return lowest-HemRadius*MathF.Max(scale.X,MathF.Max(scale.Y,scale.Z));
    }
}
