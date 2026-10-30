namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    private InteractionFeedback ApplyCue(Creature c, InteractionAction action)
    {
        var i = c.Interaction;
        if (c.Energy < 20 || c.Fullness < 20 || c.Fear > 65)
            return Feedback(c, "too-tired", Expression.Exhausted, "先处理疲劳、饥饿或恐惧，才能专心练习。", "withdraw", false, true);
        if (!CanSelfMove(c) && action is InteractionAction.TeachFollow or InteractionAction.Follow or InteractionAction.TeachStop)
            return Feedback(c, "cannot-walk", Expression.Calling, "它现在身体做不到，这不是不听话。", "help", false, true);
        if(action==InteractionAction.TeachStop&&State.WorldSeconds-i.LastWalkAt>1.5)return Unavailable("等它实际走动时，再示范停下；站着不动不算学会停步。 ");
        if(action is InteractionAction.Wait or InteractionAction.TeachWait&&c.Fullness>=85)return Unavailable("它现在不想吃饭，先在真正想吃的时候练习等待。 ");
        if (i.ActiveCue != null)
        {
            if(action is not (InteractionAction.Stop or InteractionAction.TeachStop))return Unavailable("先完成眼前这次练习，或让它停下来。 ");
            // An interruption ends the attempt; it is not a successful lesson.
            i.ActiveCue=null;i.FollowPlayer=false;i.WaitRemaining=0;
        }
        var cue = action switch
        {
            InteractionAction.TeachFollow or InteractionAction.Follow => LearnedCue.Follow,
            InteractionAction.TeachStop or InteractionAction.Stop => LearnedCue.Stop,
            _ => LearnedCue.Wait
        };
        bool demonstration = action is InteractionAction.TeachFollow or InteractionAction.TeachStop or InteractionAction.TeachWait;
        if (!i.Learning.TryGetValue(cue, out var learning)) i.Learning[cue] = learning = new();
        if (demonstration && State.WorldSeconds - learning.LastPracticeAt < 5)
            return Unavailable("刚练过这一项，先让它在生活里试试，缓一会儿再教。 ");
        if (c.TrainingFatigue > 70) return Feedback(c, "too-tired", Expression.Exhausted, "连续练习太多，先休息。", "withdraw", false, true);
        i.ActiveCue = cue; i.CueDemonstration = demonstration; i.CueElapsed = 0;
        i.CueOriginX = c.X; i.CueOriginZ = c.Z; i.CueTravel = 0;
        double understanding = (demonstration ? .55 : .12) + c.Patience * .2 + learning.Familiarity / 140;
        i.CueUnderstood = Next() < Math.Clamp(understanding, .05, .98);
        learning.Attempts++; learning.LastPracticeAt = State.WorldSeconds;
        if (demonstration) { learning.HasDemonstration = true; learning.DemonstrationX = c.X; learning.DemonstrationZ = c.Z; }
        i.FollowPlayer = cue == LearnedCue.Follow;
        if (cue == LearnedCue.Wait)
        {
            i.WaitRemaining = 4;
            if (!State.RoomObjects.Any(o => o.RegionId==c.RegionId&&o.Portions>0&&o.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet && !o.Stored && Distance(c.X, c.Z, o.X, o.Z) < 2))
            {
                if (!State.UnlimitedResources && State.Food <= 0)
                { i.ActiveCue = null; i.WaitRemaining = 0; return Unavailable("等待练习需要眼前的一份食物。 "); }
                if (!State.UnlimitedResources) State.Food--;
                State.RoomObjects.Add(new() { Kind = RoomObjectKind.Meal,RegionId=c.RegionId, X = Math.Clamp(c.X + .8, -7, 7), Z = c.Z });
            }
        }
        i.RestRemaining=0;i.Resting=false;
        if (cue == LearnedCue.Stop) { i.FollowPlayer = false; i.WaitRemaining = 3; c.TargetX = c.X; c.TargetZ = c.Z; }
        return Feedback(c, demonstration ? "learn" : cue == LearnedCue.Follow ? "follow" : cue == LearnedCue.Stop ? "stop" : "wait",
            Expression.Curious, demonstration ? "示范已开始。回到普通时间，看它实际尝试。" : "指令已发出，观察它接下来的动作。", "learn");
    }

    private void CompleteCue(Creature c, bool success)
    {
        var i = c.Interaction;
        if (i.ActiveCue is not { } cue) return;
        var learning = i.Learning[cue];
        if (success)
        {
            learning.Successes++;
            if (i.CueDemonstration) learning.Familiarity = Math.Min(100, learning.Familiarity + 8 + c.Patience * 8);
            else
            {
                learning.Applications++;
                bool newPlace = Distance(i.CueOriginX, i.CueOriginZ, learning.DemonstrationX, learning.DemonstrationZ) > 1.5;
                learning.Familiarity = Math.Min(100, learning.Familiarity + (newPlace ? 5 : 2));
            }
            i.PendingPraise = cue switch { LearnedCue.Follow => "跟随", LearnedCue.Stop => "停下", _ => "等待" };
            i.PraiseRemaining = 12;
            Feedback(c, "learn-success", Expression.Delighted, $"它实际做到了{i.PendingPraise}，现在夸奖能联系到这件事。", "proud");
            RecordInteraction(c, "learn-" + cue, $"在这次情境中完成了{i.PendingPraise}。", 2);
        }
        else
        {
            if(cue==LearnedCue.Follow)i.FollowPlayer=false;
            Feedback(c, "learn-failed", Expression.Curious, "这次没有真正完成，可以调整示范或等它休息。", "confused");
        }
        c.TrainingFatigue = Clamp100(c.TrainingFatigue + (i.CueDemonstration ? 8 : 3));
        i.ActiveCue = null; i.WaitRemaining = 0;
    }

    // Active effects (swallowing, speech and an already initiated fall) can finish in close-up.
    // This never advances hunger, age, healing, training, or other passive world time.
    public void AdvanceInteractionPresentation(double delta)
    {
        if (State.Settings.Pause || !double.IsFinite(delta) || delta <= 0) return;
        double dt = Math.Min(delta, .1);
        if(!State.InteractionPlaytest)
        {
            // Old management advances mouths once in its normal tick. Only an
            // already accepted bite may finish through presentation in close-up.
            if(State.Mode==PlayMode.CloseUp)
                foreach(var c in State.Creatures)AdvanceMouthProgress(c,dt);
            return;
        }
        SynchronizeCareParcels();
        SettleRoomSupplies();
        foreach (var c in State.Creatures)
        {
            var i = c.Interaction;
            SynchronizeWaterSupport(c);
            SynchronizeHungerThreat(c);
            i.HitRemaining=Math.Max(0,i.HitRemaining-dt);
            if(i.HitRemaining<=0)i.HitCount=0;
            if(CurrentInteractionAction!=InteractionAction.Burn||InteractionGestureTarget!=c.Id||
                _interactionGesture?.ContactActive!=true)i.HeatTension=Math.Max(0,i.HeatTension-dt*12);
            if(c.Family.ColdStorage.Stored)continue;
            SynchronizeInterruptedPhysiology(c);
            ResetInterruptedAutonomySearch(c);
            SynchronizeWildSelfCare(c);
            ValidatePendingConversation(c);
            if(i.GroundIntakeSourceId!=null&&
                (c.Condition==PhysicalCondition.Dead||i.Carried||i.Falling||i.Supine||i.Height>.25||
                InteractionGestureTarget==c.Id&&InteractionGestureOwnsPose||
                i.ReactionPoseRemaining>0&&i.ReactionPose is "slap" or "poke" or "flick" or "push"||
                !State.RoomObjects.Any(o=>o.Id==i.GroundIntakeSourceId&&o.RegionId==c.RegionId&&!o.Stored&&!o.HeldByHand)))
                DetachNaturalIntake(c);
            bool mouthfulFinishing=AdvanceMouthProgress(c,dt);
            i.ReactionPoseRemaining=Math.Max(0,i.ReactionPoseRemaining-dt);
            i.SocialResponseRemaining=Math.Max(0,i.SocialResponseRemaining-dt);
            i.HarshResponseRemaining=Math.Max(0,i.HarshResponseRemaining-dt);
            i.BallContactCooldown=Math.Max(0,i.BallContactCooldown-dt);
            c.ReactionRemaining = Math.Max(0, c.ReactionRemaining - dt);
            if(mouthfulFinishing&&c.Condition is PhysicalCondition.Active or PhysicalCondition.Recovering&&c.Pain<=40&&c.Fear<=60&&i.ReactionSituation is "eat" or "eat-leaves" or "sweet" or "drink")
            {
                string description=i.GroundIntake?
                    i.EatingKind==IntakeKind.Water?"它自己喝好这一口，抬起头看了看周围。":"它把自己找到的这一口嚼好咽下，停了一小会儿。":
                    i.EatingKind==IntakeKind.Water?"这一小口水已经咽好了，杯子可以收回。":"这一口已经慢慢咽下。先收回空勺或手，再看它还想不想吃。";
                Feedback(c,"mouthful-done",Expression.Content,description,"swallow",false);
                i.ReactionPoseRemaining=.6;
                if(i.PendingConversation.HasValue)i.ConversationReactionSerial=c.ReactionSerial;
            }
            AdvancePendingConversation(c,dt);
            AdvanceSupportedLowering(c,dt);
            if (c.ReactionRemaining <= 0)
            {
                c.Speech = "";
                c.Expression = c.Condition == PhysicalCondition.Dead ? Expression.Exhausted :
                    c.Pain > 25 ? Expression.Hurt : c.Fear > 60||i.Emotion.Alarm>=25 ? Expression.Frightened :
                    !EmotionalReadiness.Settled(c)?Expression.Worried:
                    i.Resting ? Expression.Sleeping :
                    i.HeadSensitivity>30||i.CheekSensitivity>30?Expression.Worried:
                    !i.HasAccessory ? (i.Accessory.BorrowedForCare ? Expression.Curious : Expression.Worried) :
                    i.Accessory.Integrity < 90 ? Expression.Worried :
                    i.Digestion.Symptomatic?Expression.Worried:
                    c.Energy < 20 ? Expression.Exhausted : Expression.Neutral;
            }
            if (i.Carried || i.Restraint != RestraintKind.None) continue;
            if (i.Falling)
            {
                i.VerticalVelocity -= 8 * dt;
                i.Height += i.VerticalVelocity * dt;
                if (i.Height <= 0)
                {
                    double speed = Math.Abs(i.VerticalVelocity);
                    i.Height = 0; i.Falling = false; i.VerticalVelocity = 0;
                    ClearLandingOverlap(c);
                    bool inWater=WaterEnvironment.IsInWater(State,c);
                    double damage = State.Settings.Accidents&&!inWater ? Math.Max(0, speed - 2.8) * 2.2 : 0;
                    Distress(c, 2, damage, "跌落碰撞",BodyPart.Bottom);
                    RecordInteraction(c, "fall", "离开手的支撑后落地，记住了这次经历。", Math.Max(1, damage));
                    Feedback(c, damage > 0 ? "hurt" : "down", damage > 0 ? Expression.Hurt : Expression.Shy,
                        damage > 0 ? "落地受伤，查看身体和后续动作。" : "落地了，仍然受了惊。", "land");
                    if(inWater)EnterWater(c,true);
                }
            }
            if (Math.Abs(i.VelocityX) + Math.Abs(i.VelocityZ) > .005)
            {
                double nextX=Math.Clamp(c.X+i.VelocityX*dt,-7.5,7.5),nextZ=Math.Clamp(c.Z+i.VelocityZ*dt,-6,6);
                if(i.Height>1){c.X=nextX;c.Z=nextZ;}
                else if(!GroundMove(c,nextX,nextZ,false,allowWater:true)){i.VelocityX*= -.18;i.VelocityZ*= -.18;}
                double drag = Math.Exp(-dt * (i.Falling ? .4 : 4));
                i.VelocityX *= drag; i.VelocityZ *= drag;
                c.TargetX = c.X; c.TargetZ = c.Z;
            }
        }
        foreach (var o in State.RoomObjects.Where(o => o.Kind == RoomObjectKind.Ball && !o.Stored))
        {
            if(CurrentInteractionAction==InteractionAction.PlayBall&&InteractionGestureObject==o.Id)continue;
            AdvanceRoomBall(o,dt);
        }
        PruneConsumedSupplies();
    }

    private void TickInteractionWorld(double worldDt)
    {
        SettleRoomSupplies();
        foreach (var c in State.Creatures)
        {
            SynchronizeInterruptedPhysiology(c);
            if (c.Condition == PhysicalCondition.Dead) continue;
            double dt=BiologicalDelta(c,worldDt);
            InitializePersonality(c);
            SynchronizeHungerThreat(c);
            var i = c.Interaction;
            bool inWater=TickWaterExposure(c,dt);
            if(c.Condition==PhysicalCondition.Dead)continue;
            AdvanceEmotionalContinuity(c,dt);
            if(State.WorldSeconds-i.LastHitAt>1.5)i.HitCount=0;
            i.Resting=false;
            ResetInterruptedAutonomySearch(c);
            c.InnerAgeSeconds += dt; c.Body.AgeSeconds += dt;
            if(!c.Family.ColdStorage.Stored)AdvanceHumanBond(c,dt);
            if(i.HumanCompany.Until>0&&!CanSeekHumanCompany(c))EndHumanCompany(c);
            c.Fullness = Math.Max(0, c.Fullness - Config.HungerPerSecond * dt);
            i.Hydration = Math.Max(0,i.Hydration-dt*.025);
            i.AdvanceDressings(c,dt);
            c.Energy = Math.Max(0, c.Energy - Config.EnergyPerSecond * dt);
            c.TrainingFatigue = Math.Max(0, c.TrainingFatigue - dt * .12);
            i.AffectionSaturation = Math.Max(0, i.AffectionSaturation - dt * .035);
            i.HeadSensitivity = Math.Max(0, i.HeadSensitivity - dt * .015);
            i.ChangeCheekSensitivity(SurfaceSide.Whole,-dt*.015);
            i.PraiseRemaining = Math.Max(0, i.PraiseRemaining - dt);
            if (i.PraiseRemaining == 0) i.PendingPraise = null;
            if (c.Fullness <= 0) Hurt(c, dt * .12, "饥饿", null);
            if(!inWater&&!c.Family.ColdStorage.Stored&&c.Condition is PhysicalCondition.Active or PhysicalCondition.Recovering)UpdateInteractionRest(c,dt);
            AdvanceBodyInjuries(c,dt);
            if(c.Condition==PhysicalCondition.Dead)continue;
            if (c.Condition == PhysicalCondition.Critical)
            {
                c.CriticalRemaining = Math.Max(0, c.CriticalRemaining - dt);
                c.Activity = "重伤待救";
                if (c.CriticalRemaining == 0)
                {
                    if (State.Settings.LethalInjuries) Die(c, "错过救治时限");
                    else { c.Condition = PhysicalCondition.Recovering; c.Health = 1; c.RecoveryRemaining = 90; }
                }
                continue;
            }
            if(inWater)continue;
            // Rest takes priority over a completed lesson's persistent follow
            // order. Otherwise that order prevents UpdateInteractionRest forever.
            if(!i.Physiology.Active&&i.ActiveCue==null&&i.RestRemaining>0)i.FollowPlayer=false;
            TickDigestiveHealth(c,dt);
            if(c.Condition is PhysicalCondition.Dead or PhysicalCondition.Critical)continue;
            if(!c.Family.ColdStorage.Stored)ObserveNaturalResources(c);
            if (c.Condition == PhysicalCondition.Recovering)
            {
                c.RecoveryRemaining = Math.Max(0, c.RecoveryRemaining - dt * (c.Fullness > 25 ? 1 : .2));
                c.Energy = Clamp100(c.Energy + dt * .4);
                if (c.RecoveryRemaining <= 0) { c.Condition = PhysicalCondition.Active; c.Health = Math.Max(25, c.Health); }
            }
            if (c.Fullness > 40 && c.Energy > 40 && !i.Digestion.Symptomatic)
            { c.Health = Clamp100(c.Health + dt * .025); c.Pain = Math.Max(0, c.Pain - dt * .06); }
            if(c.Family.ColdStorage.Stored){c.Activity="冷藏保存中";continue;}
            if(FamilyRules.UnderIntervention(c)){c.Activity="强制干预进行中";continue;}
            if(!i.Supine||i.Carried||i.Falling||i.Restraint!=RestraintKind.None)i.WetRestRemaining=0;
            if (i.WetRestRemaining > 0)
            { i.WetRestRemaining = Math.Max(0, i.WetRestRemaining - dt); i.DrySurfaceOverTime(dt*1.2,true); }
            else i.DrySurfaceOverTime(dt*.035);
            if (i.Supine && i.Restraint == RestraintKind.None && !i.Carried)
            {
                i.SupineRemaining = Math.Max(0, i.SupineRemaining - dt);
                if (i.SupineRemaining <= 0) i.Supine = false;
            }
            if (i.Restraint != RestraintKind.None)
            {
                i.RestraintSeconds += dt;
                if (i.Restraint == RestraintKind.Wrap) c.Energy = Clamp100(c.Energy + dt * .15);
                else if (i.RestraintSeconds > 15) { c.Fear = Clamp100(c.Fear + dt * .035); i.ContainerFear = Clamp100(i.ContainerFear + dt * .02); }
            }
            // Excretion occupies the body, not the passage of ordinary time.
            // Keep illness, care and restraint effects above this autonomy gate.
            if(i.Physiology.Active)
            {
                c.Activity=i.Physiology.Excreting==ExcretionKind.Urine?"正在撒尿":"正在排便";
                continue;
            }
            if(!CanSelfMove(c)&&i.Hydration<12&&i.EatingRemaining<=0&&c.ReactionRemaining<=0&&State.WorldSeconds-i.LastThirstRequestAt>24)
            {
                i.LastThirstRequestAt=State.WorldSeconds;i.LastHelpAt=State.WorldSeconds;
                i.NextAutonomyAt=Math.Max(i.NextAutonomyAt,State.WorldSeconds+5);
                Feedback(c,"thirsty",Expression.Calling,CanSelfMove(c)?"它抿了抿嘴，向你要一口水。":"它走不到水边，望着你请求把杯子递近。","thirsty");
            }
            if (i.Restraint != RestraintKind.None)
            {
                c.Activity = i.Restraint switch { RestraintKind.Wrap => "包裹里休息", RestraintKind.Box => "在箱内寻找出口", RestraintKind.Hanging => "悬挂中", _ => "活动被限制" };
                if (i.EatingRemaining<=0 && State.WorldSeconds - i.LastHelpAt > 24 && i.Restraint != RestraintKind.Wrap)
                { i.LastHelpAt = State.WorldSeconds; Feedback(c, i.Restraint == RestraintKind.Box ? "box" : i.Restraint == RestraintKind.Hanging ? "hang" : "restrained", Expression.Calling, "它再次请求解除限制。", "help"); }
                TryEatNearby(c);
                continue;
            }
            if (i.Carried || i.Falling) continue;
            if(InteractionGestureTarget==c.Id&&InteractionGestureBlocksAutonomy)
            {
                if(CurrentInteractionAction==InteractionAction.Tease&&CanSelfMove(c)&&i.FoodSuspicion<75)
                {RoomMove(c,c.TargetX,c.TargetZ,dt);c.Activity="追着眼前的甜甜";}
                continue;
            }
            if((c.Fullness<12||i.Hydration<12)&&i.ActiveCue!=null)CompleteCue(c,false);
            if(TryRespondToWater(c,dt))continue;
            if(TryHungerAggression(c,dt))continue;
            if(TryMaternalCrisis(c,dt))continue;
            if((c.Family.Conflict.TargetId!=null||c.Family.FlightRemaining>0)&&i.EatingRemaining<=0&&TryFamilyActivity(c,dt))continue;
            if (i.ActiveCue is { } cue)
            {
                i.CueElapsed += dt;
                if (!i.CueUnderstood && i.CueElapsed > 1.5) { CompleteCue(c, false); continue; }
                if (cue == LearnedCue.Follow)
                {
                    if(!SeesHelpingHand(c))
                    {
                        c.Activity="看不到指路的手，停下来找一找";
                        if(i.CueElapsed>20)CompleteCue(c,false);
                        continue;
                    }
                    if(TryKeepHumanDistance(c,dt))
                    {
                        if(i.CueElapsed>20)CompleteCue(c,false);
                        continue;
                    }
                    double beforeX = c.X, beforeZ = c.Z;
                    double comfortable=PreferredHumanDistance(c);
                    double followDistance=comfortable>1.15?comfortable:1.1;
                    ApproachHumanAtDistance(c,dt,followDistance);
                    i.CueTravel += Distance(beforeX, beforeZ, c.X, c.Z);
                    c.Activity = "试着跟随手势";
                    if (i.CueTravel > 1 && Distance(c.X, c.Z, State.PlayerX, State.PlayerZ) < followDistance+.2) CompleteCue(c, true);
                    else if (i.CueElapsed > 20) CompleteCue(c, false);
                }
                else
                {
                    c.Activity = cue == LearnedCue.Wait ? "等着眼前的饭饭" : "按手势停下";
                    if (i.CueElapsed >= 3) CompleteCue(c, true);
                }
                continue;
            }
            i.WaitRemaining = Math.Max(0, i.WaitRemaining - dt);
            if (i.WaitRemaining > 0) continue;
            var careRegion=State.Regions.Find(r=>r.Id==c.RegionId);
            if(careRegion!=null&&TickWildSelfCare(c,careRegion,dt))continue;
            if(!i.HasAccessory&&State.WorldSeconds>=i.NextAutonomyAt&&i.EatingRemaining<=0)
            {
                i.NextAutonomyAt=State.WorldSeconds+14+Next()*12;
                var accessory=i.Accessory;
                Feedback(c,accessory.BorrowedForCare?"accessory-care-wait":"accessory-taken",
                    accessory.BorrowedForCare?Expression.Curious:Expression.Calling,
                    accessory.BorrowedForCare?"它一边留意周围，一边等你整理好饰品。":"它还在惦记自己的饰品。",
                    accessory.BorrowedForCare?"accessory-watch":"reach");
            }
            if (i.FootIntegrity <= 25 && UsesGroundFoot(c))
            {
                c.Activity = "底部受伤 · 需要辅助生活";
                bool takingFood=TryEatNearby(c);
                // A damaged foot prevents travel, not caring for someone already
                // within reach. The care path checks needs and actual contact.
                if(!takingFood&&i.EatingRemaining<=0&&TryFamilyCare(c,dt))continue;
                // A damaged foot prevents travel, not listening to somebody who
                // is already beside it. Needs and supported rest still go first.
                if(!takingFood&&i.EatingRemaining<=0&&!NaturalResources.WantsFood(c)&&!NaturalResources.WantsWater(c)&&
                    i.RestRemaining<=0&&!i.Resting&&c.Energy>=40&&c.Family.CompanionTargetId is { } nearbyId&&
                    State.Creatures.Find(o=>o.Id==nearbyId) is { } nearby&&FamilyInContact(c,nearby)&&TryFamilyActivity(c,dt))continue;
                if(!takingFood&&TryHumanCompany(c,dt))continue;
                if (!takingFood && State.WorldSeconds - i.LastHelpAt > 18)
                { i.LastHelpAt = State.WorldSeconds; Feedback(c, "cannot-walk", Expression.Calling, "它想起步，却无法走到远处。近旁食物或搬运能帮助它。", "failed-step"); }
                if(i.Resting&&!takingFood)c.Activity="护着底部慢慢休息";
                continue;
            }
            if(i.EatingRemaining>0){TickNaturalNeeds(c,dt);continue;}
            if((c.Fullness<15||i.Hydration<15)&&TickNaturalNeeds(c,dt))continue;
            if (i.RestRemaining > 0 || c.Energy < 22 || i.Resting)
            {
                ResetNaturalResourceSearch(c);
                i.HelpWaitingUntil=0;
                ApproachInteractionRest(c,dt);
                continue;
            }
            if(TickNaturalNeeds(c,dt))continue;
            if(TryFamilyCare(c,dt))continue;
            if(TryWaterAftercare(c,dt))continue;
            if(TryKeepHumanDistance(c,dt))continue;
            if (i.FollowPlayer && CanSelfMove(c)&&SeesHelpingHand(c))
            {
                ApproachHumanAtDistance(c,dt,Math.Max(1.25,PreferredHumanDistance(c)));
                c.Activity = "慢慢跟着你"; continue;
            }
            if(TryFamilyActivity(c,dt))continue;
            if(TryHumanCompany(c,dt))continue;
            if(!EmotionalReadiness.Settled(c))
            {
                c.TargetX=c.X;c.TargetZ=c.Z;
                c.Activity="留在原处观察，等自己慢慢安定下来";
                continue;
            }
            var ball = BallForAutonomousPlay(c);
            if (ball != null)
            {
                if (Distance(c.X, c.Z, ball.X, ball.Z) > BallBodyRadius(c)+InteractionFixtureGeometry.BallRadius+.06) RoomMove(c, ball.X, ball.Z, dt);
                else if (State.WorldSeconds - i.LastRequestAt > 3)
                {
                    double dx = State.PlayerX - ball.X, dz = State.PlayerZ - ball.Z;
                    double d = Math.Max(.01, Math.Sqrt(dx * dx + dz * dz));
                    double error=(Next()*2-1)*.45*(1-i.ToySkill/100),power=1.1+i.ToySkill*.009;
                    ball.VelocityX=(dx*Math.Cos(error)-dz*Math.Sin(error))/d*power;
                    ball.VelocityZ=(dx*Math.Sin(error)+dz*Math.Cos(error))/d*power;
                    i.ToySkill = Clamp100(i.ToySkill + .3); i.LastRequestAt = State.WorldSeconds;
                    i.SetReactionDirection(dx,dz);
                    Feedback(c, Math.Abs(error)>.20?"ball-askew":"ball", Expression.Delighted,
                        Math.Abs(error)>.20?"它用身体顶球，球偏向一旁；它抬头看看你。":"它用身体把球朝你顶回来，再抬眼看你的回应。", "ball-bump");
                }
                c.Activity = "和球球玩"; continue;
            }
            if(CanNoticeAutonomousWaterBall(c))
            {
                i.NextAutonomyAt=State.WorldSeconds+12;c.Activity="看着水上的球，留在岸边";
                Feedback(c,"ball-in-water",Expression.Curious,"球漂在水上，它留在岸边看，没有追下去。","idle",false);continue;
            }
            if (State.WorldSeconds >= i.NextAutonomyAt)
            {
                i.NextAutonomyAt = State.WorldSeconds + 14 + Next() * 12;
                if (!i.HasAccessory)
                {
                    var a = i.Accessory;
                    Feedback(c, a.BorrowedForCare ? "accessory-care-wait" : a.Place == AccessoryPlace.Hung ? "accessory-high" : "accessory-taken",
                        a.BorrowedForCare ? Expression.Curious : Expression.Calling,
                        a.BorrowedForCare ? "它看着借给你整理的饰品，等你戴回去。" : "它仍然惦记着自己的饰品。",
                        a.BorrowedForCare ? "accessory-watch" : "reach");
                    if (a.Place == AccessoryPlace.Ground && a.Integrity > 0)
                    { c.TargetX = a.X; c.TargetZ = a.Z; }
                }
                else if (CanRequestAutonomousNuzzle(c))
                {
                    c.TargetX = State.PlayerX; c.TargetZ = State.PlayerZ;
                    Feedback(c, "request-nuzzle", Expression.Content, "它记得一起蹭脸的经历，主动靠过来。", "invite");
                }
                else if(State.RoomObjects.Any(o=>o.OwnerId==c.Id&&o.Kind==RoomObjectKind.Treasure&&o.Stored))
                    Feedback(c,"treasure-missing",Expression.Worried,"它望向原来放小石头的位置，还惦记着被收走的宝物。","inspect");
                else if (State.RoomObjects.Any(o => o.OwnerId == c.Id && o.Kind == RoomObjectKind.Treasure&&!o.Stored&&o.RegionId==c.RegionId&&Distance(c.X,c.Z,o.X,o.Z)<1.8))
                    Feedback(c, "treasure", Expression.Delighted, "它把自己的小宝物展示给你看。", "proud");
                else if (EmotionalReadiness.Settled(c))
                {
                    string activity=Next()<.5&&MouthFunction.CanSpeakClearly(c)?"sing":"stretch";
                    Feedback(c,activity,Expression.Content,"没有操作时，它也在自己慢慢来。",activity);
                }
            }
            c.DecisionIn -= dt;
            if (c.DecisionIn <= 0)
            {
                c.DecisionIn = 5 + Next() * 5;
                if (c.ReactionRemaining <= 0) { c.TargetX = Math.Clamp(c.X + Next() * 3 - 1.5, -6, 6); c.TargetZ = Math.Clamp(c.Z + Next() * 2 - 1, -4, 4); }
            }
            if (CanSelfMove(c) && Distance(c.X, c.Z, c.TargetX, c.TargetZ) > .2)
            { RoomMove(c, c.TargetX, c.TargetZ, dt); c.Activity = "在小院里慢慢走"; }
            else c.Activity = "慢慢来";
        }
        PruneConsumedSupplies();
    }

    private bool CanRequestAutonomousNuzzle(Creature c) => SeesHelpingHand(c)&&
        c.Interaction.NuzzleFamiliarity>8&&c.Trust>40&&EmotionalReadiness.Settled(c)&&
        InteractionReadiness.WillAcceptAffection(c,BodyPart.Cheek,SurfaceSide.Whole);

    private RoomObject? BallForAutonomousPlay(Creature c)
    {
        var ball=State.RoomObjects.FirstOrDefault(o=>o.Kind==RoomObjectKind.Ball&&!o.Stored&&
            o.RegionId==c.RegionId&&o.Id!=InteractionGestureObject&&!RoomBallEnvironment.InWater(State,o));
        return ball!=null&&CanSelfMove(c)&&EmotionalReadiness.Settled(c)&&c.Interaction.ToyInterest>.4&&c.Fear<45&&c.Energy>30&&
            Distance(c.X,c.Z,ball.X,ball.Z)<3?ball:null;
    }

    private bool CanNoticeAutonomousWaterBall(Creature c)=>c.Interaction.ToyInterest>.4&&
        State.WorldSeconds>=c.Interaction.NextAutonomyAt&&State.RoomObjects.Any(o=>o.Kind==RoomObjectKind.Ball&&!o.Stored&&
            o.RegionId==c.RegionId&&RoomBallEnvironment.InWater(State,o)&&Distance(c.X,c.Z,o.X,o.Z)<3);

    private bool AutomaticCompanionShouldWait(Creature c)
    {
        // Reserve only idle automatic selection, on both sides. Existing family
        // activities and explicit invitations never pass through this gate.
        var i=c.Interaction;
        // Even an expired request must reach TryHumanCompany once to clear its
        // saved state before another oil installs a competing companion target.
        if(i.HumanCompany.Until>0)return true;
        if(!FamilyActionReady(c)||!CanSelfMove(c)||NaturalResources.WantsFood(c)||NaturalResources.WantsWater(c)||c.Energy<22)return false;
        // Presentation is deliberately transient: pause freezes this window;
        // loading clears it rather than replaying an old one-shot response.
        if(i.ReactionPoseRemaining>0&&i.ReactionSituation is "request-nuzzle" or "treasure-missing" or "treasure" or
            "accessory-care-wait" or "accessory-high" or "accessory-taken" or "sing" or "stretch")return true;
        if(State.WorldSeconds<i.NextAutonomyAt)return false;
        // These branches already precede the short-response slot. Do not reserve
        // a response that the current ball activity cannot actually reach.
        if(BallForAutonomousPlay(c)!=null||CanNoticeAutonomousWaterBall(c))return false;
        return !i.HasAccessory||CanRequestAutonomousNuzzle(c)||
            State.RoomObjects.Any(o=>o.OwnerId==c.Id&&o.Kind==RoomObjectKind.Treasure&&
                (o.Stored||o.RegionId==c.RegionId&&Distance(c.X,c.Z,o.X,o.Z)<1.8))||c.Fear<30&&c.Pain<20;
    }

    private void RoomMove(Creature c, double x, double z, double dt)
    {
        if (!CanSelfMove(c)) return;
        double speed = UsesGroundFoot(c) ? Math.Clamp(c.Interaction.FootIntegrity / 100, .3, 1) : 1;
        if(c.Interaction.Digestion.Symptomatic)speed*=Math.Max(.5,1-c.Interaction.Digestion.Severity*.005);
        double distance = Distance(c.X, c.Z, x, z);
        if (distance < .001) return;
        double step = Math.Min(distance, Config.MoveSpeed * speed * dt);
        double dx = (x - c.X) / distance, dz = (z - c.Z) / distance;
        double previousX=c.X,previousZ=c.Z;
        double edge=8-InteractionFootprint(c);
        GroundMove(c,Math.Clamp(c.X+dx*step,-edge,edge),Math.Clamp(c.Z+dz*step,-edge,edge),true);
        var life=c.Interaction;
        // A finished mouthful may acknowledge swallowing while planted. Once
        // actual autonomous travel begins, hand its pose back to the walking gait.
        // Do not clear a newer reaction, the completion speech, or intake results.
        if((c.X!=previousX||c.Z!=previousZ)&&life.EatingRemaining<=0&&
            life.ReactionSituation=="mouthful-done"&&life.ReactionPose=="swallow"&&life.ReactionPoseRemaining>0)
        {life.ReactionPose="idle";life.ReactionPoseRemaining=0;}
        if(Distance(previousX,previousZ,c.X,c.Z)>.005)c.Interaction.LastWalkAt=State.WorldSeconds;
        c.Interaction.Facing = Math.Atan2(dx, dz);
    }

    private bool TryEatNearby(Creature c)
    {
        var i = c.Interaction;
        if(i.EatingRemaining>0){c.Activity=i.EatingKind==IntakeKind.Water?"慢慢喝着这一口":"慢慢嚼着这一口";return true;}
        if (i.NaturalBitePause>0 || i.Carried || i.Falling || i.Height > .25 || i.Supine) return false;
        var food = State.RoomObjects.Where(o => o.RegionId==c.RegionId&&!o.Stored&&!o.HeldByHand && o.Portions > 0 &&
            (NaturalResources.IsWater(o.Kind)?NaturalResources.WantsWater(c):NaturalResources.IsFood(o.Kind)&&
                NaturalResources.WantsFood(c)&&!DigestiveFoodPause(c))&&
            Distance(c.X, c.Z, o.X, o.Z) <= NaturalResources.Reach(c) && CanReachRoomFood(c,o)&&NaturalResources.Consumer(State,o)==null)
            .OrderBy(o=>NaturalResources.IsWater(o.Kind)?i.Hydration:c.Fullness)
            .ThenBy(o=>Distance(c.X,c.Z,o.X,o.Z)).FirstOrDefault();
        if (food == null)
        {
            var low=State.RoomObjects.Find(o=>o.RegionId==c.RegionId&&!o.Stored&&!o.HeldByHand&&o.Portions>0&&
                (NaturalResources.IsWater(o.Kind)?NaturalResources.WantsWater(c):NaturalResources.IsFood(o.Kind)&&NaturalResources.WantsFood(c))&&
                Distance(c.X,c.Z,o.X,o.Z)<=NaturalResources.Reach(c)&&RoomFoodPathClear(c,o)&&!CanReachRoomFood(c,o));
            if(low!=null)c.Activity=NaturalResources.IsWater(low.Kind)?"看见近旁的水，但现在的姿势够不到":"看见近旁的食物，但现在的姿势够不到";
            return false;
        }
        BeginNaturalIntake(c,food);
        return true;
    }

    private bool CanReachRoomFood(Creature c,RoomObject food,double? atX=null,double? atZ=null)=>!c.Family.ColdStorage.Stored&&food.CarrierId==null&&RoomFoodPathClear(c,food,atX,atZ)&&
        NaturalIntakeGeometry.ClearsBodySupport(c,food)&&ActorNaturalPose.CanReachSource(c,food,State.RoomObjects,atX,atZ,
            c.Body.SpeciesId=="prototype-common"?State.Creatures.IndexOf(c):0);
    private bool RoomFoodPathClear(Creature c,RoomObject food,double? atX=null,double? atZ=null)
    {
        foreach(var boxed in State.Creatures.Where(o=>o.RegionId==c.RegionId&&o.Interaction.Restraint==RestraintKind.Box))
        {
            double dx=food.X-(boxed.Id==c.Id?atX??boxed.X:boxed.X),dz=food.Z-(boxed.Id==c.Id?atZ??boxed.Z:boxed.Z);
            double scale=boxed.IsYoung?.73:boxed.IsElder?1.03:1;
            // This box has a solid back and sides, with a low front feeding gap.
            if(boxed.Id==c.Id&&!(Math.Abs(dx)<.43*scale&&dz>-.40*scale&&dz<.86*scale))return false;
            if(boxed.Id!=c.Id&&Math.Abs(dx)<InteractionFixtureGeometry.BoxSideX*scale&&Math.Abs(dz)<InteractionFixtureGeometry.BoxDepth*.5*scale)return false;
        }
        return true;
    }

    public bool PutAwayRoomObject(string id)
    {
        var obj = State.RoomObjects.Find(o => o.Id == id);
        if (obj == null || obj.Stored || obj.HeldByHand || obj.Kind == RoomObjectKind.Cushion || NaturalResources.IsWild(obj.Kind)) return false;
        if(obj.CarrierId is { } carrierId&&State.Creatures.Find(c=>c.Id==carrierId) is { } carrier)
            CancelFamilyCare(carrier,reason:"递送中的食水被收起，这次照护已中断。");
        foreach(var oil in State.Creatures)
            if(oil.Interaction.GroundIntakeSourceId==id)DetachNaturalIntake(oil);
        if (obj.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet && !State.UnlimitedResources) State.Food += obj.Portions;
        foreach (var c in State.Creatures.Where(c => c.RegionId==obj.RegionId&&Distance(c.X, c.Z, obj.X, obj.Z) < 2 &&
            c.Condition != PhysicalCondition.Dead&&CanSeeResource(c,obj)))
            Feedback(c, "cold", Expression.Shy, "眼前的东西被收起，它停下来望着你。", "withdraw");
        if(obj.Kind==RoomObjectKind.Treasure)
        {
            obj.Stored=true;
            var owner=State.Creatures.Find(c=>c.Id==obj.OwnerId&&c.Condition!=PhysicalCondition.Dead);
            if(owner!=null&&owner.RegionId==obj.RegionId&&Distance(owner.X,owner.Z,obj.X,obj.Z)<2&&CanSeeResource(owner,obj))
            {
                Distress(owner,1);RecordInteraction(owner,"treasure-away","自己的小石头被收走了。",1);
                Feedback(owner,"treasure-missing",Expression.Worried,"它看着你收走小石头，还记得那是自己的宝物。","withdraw");
            }
        }
        else State.RoomObjects.Remove(obj);
        return true;
    }
}
