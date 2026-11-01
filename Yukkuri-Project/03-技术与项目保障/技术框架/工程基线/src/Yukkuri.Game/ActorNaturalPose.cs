using System;
using System.Numerics;
using Yukkuri.Simulation;

// Shared by the live actor, simulation reach checks and offline source previews.
// This solver never changes nutrition, health or the creature's world position.
public static class ActorNaturalPose
{
    public static bool AllowsAccessoryAttention(string pose,double intakeRemaining)=>
        intakeRemaining<=0&&pose is "idle" or "accessory-watch" or "reach" or "guard";
    public static Vector2 SourceMouthTarget(RoomObjectKind kind,float distance,float height=0)=>NaturalIntakeGeometry.SourceTarget(kind,distance,height);
    private static float Smooth(float value){value=Math.Clamp(value,0,1);return value*value*(3-2*value);}
    private static (float Angle,Vector3 Offset,float Error) SolveGroundMouth(Vector3 scale,Vector2 target,float heading,ActorNaturalSupport? support)
    {
        var mouth=new Vector3(0,-.175f,ActorFaceSurface.Height(0,-.175f,.012f))*scale;
        const float margin=.003f;
        float lo=-35*MathF.PI/180,hi=NaturalIntakeGeometry.MaximumLeanDegrees*MathF.PI/180;
        (float Height,Vector3 Offset) At(float pitch)
        {
            var rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,pitch);
            var lower=Vector3.Transform(mouth,rotation);
            float down=-.46f-ActorBodyBounds.LowestSkinY(scale,rotation,Vector3.Zero)+margin;
            float forward=target.X-lower.Z;
            var offset=new Vector3(MathF.Sin(heading)*forward,down,MathF.Cos(heading)*forward);
            float lift=support?.Lift(scale,Quaternion.CreateFromYawPitchRoll(heading,pitch,0),offset)??0;
            offset.Y+=lift;
            return (lower.Y+.46f+offset.Y+(support.HasValue?(float)support.Value.Creature.Interaction.Height/support.Value.BodyScale:0),offset);
        }
        // A raised cup may need an upward reach. At a cushion edge the supporting
        // surface can also change mid-lean, so do not assume height is monotonic.
        float left=At(lo).Height-target.Y,right=At(hi).Height-target.Y;
        float best=MathF.Abs(left)<MathF.Abs(right)?lo:hi;
        float error=MathF.Min(MathF.Abs(left),MathF.Abs(right));
        if(left*right>0)
        {
            float previous=lo,previousError=left;
            const int samples=20;
            for(int n=1;n<=samples;n++)
            {
                float pitch=lo+(hi-lo)*n/samples,value=At(pitch).Height-target.Y;
                if(MathF.Abs(value)<error){best=pitch;error=MathF.Abs(value);}
                if(previousError*value<=0){lo=previous;hi=pitch;left=previousError;right=value;break;}
                previous=pitch;previousError=value;
            }
            if(left*right>0)return(best,At(best).Offset,error);
        }
        for(int n=0;n<(support.HasValue?12:18);n++)
        {
            float pitch=(lo+hi)*.5f;
            float value=At(pitch).Height-target.Y;
            if(left*value>0){lo=pitch;left=value;}else hi=pitch;
        }
        float angle=(lo+hi)*.5f;
        var solved=At(angle);return(angle,solved.Offset,MathF.Abs(solved.Height-target.Y));
    }
    private static ReactionMotion GroundMouthPose(Vector3 scale,Vector2 target,float heading,float blend,ActorNaturalSupport? support)
    {
        var solved=SolveGroundMouth(scale,target,heading,support);
        return new(scale,solved.Offset*blend,new(solved.Angle*180/MathF.PI*blend,heading*180/MathF.PI,0));
    }
    public static bool CanReachSource(Creature creature,RoomObject source,System.Collections.Generic.IReadOnlyList<RoomObject> objects,double? atX=null,double? atZ=null,int appearanceIndex=0)
    {
        float size=creature.IsYoung?.73f:creature.IsElder?1.03f:1;
        float dx=(float)(source.X-(atX??creature.X)),dz=(float)(source.Z-(atZ??creature.Z));
        var kind=NaturalResources.IsWater(source.Kind)?IntakeKind.Water:source.Kind==RoomObjectKind.Sweet?IntakeKind.Sweet:IntakeKind.Meal;
        var intake=ActorIntakePresentation.Sample(kind,InteractionLife.IntakeDuration(kind)-NaturalResources.BiteContactSeconds,false,true);
        var target=SourceMouthTarget(source.Kind,MathF.Sqrt(dx*dx+dz*dz),(float)source.Height)/size;
        var solution=SolveGroundMouth(new(intake.Width,intake.Height,1),target,MathF.Atan2(dx,dz),
            new(creature,objects,size,Hat:CreatureAppearance.UsesHat(creature.Body.SpeciesId,appearanceIndex),AtX:atX,AtZ:atZ));
        return solution.Error*size<=.003f;
    }
    public static ReactionMotion? Sample(string pose,float age,double facing=0,bool groundIntake=false,
        IntakeKind intakeKind=IntakeKind.Meal,double remaining=0,double severity=0,
        RoomObjectKind? sourceKind=null,double sourceDistance=.792,double sourceHeight=0,float bodyScale=1,ActorNaturalSupport? support=null)
    {
        age=float.IsFinite(age)?MathF.Max(0,age):0;
        float yaw=double.IsFinite(facing)?(float)Math.IEEERemainder(facing,Math.Tau):0;
        float breath=MathF.Sin(age*MathF.Tau/3.8f);
        switch(pose)
        {
            case "help-look":
                return new(new(1,1.015f,1),Vector3.Zero,new(-4,yaw*180/MathF.PI,2*MathF.Sin(age*3)));
            case "eat":case "sip":
                var intake=ActorIntakePresentation.Sample(intakeKind,remaining,false,groundIntake);
                var scale=new Vector3(intake.Width,intake.Height,1);
                if(groundIntake&&remaining>0)
                {
                    float elapsed=(float)Math.Max(0,InteractionLife.IntakeDuration(intakeKind)-remaining);
                    float blend=Smooth(elapsed/.18f)*Smooth(pose=="sip"?(float)remaining/.25f:(.95f-elapsed)/.3f);
                    var kind=sourceKind??(pose=="sip"?RoomObjectKind.RiverWater:RoomObjectKind.Leaves);
                    var target=SourceMouthTarget(kind,(float)sourceDistance,(float)sourceHeight)/Math.Clamp(bodyScale,.5f,2);
                    return GroundMouthPose(scale,target,yaw,blend,support);
                }
                return new(scale,Vector3.Zero,new(0,yaw*180/MathF.PI,0));
            case "rest":
                return new(new(1.07f-breath*.003f,.88f+breath*.008f,1.04f),Vector3.Zero,new(0,yaw*180/MathF.PI,0));
            case "stomach":
                float strain=double.IsFinite(severity)?(float)Math.Clamp(severity/100,0,1):0;
                return new(new(1.02f,.96f-strain*.06f+breath*.004f,1.01f),Vector3.Zero,new(4+strain*5,yaw*180/MathF.PI,0));
            default:return null;
        }
    }
}
