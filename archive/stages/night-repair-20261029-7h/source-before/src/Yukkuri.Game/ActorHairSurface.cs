using System;
using System.Numerics;

public readonly record struct HairLockShape(Vector3 Start,Vector3 End,float Width,float Depth,float Bend,bool Fringe);
public readonly record struct HairLockSection(Vector3 Center,Vector3 Front,float Width,float Depth);
public static class ActorHairSurface
{
    public const int BackRows=32,BackSides=80;
    private const float BackAngle=1.78f;
    public static float BackHem(float angle)=>-.285f-.045f*(.5f+.5f*MathF.Cos(angle*12.6f))+.045f*MathF.Pow(MathF.Abs(angle)/BackAngle,4);
    public static Vector3 Back(float u,float v,bool inner=false)
    {
        float angle=(v*2-1)*BackAngle;
        float y=.34f+(BackHem(angle)-.34f)*u;
        float dx=MathF.Sin(angle),dz=-MathF.Cos(angle);
        float skin=RadialBoundary(y,dx,dz,new(0,ActorFaceSurface.CenterY,ActorFaceSurface.CenterZ),new(ActorFaceSurface.RadiusX,ActorFaceSurface.RadiusY,ActorFaceSurface.RadiusZ));
        float cap=RadialBoundary(y,dx,dz,ActorSurfaceMarks.HairCenter,ActorSurfaceMarks.HairRadii);
        float fade=Math.Clamp(u/.20f,0,1);fade=fade*fade*(3-2*fade);
        // Broad, connected locks instead of separate round blobs. Their shallow ridges
        // emerge below the existing crown; the inner hem returns inside the skin.
        float ridge=(.008f+.012f*u)*(.5f+.5f*MathF.Cos(angle*12.6f+u*.7f))*fade;
        float crown=cap+.005f*fade-.003f*(1-fade),lower=skin+.022f;
        float blend=MathF.Max(0,.08f-MathF.Abs(crown-lower))/.08f;
        float radius=inner?skin-.012f:MathF.Max(crown,lower)+blend*blend*.02f+ridge;
        return new(dx*radius,y,ActorFaceSurface.CenterZ+dz*radius);
    }
    public static Vector3 OutsideBack(Vector3 point,float clearance)
    {
        float z=point.Z-ActorFaceSurface.CenterZ,angle=MathF.Atan2(point.X,-z);
        if(MathF.Abs(angle)>BackAngle||point.Y>.34f||point.Y<BackHem(angle))return point;
        float u=(.34f-point.Y)/(.34f-BackHem(angle));
        var shell=Back(u,(angle/BackAngle+1)*.5f);
        float radius=MathF.Sqrt(shell.X*shell.X+MathF.Pow(shell.Z-ActorFaceSurface.CenterZ,2));
        float current=MathF.Sqrt(point.X*point.X+z*z);
        if(current>=radius+clearance)return point;
        return new(MathF.Sin(angle)*(radius+clearance),point.Y,ActorFaceSurface.CenterZ-MathF.Cos(angle)*(radius+clearance));
    }
    public static Vector3 BackNormal(Vector3 point)
    {
        float angle=MathF.Atan2(point.X,-(point.Z-ActorFaceSurface.CenterZ));
        float u=Math.Clamp((.34f-point.Y)/(.34f-BackHem(angle)),0,1),v=(angle/BackAngle+1)*.5f;
        const float step=.0005f;
        var down=Back(MathF.Min(1,u+step),v)-Back(MathF.Max(0,u-step),v);
        var across=Back(u,MathF.Min(1,v+step))-Back(u,MathF.Max(0,v-step));
        return Vector3.Normalize(Vector3.Cross(across,down));
    }
    private static float RadialBoundary(float y,float dx,float dz,Vector3 center,Vector3 radii)
    {
        float z=ActorFaceSurface.CenterZ-center.Z;
        float a=dx*dx/(radii.X*radii.X)+dz*dz/(radii.Z*radii.Z);
        float b=2*z*dz/(radii.Z*radii.Z);
        float c=z*z/(radii.Z*radii.Z)+(y-center.Y)*(y-center.Y)/(radii.Y*radii.Y)-1;
        float discriminant=b*b-4*a*c;
        return discriminant>0?MathF.Max(0,(-b+MathF.Sqrt(discriminant))/(2*a)):0;
    }
    private static readonly Vector3[] BackSamples=BuildBackSamples();
    public static ReadOnlySpan<Vector3> BackSupport=>BackSamples;
    private static Vector3[] BuildBackSamples()
    {
        var result=new Vector3[(BackRows+1)*(BackSides+1)];int n=0;
        for(int r=0;r<=BackRows;r++)for(int s=0;s<=BackSides;s++)result[n++]=Back(r/(float)BackRows,s/(float)BackSides);
        return result;
    }
    public static HairLockShape Fringe(int index)
    {
        float x=(index-2)*.18f;
        return new(new(x*.92f,.43f,.20f),new(x,.22f+(index%2)*.025f,.465f),.14f,.063f,(index-2)*.012f,true);
    }
    public static HairLockShape Side(float sign)=>new(new(sign*.48f,.30f,.08f),new(sign*.48f,-.31f,.23f),.135f,.13f,sign*.48f*.05f,false);
    public static HairLockSection Section(HairLockShape shape,float u)
    {
        var tangent=Vector3.Normalize(shape.End-shape.Start);
        var front=Vector3.Normalize(Vector3.Cross(tangent,Vector3.UnitX));if(front.Z<0)front=-front;
        var center=Vector3.Lerp(shape.Start,shape.End,u)+Vector3.UnitZ*(MathF.Sin(u*MathF.PI)*.045f)+Vector3.UnitX*(MathF.Sin(u*MathF.PI)*shape.Bend);
        float profile=(.30f+.70f*MathF.Sin(MathF.PI*MathF.Pow(u,.72f)))*MathF.Sqrt(MathF.Max(0,1-MathF.Pow(u,5)));
        if(shape.Fringe&&u>.45f)
        {
            // Lift the entire cross-section, rather than flattening only its back vertices
            // against the forehead. Roots remain embedded in the hair cap.
            float lift=0;
            for(int sample=0;sample<32;sample++)
            {
                float a=sample*MathF.Tau/32;
                var p=center+Vector3.UnitX*(MathF.Cos(a)*shape.Width*profile)+front*(MathF.Sin(a)*shape.Depth*profile);
                lift=MathF.Max(lift,ActorFaceSurface.Height(p.X,p.Y,.014f)-p.Z);
            }
            float fade=Math.Clamp((u-.45f)/.35f,0,1);fade=fade*fade*(3-2*fade);
            center.Z+=lift*fade;
        }
        return new(center,front,shape.Width*profile,shape.Depth*profile);
    }
    public static Vector3 Point(HairLockSection section,float v)=>section.Center+
        Vector3.UnitX*(MathF.Cos(v*MathF.Tau)*section.Width)+section.Front*(MathF.Sin(v*MathF.Tau)*section.Depth);
    public static Vector3 Point(HairLockShape shape,float u,float v)=>Point(Section(shape,u),v);
    public static Vector3 Highlight(HairLockShape shape,float u)
    {
        var tangent=Vector3.Normalize(shape.End-shape.Start);
        var front=Vector3.Normalize(Vector3.Cross(tangent,Vector3.UnitX));if(front.Z<0)front=-front;
        return Point(shape,u,.25f)+front*.002f;
    }
}
