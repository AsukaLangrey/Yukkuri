using System;
using System.Numerics;

public readonly record struct ReactionMotion(Vector3 Scale,Vector3 Offset,Vector3 RotationDegrees);
public static class ActorReactionMotion
{
    public const float GuardedImpactDuration=.22f;
    // A brief resolved impact may sit on top of a protective posture. It has no
    // travel offset: actual displacement remains entirely in the simulation.
    public static ReactionMotion? SampleGuardedImpact(string impact,float age,Vector2 direction,bool restrained=false)
    {
        float strength=impact switch {"slap"=>1,"flick"=>.65f,"poke"=>.45f,_=>0};
        if(strength<=0||!float.IsFinite(age)||age<0||age>=GuardedImpactDuration)return null;
        if(!float.IsFinite(direction.X)||!float.IsFinite(direction.Y)||direction.LengthSquared()<.00001f)
            direction=new(0,-1);
        else direction=Vector2.Normalize(direction);
        float t=age/GuardedImpactDuration;
        float recoil=(1-t)*(1-t)*MathF.Cos(t*MathF.Tau)*strength;
        float compression=recoil*(restrained?.025f:.045f);
        float angle=restrained?0:recoil*5;
        return new(new(1+compression*.35f,1-compression,1+compression*.35f),Vector3.Zero,
            new(direction.Y*angle,0,-direction.X*angle));
    }
    public static float GuardYaw(double leftCheek,double rightCheek)=>rightCheek>leftCheek+10?15:-15;
    public static int SingingMouthFrame(float age)
    {
        float beat=MathF.Max(0,age)%.72f;
        return beat<.12f?4:beat<.30f?0:beat<.51f?2:3;
    }
    // Small motions supplement the primary action. None moves the simulation's body or heals it.
    public static ReactionMotion? Sample(string pose,float age,Vector2? impactDirection=null)
    {
        age=MathF.Max(0,age);
        float settle=MathF.Exp(-age*3),pulse=MathF.Sin(age*7),breath=MathF.Sin(age*2);
        Vector3 scale=Vector3.One,offset=Vector3.Zero,rotation=Vector3.Zero;
        switch(pose)
        {
            case "greet":rotation.X=-5*settle;rotation.Z=3*MathF.Sin(age*3);break;
            case "startle":scale=new(1+.025f*settle,1-.06f*settle,1+.025f*settle);rotation.X=4*settle;break;
            case "defiant":scale=new(1.04f,1.015f,1.035f);rotation.Z=3*MathF.Sin(age*4)*settle;rotation.X=-3;break;
            case "comfort":scale=new(1.02f,.97f+breath*.008f,1.01f);rotation.Z=2*breath;break;
            case "sing":rotation.Z=3*MathF.Sin(age*5.2f);scale.Y=1+.012f*MathF.Sin(age*10.4f);break;
            case "carry-tense":scale=new(1.025f,.95f,1.02f);rotation.X=-4;rotation.Z=MathF.Sin(age*11)*1.2f;break;
            case "carry-ease":scale=new(1+.025f*settle,1-.05f*settle,1+.02f*settle);rotation.X=-4*settle;break;
            case "poke":case "flick":case "slap":case "push":
                var direction=impactDirection??new Vector2(0,-1);
                direction=direction.LengthSquared()>.00001f?Vector2.Normalize(direction):new(0,-1);
                float impulse=pose=="poke"?6*MathF.Cos(age*10)*settle:
                    pose=="flick"?MathF.Sin(age*16)*8*settle:MathF.Sin(age*12)*16*MathF.Exp(-age*2);
                rotation=new(direction.Y*impulse,0,-direction.X*impulse);
                if(pose=="poke")offset=new(direction.X*.018f*settle,0,direction.Y*.018f*settle);
                break;
            case "land":
                float compression=.15f*MathF.Cos(age*10)*MathF.Exp(-age*4);
                scale=new(1+compression*.5f,1-compression,1+compression*.5f);break;
            case "settle":scale=new(1+.035f*settle,1-.06f*settle,1+.025f*settle);break;
            case "swallow":rotation.X=-2*MathF.Sin(MathF.Min(age/.6f,1)*MathF.PI);break;
            case "ball-bump":
                float push=MathF.Sin(MathF.Min(age/.55f,1)*MathF.PI);
                var ballDirection=impactDirection??new Vector2(0,1);
                ballDirection=ballDirection.LengthSquared()>.00001f?Vector2.Normalize(ballDirection):new(0,1);
                rotation=new(ballDirection.Y*7*push,0,-ballDirection.X*7*push);offset=new(ballDirection.X*.035f*push,0,ballDirection.Y*.035f*push);break;
            case "help":case "thirsty":rotation.X=-4;rotation.Z=2*MathF.Sin(age*3);scale.Y=1.015f;break;
            case "careful-step":rotation.Z=MathF.Sin(age*5)*2.5f;scale.Y=1-MathF.Abs(MathF.Sin(age*5))*.015f;break;
            case "nursing":scale=new(1.015f,.97f+breath*.006f,1.01f);rotation.Z=1.2f*breath;break;
            case "confused":case "inspect":rotation.Z=5*MathF.Sin(age*2);break;
            case "blocked":rotation.Y=MathF.Sin(age*3)*12;scale.Y=.98f;break;
            case "held":scale=new(1.025f,.95f,1.025f);rotation.Z=pulse*1.5f;break;
            case "box":case "frame":rotation.Z=pulse*2;rotation.Y=3*MathF.Sin(age*2);break;
            case "hanging":rotation.Z=3*MathF.Sin(age*2);break;
            case "accessory-watch":rotation.X=-3;rotation.Z=2*MathF.Sin(age*2);break;
            case "rest-invite":rotation.Z=3*MathF.Sin(age*2);break;
            default:return null;
        }
        return new(scale,offset,rotation);
    }
}
