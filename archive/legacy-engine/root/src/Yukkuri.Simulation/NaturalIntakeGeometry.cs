using System.Numerics;

namespace Yukkuri.Simulation;

public static class NaturalIntakeGeometry
{
    public const float MaximumLeanDegrees=65,WaterHeight=.024f,CupRimRadius=.12f;
    public static float LeafBiteHeight=>Leaf(.38f,.19f,.5f-.05f/.38f,.5f).Y;
    public static Vector3 Leaf(float length,float width,float u,float v)
    {
        float cross=v*2-1,shape=MathF.Pow(MathF.Max(0,MathF.Sin(MathF.PI*u)),.8f);
        return new((u-.5f)*length,.015f+.06f*shape*(1-MathF.Abs(cross))+.025f*u*u,cross*width*shape*.5f);
    }
    // Container/source contacts are checked against their authored meshes by ActorGeometry.
    public static Vector2 SourceTarget(RoomObjectKind kind,float distance,float height=0)=>kind switch
    {
        RoomObjectKind.RiverWater=>new(distance,height+WaterHeight),
        RoomObjectKind.Water=>new(MathF.Max(0,distance-CupRimRadius),height+.15f),
        RoomObjectKind.Sweet=>new(distance,height+.087f),
        RoomObjectKind.Meal=>new(distance,height+.107f),
        _=>new(distance,height+LeafBiteHeight)
    };
    public static Vector2 Deformation(IntakeKind kind,double remaining,bool groundIntake=false)
    {
        if(remaining<=0)return Vector2.One;
        double age=Math.Max(0,InteractionLife.IntakeDuration(kind)-remaining);
        // Ground intake first approaches with an open, steady mouth. Chewing or
        // sipping starts when the portion actually enters, not during preparation.
        if(groundIntake)age=Math.Max(0,age-NaturalResources.BiteContactSeconds);
        if(remaining<.25)return new(1,(float)(1-.018*Math.Sin(remaining/.25*Math.PI)));
        if(kind==IntakeKind.Water)return new(1,(float)(1+Math.Sin(age*7)*.006));
        float chew=(float)Math.Abs(Math.Sin(age*11));return new(1+chew*.022f,1-chew*.017f);
    }
    private static readonly float BareFood=MinimumHeight(false,false),BareWater=MinimumHeight(false,true),
        WrappedFood=MinimumHeight(true,false),WrappedWater=MinimumHeight(true,true);
    private static float MinimumHeight(bool wrapped,bool water)
    {
        var kind=water?IntakeKind.Water:IntakeKind.Meal;float safeMinimum=0;
        // This is a body capability bound, independent of inventory or hidden contamination.
        // Cache the hardest deformation during contact rather than paying for mesh sampling each tick.
        for(int frame=0;frame<=20;frame++)
        {
            double age=NaturalResources.BiteContactSeconds+(water?.95-NaturalResources.BiteContactSeconds:.65-NaturalResources.BiteContactSeconds)*frame/20;
            var shape=Deformation(kind,InteractionLife.IntakeDuration(kind)-age,true);var scale=new Vector3(shape.X,shape.Y,1);
            var mouth=new Vector3(0,-.175f,BodyContactGeometry.FaceHeight(0,-.175f,.012f))*scale;
            float minimum=float.PositiveInfinity;
            for(int step=0;step<=65;step++)
            {
                var rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,MaximumLeanDegrees*MathF.PI/180*step/65);
                float bottom=BodyContactGeometry.LowestSkinY(scale,rotation);
                if(wrapped)bottom=MathF.Min(bottom,SoftWrapGeometry.LowestY(scale,rotation,Vector3.Zero));
                minimum=MathF.Min(minimum,Vector3.Transform(mouth,rotation).Y-bottom+.003f);
            }
            safeMinimum=MathF.Max(safeMinimum,minimum);
        }
        // Soft food and the lip allow 0.002 local units of contact tolerance. The
        // complete smoothed pose still has to meet the independent 0.005 check.
        return safeMinimum-.002f;
    }
    public static float LowestSupportedMouth(Creature c,RoomObjectKind kind)
    {
        bool water=NaturalResources.IsWater(kind),wrapped=c.Interaction.Restraint==RestraintKind.Wrap;
        float scale=c.IsYoung?.73f:c.IsElder?1.03f:1;
        float floor=c.Interaction.Restraint is RestraintKind.Box or RestraintKind.Frame?
            .46f+InteractionFixtureGeometry.BoxFloorY+InteractionFixtureGeometry.BoxFloorThickness*.5f:0;
        return (floor+(wrapped?(water?WrappedWater:WrappedFood):(water?BareWater:BareFood)))*scale;
    }
    public static bool ClearsBodySupport(Creature c,RoomObject source)=>
        SourceTarget(source.Kind,0,(float)source.Height).Y>=LowestSupportedMouth(c,source.Kind);
}
