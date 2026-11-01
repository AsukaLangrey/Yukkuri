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
            if (!State.RoomObjects.Any(o => o.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet && !o.Stored && Distance(c.X, c.Z, o.X, o.Z) < 2))
            {
                if (!State.UnlimitedResources && State.Food <= 0)
                { i.ActiveCue = null; i.WaitRemaining = 0; return Unavailable("等待练习需要眼前的一份食物。 "); }
                if (!State.UnlimitedResources) State.Food--;
                State.RoomObjects.Add(new() { Kind = RoomObjectKind.Meal, X = Math.Clamp(c.X + .8, -7, 7), Z = c.Z });
            }
        }
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
        else Feedback(c, "learn-failed", Expression.Curious, "这次没有真正完成，可以调整示范或等它休息。", "confused");
        c.TrainingFatigue = Clamp100(c.TrainingFatigue + (i.CueDemonstration ? 8 : 3));
        i.ActiveCue = null; i.WaitRemaining = 0;
    }

    // Active effects (swallowing, speech and an already initiated fall) can finish in close-up.
    // This never advances hunger, age, healing, training, or other passive world time.
    public void AdvanceInteractionPresentation(double delta)
    {
        if (!State.InteractionPlaytest || State.Settings.Pause || !double.IsFinite(delta) || delta <= 0) return;
        double dt = Math.Min(delta, .1);
        foreach (var c in State.Creatures)
        {
            var i = c.Interaction;
            i.EatingRemaining = Math.Max(0, i.EatingRemaining - dt);
            i.SocialResponseRemaining=Math.Max(0,i.SocialResponseRemaining-dt);
            c.ReactionRemaining = Math.Max(0, c.ReactionRemaining - dt);
            if (c.ReactionRemaining <= 0)
            {
                c.Speech = "";
                c.Expression = c.Condition == PhysicalCondition.Dead ? Expression.Exhausted :
                    c.Pain > 25 ? Expression.Hurt : c.Fear > 60 ? Expression.Frightened :
                    i.RestRemaining > 0 ? Expression.Sleeping :
                    !i.HasAccessory ? (i.Accessory.BorrowedForCare ? Expression.Curious : Expression.Worried) :
                    i.Accessory.Integrity < 90 ? Expression.Worried :
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
                    double damage = State.Settings.Accidents ? Math.Max(0, speed - 2.8) * 2.2 : 0;
                    Distress(c, 2, damage, "跌落碰撞");
                    RecordInteraction(c, "fall", "离开手的支撑后落地，记住了这次经历。", Math.Max(1, damage));
                    Feedback(c, damage > 0 ? "hurt" : "down", damage > 0 ? Expression.Hurt : Expression.Shy,
                        damage > 0 ? "落地受伤，查看身体和后续动作。" : "落地了，仍然受了惊。", "land");
                }
            }
            if (Math.Abs(i.VelocityX) + Math.Abs(i.VelocityZ) > .005)
            {
                double nextX=Math.Clamp(c.X+i.VelocityX*dt,-7.5,7.5),nextZ=Math.Clamp(c.Z+i.VelocityZ*dt,-6,6);
                if(i.Height>1){c.X=nextX;c.Z=nextZ;}
                else if(!GroundMove(c,nextX,nextZ,false)){i.VelocityX*= -.18;i.VelocityZ*= -.18;}
                double drag = Math.Exp(-dt * (i.Falling ? .4 : 4));
                i.VelocityX *= drag; i.VelocityZ *= drag;
                c.TargetX = c.X; c.TargetZ = c.Z;
            }
        }
        foreach (var o in State.RoomObjects.Where(o => o.Kind == RoomObjectKind.Ball && !o.Stored))
        {
            o.X += o.VelocityX * dt; o.Z += o.VelocityZ * dt;
            if (Math.Abs(o.X) > 7) { o.X = Math.Clamp(o.X, -7, 7); o.VelocityX *= -.6; }
            if (Math.Abs(o.Z) > 5.8) { o.Z = Math.Clamp(o.Z, -5.8, 5.8); o.VelocityZ *= -.6; }
            o.VelocityX *= Math.Exp(-dt * .65); o.VelocityZ *= Math.Exp(-dt * .65);
        }
    }

    private void TickInteractionWorld(double dt)
    {
        foreach (var c in State.Creatures)
        {
            if (c.Condition == PhysicalCondition.Dead) continue;
            var i = c.Interaction;
            c.InnerAgeSeconds += dt; c.Body.AgeSeconds += dt;
            c.Fullness = Math.Max(0, c.Fullness - Config.HungerPerSecond * dt);
            c.Energy = Math.Max(0, c.Energy - Config.EnergyPerSecond * dt);
            c.TrainingFatigue = Math.Max(0, c.TrainingFatigue - dt * .12);
            i.AffectionSaturation = Math.Max(0, i.AffectionSaturation - dt * .035);
            i.HeadSensitivity = Math.Max(0, i.HeadSensitivity - dt * .015);
            i.CheekSensitivity = Math.Max(0, i.CheekSensitivity - dt * .015);
            i.PraiseRemaining = Math.Max(0, i.PraiseRemaining - dt);
            if (i.PraiseRemaining == 0) i.PendingPraise = null;
            if (c.Fullness <= 0) Hurt(c, dt * .12, "饥饿", null);
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
            if (i.FootTreated && i.FootIntegrity < 100 && c.Fullness > 25 && c.Energy > 20)
            {
                i.FootRecovery = Math.Max(0, i.FootRecovery - dt);
                if (i.FootRecovery <= 0)
                {
                    bool unableBefore = i.FootIntegrity <= 25;
                    i.FootIntegrity = Math.Min(100, i.FootIntegrity + dt * (i.RestRemaining > 0 ? .55 : .25));
                    if (unableBefore && i.FootIntegrity > 25)
                        Feedback(c, "recovery", Expression.Shy, "底部开始恢复支撑能力，终于能试着走动。", "careful-step");
                    if (i.FootIntegrity >= 100) i.FootTreated = false;
                }
            }
            if (c.Condition == PhysicalCondition.Recovering)
            {
                c.RecoveryRemaining = Math.Max(0, c.RecoveryRemaining - dt * (c.Fullness > 25 ? 1 : .2));
                c.Energy = Clamp100(c.Energy + dt * .4);
                if (c.RecoveryRemaining <= 0) { c.Condition = PhysicalCondition.Active; c.Health = Math.Max(25, c.Health); }
            }
            if (c.Fullness > 40 && c.Energy > 40)
            { c.Health = Clamp100(c.Health + dt * .025); c.Pain = Math.Max(0, c.Pain - dt * .06); }
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
                c.Activity = i.Restraint switch { RestraintKind.Wrap => "包裹里休息", RestraintKind.Box => "在箱内寻找出口", RestraintKind.Hanging => "悬挂中", _ => "活动被限制" };
                if (i.Restraint == RestraintKind.Wrap) c.Energy = Clamp100(c.Energy + dt * .15);
                else if (i.RestraintSeconds > 15) { c.Fear = Clamp100(c.Fear + dt * .035); i.ContainerFear = Clamp100(i.ContainerFear + dt * .02); }
                if (State.WorldSeconds - i.LastHelpAt > 24 && i.Restraint != RestraintKind.Wrap)
                { i.LastHelpAt = State.WorldSeconds; Feedback(c, i.Restraint == RestraintKind.Box ? "box" : i.Restraint == RestraintKind.Hanging ? "hang" : "restrained", Expression.Calling, "它再次请求解除限制。", "help"); }
                TryEatNearby(c);
                continue;
            }
            if (i.Carried || i.Falling) continue;
            if(InteractionGestureTarget==c.Id)
            {
                if(CurrentInteractionAction==InteractionAction.Tease&&CanSelfMove(c)&&i.FoodSuspicion<75)
                {RoomMove(c,c.TargetX,c.TargetZ,dt);c.Activity="追着眼前的甜甜";}
                continue;
            }
            if (i.ActiveCue is { } cue)
            {
                i.CueElapsed += dt;
                if (!i.CueUnderstood && i.CueElapsed > 1.5) { CompleteCue(c, false); continue; }
                if (cue == LearnedCue.Follow)
                {
                    double beforeX = c.X, beforeZ = c.Z;
                    if (Distance(c.X, c.Z, State.PlayerX, State.PlayerZ) > 1.1) RoomMove(c, State.PlayerX, State.PlayerZ, dt);
                    i.CueTravel += Distance(beforeX, beforeZ, c.X, c.Z);
                    c.Activity = "试着跟随手势";
                    if (i.CueTravel > 1 && Distance(c.X, c.Z, State.PlayerX, State.PlayerZ) < 1.3) CompleteCue(c, true);
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
            if (i.FootIntegrity <= 25 && UsesGroundFoot(c))
            {
                c.Activity = "底部受伤 · 需要辅助生活";
                TryEatNearby(c);
                if (State.WorldSeconds - i.LastHelpAt > 18)
                { i.LastHelpAt = State.WorldSeconds; Feedback(c, "cannot-walk", Expression.Calling, "它想起步，却无法走到远处。近旁食物或搬运能帮助它。", "failed-step"); }
                if (i.RestRemaining > 0) { i.RestRemaining = Math.Max(0, i.RestRemaining - dt); c.Energy = Clamp100(c.Energy + dt * .65); }
                continue;
            }
            if (i.RestRemaining > 0 || c.Energy < 22)
            {
                i.RestRemaining = Math.Max(0, i.RestRemaining - dt);
                var bed = State.RoomObjects.Where(o => o.Kind == RoomObjectKind.Cushion).MinBy(o => Distance(c.X, c.Z, o.X, o.Z));
                if (bed != null && Distance(c.X, c.Z, bed.X, bed.Z) > .65 && CanSelfMove(c))
                { RoomMove(c, bed.X, bed.Z, dt); c.Activity = "回到软垫旁"; }
                else { c.Energy = Clamp100(c.Energy + dt * .7); c.Activity = "慢慢休息"; c.Expression = Expression.Sleeping; }
                continue;
            }
            if (TryEatNearby(c)) continue;
            if (c.Fullness < 70)
            {
                var food = State.RoomObjects.Where(o => !o.Stored && o.Portions > 0 && o.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet)
                    .MinBy(o => Distance(c.X, c.Z, o.X, o.Z));
                if (food != null && CanSelfMove(c)) { RoomMove(c, food.X, food.Z, dt); c.Activity = "去找饭饭"; continue; }
            }
            if (i.FollowPlayer && CanSelfMove(c))
            {
                if (Distance(c.X, c.Z, State.PlayerX, State.PlayerZ) > 1.25) RoomMove(c, State.PlayerX, State.PlayerZ, dt);
                c.Activity = "慢慢跟着你"; continue;
            }
            if (c.Fear > 60 && c.Trust < 35 && Distance(c.X, c.Z, State.PlayerX, State.PlayerZ) < 2.2)
            {
                RoomMove(c, c.X + (c.X - State.PlayerX), c.Z + (c.Z - State.PlayerZ), dt);
                c.Activity = "留出一点距离"; continue;
            }
            var ball = State.RoomObjects.FirstOrDefault(o => o.Kind == RoomObjectKind.Ball && !o.Stored);
            if (ball != null && i.ToyInterest > .4 && c.Fear < 45 && c.Energy > 30 && Distance(c.X, c.Z, ball.X, ball.Z) < 3)
            {
                if (Distance(c.X, c.Z, ball.X, ball.Z) > .8) RoomMove(c, ball.X, ball.Z, dt);
                else if (State.WorldSeconds - i.LastRequestAt > 3)
                {
                    double dx = State.PlayerX - ball.X, dz = State.PlayerZ - ball.Z;
                    double d = Math.Max(.01, Math.Sqrt(dx * dx + dz * dz));
                    ball.VelocityX = dx / d * 1.5; ball.VelocityZ = dz / d * 1.5;
                    i.ToySkill = Clamp100(i.ToySkill + .3); i.LastRequestAt = State.WorldSeconds;
                    Feedback(c, "ball", Expression.Delighted, "它用身体把球顶回来，朝你看了一眼。", "ball-bump");
                }
                c.Activity = "和球球玩"; continue;
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
                else if (i.NuzzleFamiliarity > 8 && c.Trust > 40 && c.Fear < 35)
                {
                    c.TargetX = State.PlayerX; c.TargetZ = State.PlayerZ;
                    Feedback(c, "request-nuzzle", Expression.Content, "它记得一起蹭脸的经历，主动靠过来。", "invite");
                }
                else if (State.RoomObjects.Any(o => o.OwnerId == c.Id && o.Kind == RoomObjectKind.Treasure))
                    Feedback(c, "treasure", Expression.Delighted, "它把自己的小宝物展示给你看。", "proud");
                else if (c.Fear < 30 && c.Pain < 20)
                    Feedback(c, Next() < .5 ? "sing" : "stretch", Expression.Content, "没有操作时，它也在自己慢慢来。", "stretch");
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
        State.RoomObjects.RemoveAll(o => o.Portions <= 0);
    }

    private void RoomMove(Creature c, double x, double z, double dt)
    {
        if (!CanSelfMove(c)) return;
        double speed = UsesGroundFoot(c) ? Math.Clamp(c.Interaction.FootIntegrity / 100, .3, 1) : 1;
        double distance = Distance(c.X, c.Z, x, z);
        if (distance < .001) return;
        double step = Math.Min(distance, Config.MoveSpeed * speed * dt);
        double dx = (x - c.X) / distance, dz = (z - c.Z) / distance;
        double previousX=c.X,previousZ=c.Z;
        GroundMove(c,Math.Clamp(c.X+dx*step,-7,7),Math.Clamp(c.Z+dz*step,-5.5,5.5),true);
        if(Distance(previousX,previousZ,c.X,c.Z)>.005)c.Interaction.LastWalkAt=State.WorldSeconds;
        c.Interaction.Facing = Math.Atan2(dx, dz);
    }

    private bool TryEatNearby(Creature c)
    {
        var i = c.Interaction;
        if (c.Fullness >= 85 || i.EatingRemaining > 0 || i.Carried || i.Falling || i.Height > .25 || i.Supine) return false;
        var food = State.RoomObjects.FirstOrDefault(o => !o.Stored && o.Portions > 0 &&
            o.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet && Distance(c.X, c.Z, o.X, o.Z) <= .85 && CanReachRoomFood(c,o));
        if (food == null) return false;
        food.Portions--; c.Fullness = Clamp100(c.Fullness + (food.Kind == RoomObjectKind.Sweet ? 9 : 19));
        i.EatingRemaining = 2; c.Activity = "慢慢吃饭";
        Feedback(c, food.Kind == RoomObjectKind.Sweet ? "sweet" : "eat", Expression.Content,
            "食物就在嘴边，它自己吃到了这一口。", "eat");
        return true;
    }

    private bool CanReachRoomFood(Creature c,RoomObject food)
    {
        foreach(var boxed in State.Creatures.Where(o=>o.Interaction.Restraint==RestraintKind.Box))
        {
            double dx=food.X-boxed.X,dz=food.Z-boxed.Z;
            // This box has a solid back and sides, with a low front feeding gap.
            if(boxed.Id==c.Id&&!(Math.Abs(dx)<.43&&dz>-.40&&dz<.86))return false;
            double scale=boxed.IsYoung?.73:1;
            if(boxed.Id!=c.Id&&Math.Abs(dx)<.85*scale&&Math.Abs(dz)<.73*scale)return false;
        }
        return true;
    }

    public bool PutAwayRoomObject(string id)
    {
        var obj = State.RoomObjects.Find(o => o.Id == id);
        if (obj == null || obj.Kind == RoomObjectKind.Cushion) return false;
        if (obj.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet && !State.UnlimitedResources) State.Food += obj.Portions;
        foreach (var c in State.Creatures.Where(c => Distance(c.X, c.Z, obj.X, obj.Z) < 2 && c.Condition != PhysicalCondition.Dead))
            Feedback(c, "cold", Expression.Shy, "眼前的东西被收起，它停下来望着你。", "withdraw");
        State.RoomObjects.Remove(obj); return true;
    }
}
