using System.Numerics;
using Yukkuri.Simulation;

public static class HopIntakeAudit
{
    public static bool Run()
    {
        float worst=0,largestFormerSnap=0;int cases=0;
        foreach(string species in new[]{"reimu","marisa"})
        foreach(float size in new[]{.73f,1f,1.03f})
        foreach(float hopAge in new[]{0f,.052f,.15f,.235f,.35f,.4175f})
        foreach(float heading in new[]{0f,MathF.PI*.5f,MathF.PI})
        foreach(var kind in new[]{RoomObjectKind.Leaves,RoomObjectKind.RiverWater,RoomObjectKind.Meal})
        foreach(var schedule in new[]{new[]{1/30f},new[]{1/60f},new[]{1/144f},new[]{.01f,.035f,.008f,.019f,.041f}})
        {
            var state=WorldSimulation.CreateInteractionPlaytest().State;var c=state.Creatures[0];
            c.X=0;c.Z=0;c.Body.SpeciesId=species;c.IsYoung=size<1;c.IsElder=size>1;state.RoomObjects.Clear();
            var support=new ActorNaturalSupport(c,state.RoomObjects,size);
            var hop=ActorHopMotion.Sample(hopAge);
            var forward=new Vector3(MathF.Sin(heading),0,MathF.Cos(heading));
            var previousOffset=-forward*(.35f*size)+Vector3.UnitY*hop.Lift*size;
            var previousRotation=Quaternion.CreateFromYawPitchRoll(heading,hop.Pitch*MathF.PI/180,0);
            float previousLift=support.Lift(hop.Scale,previousRotation,previousOffset/size);
            previousOffset.Y+=previousLift*size;
            largestFormerSnap=MathF.Max(largestFormerSnap,previousOffset.Length());
            var transition=new ActorPoseSmoothing();
            transition.Advance(Vector3.One*size,Vector3.Zero,Quaternion.Identity,0,false);
            transition.BeginFrom(hop.Scale*size,previousOffset,previousRotation);
            transition.Advance(Vector3.One*size,Vector3.Zero,Quaternion.Identity,0,false,true);
            if(transition.Scale!=hop.Scale*size||transition.Offset!=previousOffset||transition.Rotation!=previousRotation)return false;
            float distance=.792f*size,time=0;int frame=0;
            var intake=kind==RoomObjectKind.RiverWater?IntakeKind.Water:IntakeKind.Meal;
            while(time<(float)NaturalResources.BiteContactSeconds)
            {
                float dt=MathF.Min(schedule[frame++%schedule.Length],(float)NaturalResources.BiteContactSeconds-time);time+=dt;
                var motion=ActorNaturalPose.Sample(intake==IntakeKind.Water?"sip":"eat",time,heading,true,intake,
                    InteractionLife.IntakeDuration(intake)-time,sourceKind:kind,sourceDistance:distance,bodyScale:size,support:support)!.Value;
                var rotation=Quaternion.CreateFromYawPitchRoll(motion.RotationDegrees.Y*MathF.PI/180,motion.RotationDegrees.X*MathF.PI/180,0);
                transition.Advance(motion.Scale*size,motion.Offset*size,rotation,dt,false,true);
            }
            float lift=support.Lift(transition.Scale/size,transition.Rotation,transition.Offset/size);
            var mouth=Vector3.Transform(new Vector3(0,-.175f,ActorFaceSurface.Height(0,-.175f,.012f))*transition.Scale,transition.Rotation)
                +transition.Offset+Vector3.UnitY*(.46f+lift)*size;
            var target=ActorNaturalPose.SourceMouthTarget(kind,distance);
            float gap=Vector3.Distance(mouth,forward*target.X+Vector3.UnitY*target.Y);worst=MathF.Max(worst,gap);cases++;
            if(gap>.005f)
            {Console.WriteLine($"Hop intake gap {species}/{size}/{hopAge}/{heading}/{kind}/{schedule[0]}: {gap:F6}");return false;}
        }
        Console.WriteLine($"Hop-to-intake transitions: {cases}, maximum contact gap {worst:F6}, former instantaneous offset removal up to {largestFormerSnap:F6}");
        return true;
    }
}
