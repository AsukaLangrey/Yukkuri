using System;
using System.Numerics;
using Yukkuri.Simulation;

public static class ActorCradleSurface
{
    public const int Rows=SoftWrapGeometry.Rows,Sides=SoftWrapGeometry.Sides;
    public const float HemRadius=SoftWrapGeometry.HemRadius;
    public static Vector3 Point(float row,float angle,bool inner=false)=>SoftWrapGeometry.Point(row,angle,inner);
    public static Vector3 Hem(float angle)=>SoftWrapGeometry.Hem(angle);
    public static ReadOnlySpan<Vector3> Vertices=>SoftWrapGeometry.Vertices;
    public static float LowestY(Vector3 scale,Quaternion rotation,Vector3 offset)=>SoftWrapGeometry.LowestY(scale,rotation,offset);
}
