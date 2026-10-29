using System.Numerics;
using System.Text.Json;
using Yukkuri.Simulation;

public static class IntakeCombinationAudit
{
    public static int Run(string output,bool animate=false)
    {
        var cases=new List<object>();int tested=0,accepted=0,gaps=0,overrides=0,lostBlockedPortions=0,mobileAttempts=0,mobileRecovered=0,motionFailures=0;float worst=0,worstMotion=0;
        foreach(bool wrapped in new[]{false,true})foreach(bool dressed in new[]{false,true})
        foreach(string species in new[]{"reimu","marisa"})foreach(float size in new[]{.73f,1f,1.03f})
        foreach(float bedOffset in new[]{0f,.5f,.9f})foreach(float heading in new[]{0f,MathF.PI*.5f,MathF.PI,-MathF.PI*.5f})
        foreach(float reach in new[]{.4f,.792f})foreach(var kind in new[]{RoomObjectKind.Meal,RoomObjectKind.Sweet,RoomObjectKind.Water})
        {
            var sim=WorldSimulation.CreateInteractionPlaytest();var state=sim.State;var c=sim.Selected!;
            state.Creatures.RemoveAll(o=>o.Id!=c.Id);state.RoomObjects.Clear();c.X=0;c.Z=bedOffset;c.TargetX=c.X;c.TargetZ=c.Z;
            c.IsYoung=size<1;c.IsElder=size>1;c.Body.SpeciesId=species;c.Fullness=NaturalResources.IsFood(kind)?30:90;c.Interaction.Hydration=NaturalResources.IsWater(kind)?30:90;c.Energy=90;
            c.Interaction.FootIntegrity=20;c.Interaction.NextAutonomyAt=10000;c.Interaction.Restraint=wrapped?RestraintKind.Wrap:RestraintKind.None;
            if(dressed){c.Interaction.DressingPart=BodyPart.Bottom;c.Interaction.DressingRemaining=100;}
            state.RoomObjects.Add(new(){Kind=RoomObjectKind.Cushion});
            float distance=reach*size;double x=c.X+Math.Sin(heading)*distance,z=c.Z+Math.Cos(heading)*distance;
            var source=new RoomObject{Kind=kind,X=x,Z=z,Portions=1};state.RoomObjects.Add(source);
            bool preparationOverridden=false;
            for(int frame=0;frame<30&&c.Interaction.Digestion.Exposures==0;frame++)
            {
                sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);
                if(c.Interaction.NaturalIntakePending&&InteractionPresentation.Pose(c) is not ("eat" or "sip"))preparationOverridden=true;
            }
            tested++;bool entered=c.Interaction.Digestion.Exposures>0;float gap=0,angle=0,motionGap=0;
            if(entered)
            {
                accepted++;var life=c.Interaction;var support=new ActorNaturalSupport(c,state.RoomObjects,size);
                var pose=ActorNaturalPose.Sample(NaturalResources.IsWater(kind)?"sip":"eat",.5f,life.IntakeFacing,true,life.EatingKind,life.EatingRemaining,
                    sourceKind:kind,sourceDistance:life.GroundIntakeDistance,sourceHeight:life.GroundIntakeSourceHeight,bodyScale:size,support:support)!.Value;
                var rotation=Quaternion.CreateFromYawPitchRoll(pose.RotationDegrees.Y*MathF.PI/180,pose.RotationDegrees.X*MathF.PI/180,0);
                float lift=support.Lift(pose.Scale,rotation,pose.Offset);
                var mouth=(Vector3.Transform(new Vector3(0,-.175f,ActorFaceSurface.Height(0,-.175f,.012f))*pose.Scale,rotation)+pose.Offset+Vector3.UnitY*(.46f+lift))*size;
                var target=ActorNaturalPose.SourceMouthTarget(kind,distance,(float)source.Height);
                gap=Vector3.Distance(mouth,new(MathF.Sin(heading)*target.X,target.Y,MathF.Cos(heading)*target.X));angle=pose.RotationDegrees.X;
                worst=MathF.Max(worst,gap);if(gap>.005f)gaps++;
                if(preparationOverridden)overrides++;
                if(animate)
                {
                    foreach(var schedule in new[]{new[]{1/30f},new[]{1/60f},new[]{1/144f},new[]{.01f,.035f,.008f,.019f,.041f}})
                        motionGap=MathF.Max(motionGap,AnimatedGap(c,state.RoomObjects,source,size,distance,heading,schedule));
                    worstMotion=MathF.Max(worstMotion,motionGap);if(motionGap>.005f)motionFailures++;
                }
            }
            bool? recovered=null;
            if(!entered)
            {
                if(source.Portions!=1||c.Interaction.NaturalIntakePending)lostBlockedPortions++;
                if(!wrapped)
                {
                    mobileAttempts++;c.Interaction.FootIntegrity=100;
                    for(int frame=0;frame<200&&c.Interaction.Digestion.Exposures==0;frame++)
                    {sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}
                    recovered=c.Interaction.Digestion.Exposures>0;if(recovered.Value)mobileRecovered++;
                }
            }
            cases.Add(new{wrapped,dressed,species,size,bedOffset,heading,reach,kind=kind.ToString(),entered,preparationOverridden,sourceHeight=source.Height,gap,angle,motionGap,recovered,finalX=c.X,finalZ=c.Z});
            if(animate&&tested%288==0)Console.WriteLine($"Combination progress: {tested}/1728, motion failures {motionFailures}, worst {worstMotion:F6}");
        }
        var result=new{at=DateTimeOffset.UtcNow,scope="Actual pure simulation intake followed by shared source geometry. Optional production pose smoothing at 30/60/144fps and irregular steps, starting facing away. Blocked portions retained; unwrapped blocked cases regain foot movement and try alternate standing points for 10 seconds. No native renderer.",tested,accepted,gaps,overrides,lostBlockedPortions,mobileAttempts,mobileRecovered,animate,motionFailures,worstMotion,worstGap=worst,success=gaps==0&&overrides==0&&lostBlockedPortions==0&&mobileAttempts==mobileRecovered&&motionFailures==0,cases};
        File.WriteAllText(output,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Intake combinations: {tested} cases, {accepted} entered, {gaps} unreachable credited intakes, {overrides} preparation pose conflicts, worst gap {worst:F6}");
        Console.WriteLine($"Blocked portions lost: {lostBlockedPortions}; restored mobility recovered {mobileRecovered}/{mobileAttempts}");
        if(animate)Console.WriteLine($"Animated combinations failed: {motionFailures}; worst contact gap {worstMotion:F6}");
        return result.success?0:1;
    }
    private static float AnimatedGap(Creature c,IReadOnlyList<RoomObject> objects,RoomObject source,float size,float distance,float heading,float[] schedule)
    {
        var transition=new ActorPoseSmoothing();transition.Advance(Vector3.One,Vector3.Zero,Quaternion.CreateFromAxisAngle(Vector3.UnitY,heading+MathF.PI),0,false);
        var life=c.Interaction;var support=new ActorNaturalSupport(c,objects,size);
        float end=(float)NaturalResources.BiteContactSeconds;int frame=0;
        for(float time=0;time<end;)
        {
            float dt=MathF.Min(schedule[frame++%schedule.Length],end-time);time+=dt;
            var motion=ActorNaturalPose.Sample(NaturalResources.IsWater(source.Kind)?"sip":"eat",time,heading,true,life.EatingKind,InteractionLife.IntakeDuration(life.EatingKind)-time,
                sourceKind:source.Kind,sourceDistance:distance,sourceHeight:source.Height,bodyScale:size,support:support)!.Value;
            var rotation=Quaternion.CreateFromYawPitchRoll(motion.RotationDegrees.Y*MathF.PI/180,motion.RotationDegrees.X*MathF.PI/180,0);
            transition.Advance(motion.Scale,motion.Offset,rotation,dt,false,true);
        }
        float lift=support.Lift(transition.Scale,transition.Rotation,transition.Offset);
        var mouth=(Vector3.Transform(new Vector3(0,-.175f,ActorFaceSurface.Height(0,-.175f,.012f))*transition.Scale,transition.Rotation)+transition.Offset+Vector3.UnitY*(.46f+lift))*size;
        var target=ActorNaturalPose.SourceMouthTarget(source.Kind,distance,(float)source.Height);
        return Vector3.Distance(mouth,new(MathF.Sin(heading)*target.X,target.Y,MathF.Cos(heading)*target.X));
    }
}
