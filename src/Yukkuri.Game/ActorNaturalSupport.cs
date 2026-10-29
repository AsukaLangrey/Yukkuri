using System;
using System.Collections.Generic;
using System.Numerics;
using Yukkuri.Simulation;

// Intake must solve against the same support surfaces that later keep the actor
// above the scene. Coordinates passed to Lift are the actor's unscaled local pose.
public readonly record struct ActorNaturalSupport(Creature Creature,IReadOnlyList<RoomObject>? Objects,float BodyScale,bool? Hat=null,double? AtX=null,double? AtZ=null)
{
    private readonly record struct Bounds(Vector3 Center,Vector3 Extent)
    {
        public static Bounds From(ReadOnlySpan<Vector3> points)
        {
            var min=new Vector3(float.PositiveInfinity);var max=new Vector3(float.NegativeInfinity);
            foreach(var p in points){min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
            return new((min+max)*.5f,(max-min)*.5f);
        }
        public float MinimumY(Matrix4x4 m)=>Vector3.Transform(Center,m).Y-
            (MathF.Abs(m.M12)*Extent.X+MathF.Abs(m.M22)*Extent.Y+MathF.Abs(m.M32)*Extent.Z);
        public bool MayTouchCushion(Matrix4x4 m,Vector3 bed,float margin)
        {
            if(MinimumY(m)>bed.Y+ActorCushionSurface.CenterY+ActorCushionSurface.HalfHeight+margin)return false;
            var center=Vector3.Transform(Center,m);
            float x=MathF.Abs(m.M11)*Extent.X+MathF.Abs(m.M21)*Extent.Y+MathF.Abs(m.M31)*Extent.Z;
            float z=MathF.Abs(m.M13)*Extent.X+MathF.Abs(m.M23)*Extent.Y+MathF.Abs(m.M33)*Extent.Z;
            return MathF.Abs(center.X-bed.X)<=x+ActorCushionSurface.RadiusX&&MathF.Abs(center.Z-bed.Z)<=z+ActorCushionSurface.RadiusZ;
        }
    }
    private static readonly Bounds HairBounds=Bounds.From(ActorHairSurface.BackSupport),BowBounds=Bounds.From(ActorAccessorySupport.Points(false)),HatBounds=Bounds.From(ActorAccessorySupport.Points(true));
    public float Lift(Vector3 scale,Quaternion rotation,Vector3 offset)
    {
        var c=Creature;var life=c.Interaction;bool hat=Hat??CreatureAppearance.UsesHat(c.Body.SpeciesId);
        double x=AtX??c.X,z=AtZ??c.Z;
        var origin=new Vector3((float)x,.46f*BodyScale+(float)life.Height,(float)z)+offset*BodyScale;
        var bodyScale=scale*BodyScale;
        float floor=life.Restraint is RestraintKind.Box or RestraintKind.Frame?
            (.46f+InteractionFixtureGeometry.BoxFloorY+InteractionFixtureGeometry.BoxFloorThickness*.5f)*BodyScale:0;
        float bottom=ActorBodyBounds.LowestSkinY(bodyScale,rotation,origin);
        var transform=Matrix4x4.CreateScale(bodyScale)*Matrix4x4.CreateFromQuaternion(rotation)*Matrix4x4.CreateTranslation(origin);
        bottom=Lowest(ActorHairSurface.BackSupport,HairBounds,transform,bottom);
        if(life.HasAccessory)bottom=Lowest(ActorAccessorySupport.Points(hat),hat?HatBounds:BowBounds,
            ActorAccessorySupport.Transform(hat,origin,bodyScale,BodyScale,rotation,life.Accessory.Integrity<75?12:0),bottom);
        bottom=MathF.Min(bottom,ActorDressingSurface.LowestY(life,transform));
        if(life.Restraint==RestraintKind.Wrap)bottom=MathF.Min(bottom,ActorCradleSurface.LowestY(bodyScale,rotation,origin));
        float lift=MathF.Max(0,floor+.003f-bottom);origin.Y+=lift;
        if(life.Restraint is RestraintKind.None or RestraintKind.Wrap&&Objects!=null)
            for(int index=0;index<Objects.Count;index++)
            {
                var cushion=Objects[index];
                if(cushion.Stored||cushion.Kind!=RoomObjectKind.Cushion||cushion.RegionId!=c.RegionId||Math.Abs(cushion.X-x)>1.8||Math.Abs(cushion.Z-z)>1.7)continue;
                float extra=CushionLift(life,hat,new((float)cushion.X,(float)cushion.Height,(float)cushion.Z),origin,bodyScale,BodyScale,rotation);
                lift+=extra;origin.Y+=extra;
            }
        return lift/BodyScale;
    }
    private static float Lowest(ReadOnlySpan<Vector3> points,Bounds bounds,Matrix4x4 transform,float ceiling)
    {
        if(bounds.MinimumY(transform)>=ceiling)return ceiling;
        float y=ceiling;
        foreach(var p in points)y=MathF.Min(y,Vector3.Transform(p,transform).Y);
        return y;
    }
    public static float CushionLift(InteractionLife life,bool hat,Vector3 bed,Vector3 body,Vector3 scale,float ageScale,Quaternion rotation)
    {
        float lift=ActorCushionSurface.SkinLift(bed,body,scale,rotation);
        var transform=Matrix4x4.CreateScale(scale)*Matrix4x4.CreateFromQuaternion(rotation)*Matrix4x4.CreateTranslation(body);
        if(HairBounds.MayTouchCushion(transform,bed,.010f))lift=MathF.Max(lift,ActorCushionSurface.HairLift(bed,body,scale,rotation));
        lift=MathF.Max(lift,ActorDressingSurface.CushionLift(life,bed,body,scale,rotation));
        if(life.HasAccessory)
        {
            var accessory=ActorAccessorySupport.Transform(hat,body,scale,ageScale,rotation,life.Accessory.Integrity<75?12:0);
            if((hat?HatBounds:BowBounds).MayTouchCushion(accessory,bed,.012f*ageScale))
                lift=MathF.Max(lift,ActorCushionSurface.PointLift(ActorAccessorySupport.Points(hat),accessory,bed,.012f*ageScale));
        }
        if(life.Restraint==RestraintKind.Wrap)lift=MathF.Max(lift,ActorCushionSurface.CradleLift(bed,body,scale,rotation));
        return lift;
    }
}
