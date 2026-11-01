using System;
using System.Numerics;

public static class ActorAccessoryPlacement
{
    public static readonly Vector3 HatOrigin=new(.02f,.48f,-.02f),HatRotation=new(-5,0,-6);
    public static readonly Vector3 BowOrigin=new(0,.56f,.225f),BowRotation=new(-8,0,-7);
    public static Matrix4x4 Rotation(Vector3 degrees)=>Matrix4x4.CreateRotationZ(degrees.Z*MathF.PI/180)*
        Matrix4x4.CreateRotationX(degrees.X*MathF.PI/180)*Matrix4x4.CreateRotationY(degrees.Y*MathF.PI/180);
    public static Vector3 Point(Vector3 local,bool hat,Vector3 bodyDeformation,float damageTilt=0)
    {
        var rest=Rotation(hat?HatRotation:BowRotation)*Matrix4x4.CreateRotationZ(damageTilt*MathF.PI/180);
        return Vector3.Transform(local,rest)+(hat?HatOrigin:BowOrigin)*bodyDeformation+Vector3.UnitY*(hat?HatClearance(bodyDeformation,damageTilt):0);
    }
    public static float HatClearance(Vector3 deformation,float damageTilt=0)
    {
        if(_hasClearance&&deformation==_lastDeformation&&damageTilt==_lastTilt)return _lastClearance;
        var rotation=Rotation(HatRotation)*Matrix4x4.CreateRotationZ(damageTilt*MathF.PI/180);
        var center=ActorSurfaceMarks.HairCenter*deformation;
        var radii=ActorSurfaceMarks.HairRadii*deformation;
        var origin=HatOrigin*deformation;float lift=0;
        foreach(var sample in OuterBrim)
        {
            var p=Vector3.Transform(sample,rotation)+origin;
            float q=MathF.Pow((p.X-center.X)/radii.X,2)+MathF.Pow((p.Z-center.Z)/radii.Z,2);
            if(q>1)continue;
            lift=MathF.Max(lift,center.Y+radii.Y*MathF.Sqrt(1-q)+.009f-p.Y);
        }
        _hasClearance=true;_lastDeformation=deformation;_lastTilt=damageTilt;_lastClearance=lift;
        return lift;
    }
    [ThreadStatic] private static bool _hasClearance;
    [ThreadStatic] private static Vector3 _lastDeformation;
    [ThreadStatic] private static float _lastTilt,_lastClearance;
    private static readonly Vector3[] OuterBrim=BuildOuterBrim();
    private static Vector3[] BuildOuterBrim()
    {
        var points=new Vector3[5*96];int n=0;
        for(int angle=0;angle<96;angle++)
        {
            float a=angle*MathF.Tau/96;
            float projection=.025f*MathF.Cos(a)-.02f*MathF.Sin(a);
            float innerRadius=projection+MathF.Sqrt(.412f*.412f-.025f*.025f-.02f*.02f+projection*projection)+.004f;
            float start=(innerRadius-.035f)/.65f;
            for(int row=0;row<5;row++)points[n++]=ActorAccessorySurface.HatBrimUnderside(start+(1-start)*row/4,a);
        }
        return points;
    }
}
