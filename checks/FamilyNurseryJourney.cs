using Yukkuri.Simulation;

// Shared by the two explicit journey projects only. This is player-assisted
// nursery care after a real birth, not proof of unattended natural upbringing.
static class FamilyNurseryJourney
{
    public static void Run(WorldSimulation sim,Creature mother,Creature father,string birthId)
    {new Nursery(sim,mother,father,birthId).Run();}

    private sealed class Nursery(WorldSimulation sim,Creature mother,Creature father,string birthId)
    {
        private readonly Creature[] babies=sim.State.Creatures.Where(c=>c.Family.BirthEventId==birthId).ToArray();
        private readonly Dictionary<string,HashSet<RoomObjectKind>> eaten=new();
        private readonly Dictionary<string,(RoomObjectKind Kind,string Carrier)> transported=new();
        private readonly Dictionary<string,double> initialWarmth=new();
        private readonly HashSet<string> finishedMouth=new();
        private double started,recoveryRemaining,ageAtStart;

        private static void Require(bool ok,string message){if(!ok)throw new Exception("nursery: "+message);}
        private bool CompleteIntake(Creature c)=>eaten[c.Id].Contains(RoomObjectKind.Meal)&&eaten[c.Id].Contains(RoomObjectKind.Water);
        private bool Complete()=>mother.Family.Pregnancy.Phase==PregnancyPhase.None&&babies.All(c=>CompleteIntake(c)&&
            finishedMouth.Contains(c.Id)&&c.Fullness>35&&c.Interaction.Hydration>35&&c.Family.Warmth>initialWarmth[c.Id]);

        public void Run()
        {
            Require(babies.Length==mother.Family.Pregnancy.PlannedChildren&&babies.Length>0&&sim.State.CompletedBirths.Contains(birthId),
                "tail did not start from the actual complete brood");
            Require(mother.Family.Pregnancy.Phase==PregnancyPhase.Recovering&&!mother.Family.Pregnancy.Forced,
                "ordinary birth did not leave actual maternal recovery");
            started=sim.State.WorldSeconds;recoveryRemaining=mother.Family.Pregnancy.RecoveryRemaining;
            // The previous care loop may finish another real mouthful after the
            // birth tick. Newborn body age retains those elapsed world seconds.
            ageAtStart=babies.Min(c=>c.Body.AgeSeconds);
            double originalRecovery=FamilyRules.RecoverySeconds(mother.Family.Pregnancy.Method);
            // Hunger, thirst or pain may legitimately pause recovery while the
            // babies still age. They can lengthen this sum, never shorten it.
            Require(recoveryRemaining>0&&recoveryRemaining<=originalRecovery&&recoveryRemaining+ageAtStart>=originalRecovery-.2,
                "original maternal recovery was shortened before nursery care");
            sim.SetAutonomousBreeding(false); // Public permission; already-started recovery remains intact.
            Require(mother.Family.Pregnancy.Phase==PregnancyPhase.Recovering,"new-breeding permission erased existing recovery");
            foreach(var baby in babies)
            {
                eaten[baby.Id]=new();initialWarmth[baby.Id]=baby.Family.Warmth;
                Require(baby.Family.MotherId==mother.Id&&baby.Family.FatherId==father.Id&&baby.IsYoung,"actual parent/child identity changed");
                Require(sim.ExecuteInteraction(baby.Id,new(InteractionAction.Wrap,BodyPart.Side)).Success&&
                    baby.Interaction.Restraint==RestraintKind.Wrap,"real player wrapping for warmth failed");
            }
            double nextCare=sim.State.WorldSeconds;
            while(!Complete()&&sim.State.WorldSeconds-started<1200)
            {
                Require(mother.Condition is not (PhysicalCondition.Dead or PhysicalCondition.Critical)&&
                    father.Condition is not (PhysicalCondition.Dead or PhysicalCondition.Critical)&&
                    babies.All(c=>c.Condition is not (PhysicalCondition.Dead or PhysicalCondition.Critical)),"ordinary nursery suffered a physical crisis");
                if(sim.State.WorldSeconds>=nextCare)
                {
                    nextCare=sim.State.WorldSeconds+1;
                    MaintainAdults();ArrangeAndSupply(mother);ArrangeAndSupply(father);RemoveEmptySupplies();
                }
                Frame();
            }
            Require(Complete(),"real nursery did not finish: phase="+mother.Family.Pregnancy.Phase+", remaining="+
                mother.Family.Pregnancy.RecoveryRemaining+"; "+string.Join("; ",babies.Select(c=>c.Name+" "+
                    string.Join(",",eaten[c.Id])+" needs="+c.Fullness+"/"+c.Interaction.Hydration+" warmth="+c.Family.Warmth)));
            Require(mother.Condition is not (PhysicalCondition.Dead or PhysicalCondition.Critical)&&
                father.Condition is not (PhysicalCondition.Dead or PhysicalCondition.Critical)&&
                babies.All(c=>c.Condition is not (PhysicalCondition.Dead or PhysicalCondition.Critical)),
                "last nursery frame left a parent or child in a physical crisis");
            Require(sim.State.WorldSeconds-started+1e-6>=recoveryRemaining,"maternal recovery was shortened");
            Require(sim.State.WorldSeconds-started+ageAtStart+.2>=FamilyRules.RecoverySeconds(mother.Family.Pregnancy.Method),
                "full original maternal recovery duration did not elapse");
            Require(mother.Fullness>35&&mother.Interaction.Hydration>35&&mother.Pain<40&&
                mother.Interaction.Emotion.Alarm<12,"maternal body/safety conditions were lost during recovery");
            Require(sim.State.Creatures.Where(c=>c.Family.BirthEventId==birthId).Select(c=>c.Id).Order().SequenceEqual(babies.Select(c=>c.Id).Order())&&
                sim.State.CompletedBirths.Contains(birthId),"brood identity/count was replayed or lost");
            foreach(var baby in babies)
            {
                Require(baby.Family.Peers.TryGetValue(mother.Id,out var m)&&baby.Family.Peers.TryGetValue(father.Id,out var f)&&
                    m.CareSeconds+f.CareSeconds>0,"actual parental delivery never left care evidence");
                // Both required intakes have completed at least once. A later
                // genuine mouthful may still be ongoing; release does not erase it.
                Require(sim.ExecuteInteraction(baby.Id,new(InteractionAction.Unwrap,BodyPart.Side)).Success,
                    "nursery wrapping could not be released after care");
            }
            SaveStore.Validate(sim.State);
            Console.WriteLine("PASS actual "+mother.Family.Pregnancy.Method+" brood "+birthId+" ate transported meals and water, stayed warm, maternal recovery finished after "+
                (sim.State.WorldSeconds-started).ToString("F1")+" real world seconds");
        }

        private void Frame()
        {
            sim.SetInteractionHand(sim.State.PlayerX,sim.State.PlayerZ,false);
            sim.AdvanceInteractionPresentation(.1);sim.Advance(.1);
            foreach(var obj in sim.State.RoomObjects)
                if(obj.CarrierId is { } carrier&&(carrier==mother.Id||carrier==father.Id))transported[obj.Id]=(obj.Kind,carrier);
            foreach(var baby in babies)
            {
                foreach(var intake in baby.Interaction.Digestion.RecentIntakes)
                    if(intake.At>=started&&intake.SourceId is { } id&&transported.TryGetValue(id,out var source)&&
                        intake.Kind==source.Kind&&source.Kind is RoomObjectKind.Meal or RoomObjectKind.Water)
                    {
                        eaten[baby.Id].Add(source.Kind);
                        var obj=sim.State.RoomObjects.Find(o=>o.Id==id);
                        Require(obj==null||obj.Portions==0,"accepted transported portion remained available a second time");
                    }
                if(CompleteIntake(baby)&&baby.Interaction.EatingRemaining<=0&&!baby.Interaction.NaturalIntakePending)
                    finishedMouth.Add(baby.Id);
            }
            if(sim.State.WorldSeconds-started+1e-6<recoveryRemaining)
                Require(mother.Family.Pregnancy.Phase==PregnancyPhase.Recovering,"recovery ended before its original clock");
        }

        private void MaintainAdults()
        {
            foreach(var c in sim.State.Creatures.Where(c=>!c.IsYoung).ToArray())
            {
                if(c.Condition==PhysicalCondition.Dead)continue;
                if(WaterEnvironment.IsInWater(sim.State,c))
                {if(sim.State.WaterRescueTools==0)sim.BuyWaterRescueTool();sim.RescueFromWater(c.Id);continue;}
                if(c.Interaction.EatingRemaining<=0&&!c.Interaction.Physiology.Active)
                {
                    if(c.Interaction.Hydration<85)OfferAdult(c,InteractionAction.Drink);
                    else if(c.Fullness<85)OfferAdult(c,InteractionAction.FeedMeal);
                }
                if(c.Energy<65&&c.Interaction.RestRemaining<=0)sim.ExecuteInteraction(c.Id,new(InteractionAction.Rest));
            }
        }

        private void OfferAdult(Creature c,InteractionAction action)
        {
            if(action==InteractionAction.FeedMeal&&sim.UsesConsumableStock&&sim.State.Food==0)
                Require(sim.BuyFood(),"actual adult food replenishment failed");
            var input=new InteractionInput(action,BodyPart.Mouth,Force:.15,Duration:.1);
            if(!sim.BeginInteractionGesture(c.Id,input).Success)return;
            for(int n=0;n<12&&!sim.InteractionIntakeAccepted;n++)
            {sim.ContinueInteractionGesture(input,true);Frame();}
            sim.EndInteractionGesture();
        }

        private void ArrangeAndSupply(Creature parent)
        {
            var pending=babies.Where(c=>!CompleteIntake(c)||c.Fullness<55||c.Interaction.Hydration<55).ToArray();
            if(pending.Length==0)return;
            var target=pending.FirstOrDefault(c=>c.Id==parent.Family.CareTargetId)??
                pending.FirstOrDefault(c=>mother.Family.CareTargetId!=c.Id&&father.Family.CareTargetId!=c.Id)??pending[0];
            if(parent.Family.CompanionTargetId is { } companion)sim.EndCompanionship(parent.Id,companion);
            if(parent.Family.CareTargetId!=target.Id&&sim.FamilyCareReason(parent.Id,target.Id)==null)
                sim.ArrangeCare(parent.Id,target.Id);
            if(parent.Family.CareTargetId!=target.Id)return;
            // Both kinds are real one-portion items. Each stays beyond every
            // wrapped baby's reach, so successful nursery intake must follow delivery.
            if(target.Fullness<55)SupplyNearParent(parent,RoomObjectKind.Meal);
            if(target.Interaction.Hydration<55)SupplyNearParent(parent,RoomObjectKind.Water);
        }

        private void SupplyNearParent(Creature parent,RoomObjectKind kind)
        {
            if(sim.State.RoomObjects.Any(o=>o.Kind==kind&&o.RegionId==parent.RegionId&&!o.Stored&&!o.HeldByHand&&
                o.CarrierId==null&&o.Portions>0&&WorldSimulation.Distance(parent.X,parent.Z,o.X,o.Z)<2.5))return;
            if(kind==RoomObjectKind.Meal&&sim.UsesConsumableStock&&sim.State.Food==0)
                Require(sim.BuyFood(),"actual nursery food replenishment failed");
            for(int n=0;n<16;n++)
            {
                double angle=n*Math.Tau/16,x=parent.X+Math.Cos(angle)*.75,z=parent.Z+Math.Sin(angle)*.75;
                if(Math.Abs(x)>7.5||Math.Abs(z)>7.5||!WoodlandLayout.GroundFits(sim.State,parent.RegionId,x,z,.2)||
                    babies.Any(c=>WorldSimulation.Distance(c.X,c.Z,x,z)<=NaturalResources.Reach(c)+.1))continue;
                var before=sim.State.RoomObjects.Select(o=>o.Id).ToHashSet();int stock=sim.State.Food;
                var result=sim.ExecuteInteraction(parent.Id,new(kind==RoomObjectKind.Water?InteractionAction.PlaceWater:InteractionAction.PlaceMeal,
                    BodyPart.Side,X:x,Z:z));
                if(!result.Success)continue;
                var source=sim.State.RoomObjects.Single(o=>!before.Contains(o.Id));
                Require(source.Kind==kind&&source.Portions==1&&source.CarrierId==null&&
                    sim.State.Food==stock-(kind==RoomObjectKind.Meal&&sim.UsesConsumableStock?1:0),"real source placement/stock transaction was not one portion");
                return;
            }
        }

        private void RemoveEmptySupplies()
        {
            foreach(var source in sim.State.RoomObjects.Where(o=>o.Kind is RoomObjectKind.Meal or RoomObjectKind.Water&&
                !o.Stored&&!o.HeldByHand&&o.CarrierId==null&&o.Portions==0&&NaturalResources.Consumer(sim.State,o)==null).ToArray())
            {
                int stock=sim.State.Food;Require(sim.PutAwayRoomObject(source.Id),"empty nursery supply could not be cleared");
                Require(sim.State.Food==stock,"empty supply refunded a nonexistent portion");
            }
        }
    }
}
