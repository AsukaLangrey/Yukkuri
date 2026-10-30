using Yukkuri.Simulation;

// Execute only on the authorised independent remote machine. This is a journey,
// not a fixture with pre-filled trust, safety, interest or reciprocal affection.
try
{
    var sim=WorldSimulation.CreateInteractionPlaytest(20261031);
    var mother=sim.State.Creatures.Single(c=>c.Id=="interaction-oil-0");
    var father=sim.State.Creatures.Single(c=>c.Id=="interaction-oil-1");
    Require(mother.Trust==38&&mother.Attachment==18&&mother.Interaction.HumanBond.Familiarity==0&&
        mother.Interaction.HumanBond.Safety==0&&mother.Family.PartnerIds.Count==0&&
        mother.Family.BreedingInterest==0,"default stranger starting point changed");
    var journey=new AffectionJourney(sim,mother,father);
    journey.Until(()=>mother.Interaction.Memories.GetValueOrDefault("human-company")>0,
        3600,true,false,"default stranger did not become a willing human companion");
    Require(mother.Trust>=60&&mother.Attachment>=40&&mother.Interaction.HumanBond.Familiarity>=8&&
        mother.Interaction.HumanBond.Safety>=8,"company skipped earned relationship conditions");
    Require(journey.AcceptedMeals>0&&journey.AcceptedDrinks>0&&journey.Conversations>0,
        "human relationship was not built through actual care and conversation");
    journey.Trace("earned human companionship");

    journey.Until(()=>DeepBond(mother,father)&&DeepBond(father,mother)&&
        mother.Family.PartnerIds.Contains(father.Id)&&father.Family.PartnerIds.Contains(mother.Id),
        3000,false,true,"actual reciprocal companionship never became a deep partnership");
    Require(sim.State.FamilyRelationshipEvents.Any(e=>
        e.FirstId==mother.Id&&e.SecondId==father.Id||e.FirstId==father.Id&&e.SecondId==mother.Id),
        "partnership lacks its real relationship history");
    journey.Trace("earned mutual deep partnership");

    sim.SetAutonomousBreeding(true);
    journey.Until(()=>mother.Family.Pregnancy.Phase==PregnancyPhase.Gestating,
        1800,false,true,"safe mature partners did not choose autonomous pregnancy");
    var pregnancy=mother.Family.Pregnancy;
    Require(!pregnancy.Forced&&pregnancy.FatherId==father.Id&&pregnancy.EventId!=null,
        "autonomous journey bypassed the ordinary pregnancy process");
    string birthId=pregnancy.EventId!;
    int planned=pregnancy.PlannedChildren;
    var before=sim.State.Creatures.Select(c=>c.Id).ToHashSet();
    journey.Trace("autonomous "+pregnancy.Method+" pregnancy");
    journey.Until(()=>sim.State.CompletedBirths.Contains(birthId),900,false,false,
        "ordinary care did not sustain the chosen pregnancy through birth");
    Require(sim.State.Creatures.Count(c=>!before.Contains(c.Id)&&c.Family.BirthEventId==birthId)==planned&&
        mother.Family.Pregnancy.Phase==PregnancyPhase.Recovering,
        "birth omitted children or the maternal recovery period");
    Require(mother.Interaction.Emotion.Alarm<12&&mother.Interaction.Emotion.SafeSeconds>=45,
        "autonomous family journey lost its continuous safety prerequisites");
    SaveStore.Validate(sim.State);
    journey.Trace("birth and maternal recovery");
    Console.WriteLine("PASS default stranger -> trusted company -> reciprocal partnership -> autonomous pregnancy -> birth");
    return 0;
}
catch(Exception error)
{
    Console.WriteLine("FAIL affection journey: "+error);
    return 1;
}

static void Require(bool condition,string why){if(!condition)throw new Exception(why);}
static bool DeepBond(Creature from,Creature to)=>from.Family.Peers.TryGetValue(to.Id,out var bond)&&
    bond.Familiarity>=70&&bond.Affection>=70&&bond.Fear<15&&bond.Resentment<15;

sealed class AffectionJourney(WorldSimulation sim,Creature mother,Creature father)
{
    public int AcceptedMeals { get; private set; }
    public int AcceptedDrinks { get; private set; }
    public int Conversations { get; private set; }
    public int CompanionInvitations { get; private set; }
    private double nextCare,nextTalk,nextInvitation,nextHandPosition;
    private double handX=mother.X+1.8,handZ=mother.Z;
    private bool showHand;

    public void Trace(string milestone)
    {
        var bond=mother.Family.Peers.GetValueOrDefault(father.Id);
        Console.WriteLine($"{milestone}: t={sim.State.WorldSeconds:F1}, trust={mother.Trust:F1}, attachment={mother.Attachment:F1}, "+
            $"handSafety={mother.Interaction.HumanBond.Safety:F1}, familiarity={bond?.Familiarity:F1}, affection={bond?.Affection:F1}, "+
            $"interest={mother.Family.BreedingInterest:F1}, alarm={mother.Interaction.Emotion.Alarm:F1}, "+
            $"needs={mother.Fullness:F1}/{mother.Interaction.Hydration:F1}/{mother.Energy:F1}, activity={mother.Activity}");
    }

    public void Until(Func<bool> finished,double timeout,bool buildHuman,bool buildFamily,string failure)
    {
        double deadline=sim.State.WorldSeconds+timeout;
        showHand=buildHuman;
        while(!finished()&&sim.State.WorldSeconds<deadline)
        {
            if(mother.Condition is PhysicalCondition.Dead or PhysicalCondition.Critical||
                father.Condition is PhysicalCondition.Dead or PhysicalCondition.Critical)
                throw new Exception("ordinary care journey suffered a physical crisis before its milestone");
            if(showHand&&sim.State.WorldSeconds>=nextHandPosition&&mother.Interaction.HumanCompany.Until<=0)
            {handX=Math.Clamp(mother.X+1.8,-5.5,5.5);handZ=mother.Z;nextHandPosition=sim.State.WorldSeconds+60;}
            if(sim.State.WorldSeconds>=nextCare)
            {nextCare=sim.State.WorldSeconds+4;CareForHousehold();}
            if(buildHuman&&sim.State.WorldSeconds>=nextTalk&&mother.Interaction.HumanCompany.Until<=0)
            {
                nextTalk=sim.State.WorldSeconds+8;
                if(sim.ExecuteInteraction(mother.Id,new(InteractionAction.Comfort)).Success)Conversations++;
            }
            if(buildFamily&&sim.State.WorldSeconds>=nextInvitation)
            {
                nextInvitation=sim.State.WorldSeconds+26;
                BringPartnersWithinSight();
                string result=sim.InviteCompanion(mother.Id,father.Id);
                if(result.Contains("愿意试着靠近"))CompanionInvitations++;
            }
            Frame();
        }
        if(!finished()){Trace("timeout");throw new Exception(failure);}
    }

    private void Frame()
    {
        sim.SetInteractionHand(handX,handZ,showHand);
        sim.AdvanceInteractionPresentation(.1);
        sim.Advance(.1);
    }

    private void CareForHousehold()
    {
        // Keep the original inhabitants and natural environment. Care uses the
        // same finite mouthful, rest and rescue commands as ordinary play.
        foreach(var c in sim.State.Creatures.ToArray())
        {
            if(c.Condition==PhysicalCondition.Dead)continue;
            if(WaterEnvironment.IsInWater(sim.State,c))
            {if(sim.State.WaterRescueTools==0)sim.BuyWaterRescueTool();sim.RescueFromWater(c.Id);continue;}
            if(c.Interaction.EatingRemaining<=0&&!c.Interaction.Physiology.Active)
            {
                if(c.Interaction.Hydration<90)Offer(c,InteractionAction.Drink);
                else if(c.Fullness<90)Offer(c,InteractionAction.FeedMeal);
            }
            if(c.Energy<85&&c.Interaction.RestRemaining<=0)
                sim.ExecuteInteraction(c.Id,new(InteractionAction.Rest));
        }
    }

    private void Offer(Creature c,InteractionAction action)
    {
        var input=new InteractionInput(action,BodyPart.Mouth,.15,.1,X:c.X,Z:c.Z);
        if(!sim.BeginInteractionGesture(c.Id,input).Success)return;
        bool accepted=false;
        for(int sample=0;sample<10&&!accepted;sample++)
        {
            sim.ContinueInteractionGesture(input,true);
            accepted=sim.InteractionIntakeAccepted;
            Frame();
        }
        sim.EndInteractionGesture();
        if(accepted){if(action==InteractionAction.Drink)AcceptedDrinks++;else AcceptedMeals++;}
    }

    private void BringPartnersWithinSight()
    {
        if(Math.Sqrt(Math.Pow(mother.X-father.X,2)+Math.Pow(mother.Z-father.Z,2))<4.5||
            !WorldSimulation.CanSelfMove(father)||father.Interaction.Resting||mother.Interaction.Resting||
            father.Interaction.EatingRemaining>0||mother.Interaction.EatingRemaining>0)return;
        foreach(double offset in new[]{1.8,-1.8})
        {
            double x=mother.X+offset,z=mother.Z;
            if(Math.Abs(x)>5.5||!sim.CanPlaceInteractionAt(father.Id,x,z))continue;
            if(!sim.BeginInteractionGesture(father.Id,new(InteractionAction.Lift,BodyPart.Side,.1,Height:.8)).Success)return;
            sim.ExecuteInteraction(father.Id,new(InteractionAction.PutDown,BodyPart.Side,X:x,Z:z));
            sim.EndInteractionGesture();
            return;
        }
    }
}
