using System;
using System.Numerics;
using Yukkuri.Simulation;

// Surface-space patches stay attached while the complete body is squeezed or turned.
public readonly record struct BodySurfaceMark(BodyPart Part,Vector3 Direction,Vector2 Size,bool OnHair=false)
{
    public SurfaceSide Side=>ActorSurfaceMarks.ContactSide(Part,Direction.X);
}
public static class ActorSurfaceMarks
{
    public static readonly Vector3 HairCenter=new(0,.035f,0),HairRadii=new(.595f,.52f,.54f);
    private static readonly Vector3 SkinCenter=new(0,ActorFaceSurface.CenterY,ActorFaceSurface.CenterZ);
    private static readonly Vector3 SkinRadii=new(ActorFaceSurface.RadiusX,ActorFaceSurface.RadiusY,ActorFaceSurface.RadiusZ);
    public static readonly BodySurfaceMark[] Dirt=
    [
        new(BodyPart.Cheek,new(-.63f,-.18f,.76f),new(.13f,.065f)),
        new(BodyPart.Cheek,new(.63f,-.18f,.76f),new(.08f,.045f)),
        new(BodyPart.Side,new(.53f,-.72f,.45f),new(.12f,.06f)),
        new(BodyPart.Side,new(-.60f,-.65f,.47f),new(.08f,.06f)),
        new(BodyPart.Mouth,new(-.18f,-.36f,.92f),new(.04f,.035f)),
        new(BodyPart.Mouth,new(.18f,-.36f,.92f),new(.035f,.025f)),
        new(BodyPart.Bottom,new(.10f,-1,.08f),new(.30f,.22f)),
        new(BodyPart.Head,new(.66f,.10f,-.75f),new(.16f,.08f),true),
        new(BodyPart.Head,new(-.66f,.10f,-.75f),new(.10f,.07f),true)
    ];
    public static readonly BodySurfaceMark[] Wet=
    [
        new(BodyPart.Cheek,new(-.68f,-.10f,.73f),new(.035f,.085f)),
        new(BodyPart.Cheek,new(.68f,-.10f,.73f),new(.03f,.065f)),
        new(BodyPart.Side,new(.50f,-.71f,.50f),new(.06f,.045f)),
        new(BodyPart.Side,new(-.60f,-.65f,.47f),new(.035f,.07f)),
        new(BodyPart.Mouth,new(.18f,-.36f,.92f),new(.03f,.035f)),
        new(BodyPart.Bottom,new(-.10f,-1,-.08f),new(.28f,.18f)),
        new(BodyPart.Head,new(.66f,.10f,-.75f),new(.035f,.07f),true),
        new(BodyPart.Head,new(-.66f,.10f,-.75f),new(.03f,.06f),true)
    ];
    public static readonly BodySurfaceMark[] Dressing=
    [
        new(BodyPart.Head,new(.66f,.10f,-.75f),new(.15f,.07f),true),
        new(BodyPart.Cheek,new(.63f,-.18f,.76f),new(.15f,.065f)),
        new(BodyPart.Cheek,new(-.63f,-.18f,.76f),new(.15f,.065f)),
        new(BodyPart.Side,new(.53f,-.72f,.45f),new(.12f,.06f)),
        new(BodyPart.Side,new(-.53f,-.72f,.45f),new(.12f,.06f)),
        new(BodyPart.Mouth,new(-.18f,-.36f,.92f),new(.04f,.035f)),
        new(BodyPart.Bottom,new(0,-1,0),new(.38f,.17f))
    ];
    public static Vector3 Point(BodySurfaceMark mark,float radius,float angle,float offset=.009f)
        =>Project(mark,new Vector2(MathF.Cos(angle),MathF.Sin(angle))*radius,offset);
    public static Vector3 Project(BodySurfaceMark mark,Vector2 surface,float offset)
    {
        Vector3 direction=Vector3.Normalize(mark.Direction),radii=mark.OnHair?HairRadii:SkinRadii;
        Vector3 reference=MathF.Abs(direction.Y)>.9f?Vector3.UnitZ:Vector3.UnitY;
        Vector3 right=Vector3.Normalize(Vector3.Cross(reference,direction));
        Vector3 up=Vector3.Normalize(Vector3.Cross(direction,right));
        Vector3 unit=Vector3.Normalize(direction+(right*mark.Size.X*surface.X+up*mark.Size.Y*surface.Y)*.5f/radii);
        Vector3 normal=Vector3.Normalize(unit/radii);
        var point=(mark.OnHair?HairCenter:SkinCenter)+unit*radii;
        if(mark.OnHair)
        {
            var shell=ActorHairSurface.OutsideBack(point,0);
            if(Vector3.DistanceSquared(shell,point)>.000000001f)normal=ActorHairSurface.BackNormal(shell);
            point=shell;
        }
        return point+normal*offset;
    }
    public static BodyPart ClassifySkin(Vector3 point)
    {
        Vector3 n=Vector3.Normalize((point-SkinCenter)/(SkinRadii*SkinRadii));
        return n.Y<-.82f?BodyPart.Bottom:point.Y>.20f?BodyPart.Head:
            point.Z>.28f&&MathF.Pow(point.X/.16f,2)+MathF.Pow((point.Y+.17f)/.105f,2)<1?BodyPart.Mouth:
            point.Z>.22f&&MathF.Abs(point.X)>.18f&&point.Y>-.24f?BodyPart.Cheek:BodyPart.Side;
    }
    public static SurfaceSide ContactSide(BodyPart part,float localX)=>InteractionLife.HasPairedSides(part)?localX<0?SurfaceSide.Left:SurfaceSide.Right:SurfaceSide.Whole;
    public static float Opacity(InteractionLife life,BodyPart part,bool wet,SurfaceSide side=SurfaceSide.Whole)=>
        (float)Math.Clamp((wet?life.WetnessAt(part,side)/24:life.DirtAt(part,side)/35),0,1);
}
