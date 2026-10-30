using System;
using System.Numerics;

public readonly record struct HairLockShape(Vector3 Start,Vector3 End,float Width,float Depth,float Bend,bool Fringe);
public readonly record struct HairLockSection(Vector3 Center,Vector3 Front,float Width,float Depth,float Relief=0,bool Fringe=false);
public static class ActorHairSurface
{
    public const int BackRows=32,BackSides=80;
    public const float FringeRecess=.0025f,SideRecess=.004f;
    private const float BackAngle=1.78f;
    public static bool Contains(Vector3 point)
    {
        var q=(point-ActorSurfaceMarks.HairCenter)/ActorSurfaceMarks.HairRadii;
        float angle=MathF.Atan2(q.X,-q.Z);
        return point.Y>=BackHem(angle)&&q.LengthSquared()<1;
    }
    public static float BackHem(float angle)
    {
        float front=Math.Clamp((MathF.Abs(angle)-1.3f)/(MathF.PI-1.3f),0,1);front=front*front*(3-2*front);
        float rear=-.285f-.035f*(.5f+.5f*MathF.Cos(angle*12.6f));
        return rear*(1-front)+.31f*front;
    }
    public static Vector3 Shell(float u,float v,bool inner=false)
    {
        float angle=(v*2-1)*MathF.PI;
        var center=ActorSurfaceMarks.HairCenter;var radii=ActorSurfaceMarks.HairRadii;
        float latitude=u*MathF.Acos((BackHem(angle)-center.Y)/radii.Y);
        float radius=MathF.Sin(latitude);
        var p=center+new Vector3(MathF.Sin(angle)*radii.X*radius,MathF.Cos(latitude)*radii.Y,-MathF.Cos(angle)*radii.Z*radius);
        if(inner)
        {
            var skinCenter=new Vector3(0,ActorFaceSurface.CenterY,ActorFaceSurface.CenterZ);
            var skinRadii=new Vector3(ActorFaceSurface.RadiusX,ActorFaceSurface.RadiusY,ActorFaceSurface.RadiusZ);
            return skinCenter+Vector3.Normalize((p-skinCenter)/skinRadii)*skinRadii*.98f;
        }
        // One convex crown-to-nape surface. Only shallow vertical fluting is added;
        // no second ellipsoid or horizontal join can make the skull look dented.
        float ridge=.004f*radius*u*u*(.5f+.5f*MathF.Cos(angle*12+u*.7f));
        return p+new Vector3(MathF.Sin(angle),0,-MathF.Cos(angle))*ridge;
    }
    public static Vector3 Back(float u,float v,bool inner=false)
    {
        float angle=(v*2-1)*BackAngle;
        float y=.34f+(BackHem(angle)-.34f)*u;
        var center=ActorSurfaceMarks.HairCenter;var radii=ActorSurfaceMarks.HairRadii;
        float latitude=MathF.Acos((y-center.Y)/radii.Y);
        float extent=MathF.Acos((BackHem(angle)-center.Y)/radii.Y);
        return Shell(latitude/extent,(angle/MathF.PI+1)*.5f,inner);
    }
    public static Vector3 OutsideBack(Vector3 point,float clearance)
    {
        var center=ActorSurfaceMarks.HairCenter;var radii=ActorSurfaceMarks.HairRadii;
        float z=point.Z-center.Z,angle=MathF.Atan2(point.X/radii.X,-z/radii.Z);
        float hem=BackHem(angle);
        if(MathF.Abs(angle)>BackAngle||point.Y>.34f||point.Y<hem-clearance)return point;
        float u=(.34f-MathF.Max(point.Y,hem))/(.34f-hem);
        var shell=Back(u,(angle/BackAngle+1)*.5f);
        float radius=MathF.Sqrt(shell.X*shell.X+MathF.Pow(shell.Z-center.Z,2));
        float current=MathF.Sqrt(point.X*point.X+z*z);
        if(current>=radius+clearance)return point;
        return new(shell.X*(1+clearance/radius),point.Y,center.Z+(shell.Z-center.Z)*(1+clearance/radius));
    }
    public static Vector3 BackNormal(Vector3 point)
    {
        float angle=MathF.Atan2(point.X/ActorSurfaceMarks.HairRadii.X,-(point.Z-ActorSurfaceMarks.HairCenter.Z)/ActorSurfaceMarks.HairRadii.Z);
        float u=Math.Clamp((.34f-point.Y)/(.34f-BackHem(angle)),0,1),v=(angle/BackAngle+1)*.5f;
        const float step=.0005f;
        var down=Back(MathF.Min(1,u+step),v)-Back(MathF.Max(0,u-step),v);
        var across=Back(u,MathF.Min(1,v+step))-Back(u,MathF.Max(0,v-step));
        return Vector3.Normalize(Vector3.Cross(across,down));
    }
    private static readonly Vector3[] BackSamples=BuildBackSamples();
    public static ReadOnlySpan<Vector3> BackSupport=>BackSamples;
    private static Vector3[] BuildBackSamples()
    {
        var result=new Vector3[(BackRows+1)*(BackSides+1)];int n=0;
        for(int r=0;r<=BackRows;r++)for(int s=0;s<=BackSides;s++)result[n++]=Shell(r/(float)BackRows,s/(float)BackSides);
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
        float taper=u<=0||u>=1?0:MathF.Pow(MathF.Sin(u*MathF.PI),2);
        return new(center,front,shape.Width*profile,shape.Depth*profile,
            (shape.Fringe?FringeRecess:SideRecess)*taper,shape.Fringe);
    }
    // The original smooth section is retained as the exact outer boundary. The
    // new bundles change only Z on its front, leaving X/Y and the back untouched.
    public static Vector3 SmoothPoint(HairLockSection section,float v)=>section.Center+
        Vector3.UnitX*(MathF.Cos(v*MathF.Tau)*section.Width)+section.Front*(MathF.Sin(v*MathF.Tau)*section.Depth);
    public static float Recess(HairLockSection section,float v)
    {
        if(section.Relief<=0||v<=0||v>=.5f)return 0;
        float angle=v*MathF.Tau,front=MathF.Sin(angle);
        // Two valleys separate the central lock from its shoulders. Both vanish
        // at the silhouette, the central ridge, the embedded root and the tip.
        float groove=MathF.Pow(MathF.Sin(MathF.PI*MathF.Cos(angle)),8)*front*front;
        float depth=MathF.Min(section.Relief*groove,section.Depth*section.Front.Z*front*.18f);
        if(section.Fringe)
        {
            var p=SmoothPoint(section,v);
            // Never push an existing point outward to enforce a new rule. Only
            // use clearance already available above the original forehead. This
            // soft limiter is <= depth and <= clearance/2, with zero slope when
            // clearance closes, avoiding a hard crease against the skin limit.
            float clearance=MathF.Max(0,p.Z-ActorFaceSurface.Height(p.X,p.Y,.012f));
            if(depth>0&&clearance>0)
            {
                float ratio=MathF.Min(depth,clearance)/MathF.Max(depth,clearance);
                depth=clearance>=depth?depth/(1+ratio*ratio):clearance*ratio/(1+ratio*ratio);
            }
            else depth=0;
        }
        return MathF.Max(0,depth);
    }
    public static Vector3 Point(HairLockSection section,float v)=>SmoothPoint(section,v)-Vector3.UnitZ*Recess(section,v);
    public static Vector3 Point(HairLockShape shape,float u,float v)=>Point(Section(shape,u),v);
    public static Vector3 Highlight(HairLockShape shape,float u)
    {
        var tangent=Vector3.Normalize(shape.End-shape.Start);
        var front=Vector3.Normalize(Vector3.Cross(tangent,Vector3.UnitX));if(front.Z<0)front=-front;
        return Point(shape,u,.25f)+front*.002f;
    }
}
