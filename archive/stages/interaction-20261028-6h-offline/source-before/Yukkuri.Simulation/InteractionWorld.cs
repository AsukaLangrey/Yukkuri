namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    public static WorldSimulation CreateInteractionPlaytest(ulong seed = 20261028)
    {
        var s = new WorldState
        {
            MilestoneScenario = true, InteractionPlaytest = true, RandomState = seed == 0 ? 20261028 : seed,
            UnlimitedResources = true, Food = 50, Medicine = 12, Money = 0,
            Settings = new() { Invasions = false, Accidents = true, LethalInjuries = true },
            Regions = [new() { Id = "home", Name = "慢慢来 · 相处小院", Kind = RegionKind.Sanctuary }]
        };
        var profiles = new[]
        {
            ("小铃", "reimu", "shy", -2.7, 0.0, 38.0, .25, .72),
            ("松果", "marisa", "proud", .0, -1.2, 48.0, .8, .42),
            ("豆豆", "reimu", "curious", 2.7, .4, 60.0, .5, .62)
        };
        for (int i = 0; i < profiles.Length; i++)
        {
            var (name, species, disposition, x, z, trust, courage, patience) = profiles[i];
            var c = new Creature
            {
                Id = $"interaction-oil-{i}", Name = name, TrueDisposition = disposition,
                X = x, Z = z, TargetX = x, TargetZ = z, Trust = trust, Courage = courage,
                Patience = patience, Aggression = disposition == "proud" ? .42 : .08,
                Fullness = 55 + i * 8, Energy = 82, Attachment = 18, IsYoung = i == 2,
                Body = new() { Id = $"interaction-body-{i}", SpeciesId = species,
                    ReproductionRole = i == 1 ? ReproductionRole.Paternal : ReproductionRole.Maternal },
                Registration = new() { IdentityId = $"interaction-identity-{i}" },
                Interaction = new() { Enabled = true, TouchPreference = .35 + i * .22,
                    NextAutonomyAt = 6 + i * 5, ToyInterest = .5 + i * .2,
                    Accessory = new() { Id = $"interaction-accessory-{i}", X = x, Z = z } }
            };
            s.Creatures.Add(c);
        }
        s.SelectedId = s.Creatures[0].Id;
        s.RoomObjects.Add(new() { Kind = RoomObjectKind.Cushion, X = -3.8, Z = -3.4 });
        s.RoomObjects.Add(new() { Kind = RoomObjectKind.Cushion, X = 3.8, Z = -3.4 });
        var sim = new WorldSimulation(s);
        sim.Log("小院里有三只性情不同的油。先打个招呼，再慢慢熟悉。饭饭、工具与护理用品已备好。 ");
        return sim;
    }

    public static bool UsesGroundFoot(Creature c) =>
        c.Body.SpeciesId is not ("remilia" or "flandre") &&
        c.Body.UpgradeRoute is not ("flying" or "body" or "bodied");

    public static bool CanSelfMove(Creature c) => c.Condition == PhysicalCondition.Active &&
        c.Interaction.Restraint == RestraintKind.None && !c.Interaction.Carried &&
        !c.Interaction.Falling && !c.Interaction.Supine &&
        (!UsesGroundFoot(c) || c.Interaction.FootIntegrity > 25);

    private InteractionFeedback Feedback(Creature c, string situation, Expression expression,
        string description, string pose = "idle", bool changed = true, bool refused = false)
    {
        if(c.Condition==PhysicalCondition.Dead)
        {
            c.Speech="";c.Expression=Expression.Exhausted;c.LastReaction="再也没有回应。";
            State.LastActionMessage="它已经无法回应，已发生的后果不会被取消动作抹掉。";
            return new(true,State.LastActionMessage,"","dead",Changed:changed);
        }
        React(c, expression, OilDialogue.Say(c, situation), description, 4.5);
        State.LastActionMessage = description;
        return new(!refused, description, c.Speech, pose, refused, changed);
    }

    private void RecordInteraction(Creature c, string key, string description, double weight = 1)
    {
        var i = c.Interaction;
        i.Memories[key] = Math.Clamp(i.Memories.GetValueOrDefault(key) + weight, 0, 100);
        i.LastMeaningfulEvent = description;
        c.Memories.Add(description);
        if (c.Memories.Count > 30) c.Memories.RemoveAt(0);
        c.PersonalMemories.Add(new() { IncidentId = $"interaction-{++State.InteractionSequence}",
            At = State.WorldSeconds, Text = description, EmotionalWeight = weight });
        if (c.PersonalMemories.Count > 48) c.PersonalMemories.RemoveAt(0);
        Log($"{c.Name}：{description}");
    }

    private void ImproveAffection(Creature c, double amount)
    {
        amount /= 1 + c.Interaction.AffectionSaturation * .08;
        c.Trust = Clamp100(c.Trust + amount);
        c.Attachment = Clamp100(c.Attachment + amount * .6);
        c.Fear = Math.Max(0, c.Fear - amount * .35);
        c.Interaction.AffectionSaturation = Clamp100(c.Interaction.AffectionSaturation + amount * .7);
    }

    private void Distress(Creature c, double amount, double damage = 0, string cause = "粗暴接触")
    {
        c.Fear = Clamp100(c.Fear + amount * (1.2 - c.Courage * .5));
        c.Resentment = Clamp100(c.Resentment + amount * .55);
        c.Trust = Clamp100(c.Trust - amount * .45);
        if (damage > 0) Hurt(c, damage, cause, "player");
    }

    private InteractionFeedback Unavailable(string message) => new(false, message, "", "idle");
    private Creature? InteractionTarget(string id) => State.InteractionPlaytest
        ? State.Creatures.Find(c => c.Id == id && c.RegionId == State.ActiveRegionId) : null;

    private string? ValidateInteraction(Creature? c, InteractionInput input)
    {
        if (c == null) return "先选择当前小院里的一只油。";
        if (!Enum.IsDefined(input.Action) || !Enum.IsDefined(input.Part) ||
            !new[] { input.Force, input.Duration, input.Motion, input.X, input.Z, input.Height,
                input.DirectionX, input.DirectionZ }.All(double.IsFinite) ||
            input.Force is < 0 or > 1 || input.Duration is < 0 or > 1 || input.Motion < 0 ||
            Math.Abs(input.X) > 8 || Math.Abs(input.Z) > 8 || input.Height is < 0 or > 3)
            return "这个动作的位置或输入无效。";
        if (State.Settings.Pause) return "先恢复时间，再开始互动。";
        if (State.Mode == PlayMode.CloseUp && State.SelectedId != c.Id) return "先退出特写，再选择另一只油。";
        if (c.Condition == PhysicalCondition.Dead) return "它已经无法回应。可以读取此前存档。";
        if (c.Condition == PhysicalCondition.Critical && input.Action is not
            (InteractionAction.Care or InteractionAction.Inspect or InteractionAction.Lift or
             InteractionAction.PutDown or InteractionAction.Lower or InteractionAction.Release or
             InteractionAction.OpenBox or InteractionAction.Unwrap)) return "它需要先稳定伤势。";
        return null;
    }

    public InteractionFeedback ExecuteInteraction(string id, InteractionInput input)
    {
        var c = InteractionTarget(id);
        var invalid = ValidateInteraction(c, input);
        if (invalid != null) return Unavailable(invalid);
        BodyPart? requiredPart=input.Action switch
        {
            InteractionAction.Flick=>BodyPart.Head,
            InteractionAction.Slap or InteractionAction.Poke=>BodyPart.Cheek,
            InteractionAction.TakeAccessory or InteractionAction.TakeAccessoryCare=>BodyPart.Accessory,
            _=>null
        };
        if(requiredPart.HasValue&&input.Part!=requiredPart.Value)
            return Unavailable(requiredPart.Value switch
            {
                BodyPart.Head=>"还没有碰到额头，把手移到头顶前方。",
                BodyPart.Cheek=>"还没有碰到脸侧。",
                _=>"要接触它戴着的饰品，才可以取下。"
            });
        var oil = c!;
        var life = oil.Interaction;
        var accessory = life.Accessory;
        bool conversational=input.Action is InteractionAction.Greet or InteractionAction.Praise or InteractionAction.Comfort or InteractionAction.Sing or InteractionAction.Stretch;
        if(conversational&&life.SocialResponseRemaining>0)return Unavailable("它还在回应刚才的话，给它一点时间。 ");
        if(conversational)life.SocialResponseRemaining=2.4;
        oil.LastPlayerTouchAt = State.WorldSeconds;
        life.LastContact = input.Action.ToString();
        switch (input.Action)
        {
            case InteractionAction.Greet:
                ImproveAffection(oil, .25);
                return Feedback(oil, oil.Fear > 45 || oil.Trust < 25 ? "greet-shy" : "greet",
                    Expression.Curious, "抬起脸，听见你在叫它。", "greet");
            case InteractionAction.Praise:
                bool earned = life.PendingPraise != null && life.PraiseRemaining > 0;
                if (earned)
                {
                    RecordInteraction(oil, "praise", $"做好{life.PendingPraise}后得到了夸奖。", 2);
                    life.PendingPraise = null; life.PraiseRemaining = 0;
                    ImproveAffection(oil, 1.5);
                }
                else ImproveAffection(oil, .15);
                return Feedback(oil, earned ? "praise" : "praise-empty", Expression.Delighted,
                    earned ? "它把这次夸奖和刚做好的事情联系起来。" : "它听见温和的话，朝你望过来。", "proud");
            case InteractionAction.Comfort:
                if (oil.Fear > 75 && oil.Trust < 15)
                    return Feedback(oil, "touch-no", Expression.Frightened, "它还不敢靠近，先留一点距离。", "withdraw", false, true);
                ImproveAffection(oil, .5); oil.Fear = Math.Max(0, oil.Fear - 2);
                return Feedback(oil, "comfort", Expression.Shy, "安慰让它稍微放松，先陪在旁边。", "comfort");
            case InteractionAction.PutDown:
                if (!life.Carried) return Unavailable("先把它托起来，再选择落点。 ");
                if(!CanOccupyRoom(oil,input.X,input.Z))return Unavailable("这里被身体或装置占着，仍在托着它；换一个空落点。 ");
                life.Carried = false; life.Height = 0; life.Falling = false;
                MoveCreature(oil.Id, input.X, input.Z);
                RecordInteraction(oil, "safe-carry", "被稳稳托着，放到了地面。", 1);
                return Feedback(oil, "down", Expression.Content, "已经放稳，可以自己活动。", "settle");
            case InteractionAction.Wrap:
                if (life.Restraint != RestraintKind.None || life.Carried || life.Falling) return Unavailable("先放稳并解除其他装置。 ");
                if (oil.Fear > 65 && life.ContainerFear > 30)
                    return Feedback(oil, "touch-no", Expression.Frightened, "它想起被困的经历，拒绝包裹。", "withdraw", false, true);
                life.Restraint = RestraintKind.Wrap; life.RestraintSeconds = 0;
                RecordInteraction(oil, "wrapped", "被放进柔软包裹。", .5);
                return Feedback(oil, "wrap", Expression.Content, "包裹托住了身体，可以轻轻摇。", "wrapped");
            case InteractionAction.Unwrap:
                if (life.Restraint != RestraintKind.Wrap) return Unavailable("它现在没有包裹。 ");
                life.Restraint = RestraintKind.None; life.Height = 0;
                return Feedback(oil, "unwrap", Expression.Content, "解开包裹，恢复原本可用的动作。", "stretch");
            case InteractionAction.PlaceMeal:
            case InteractionAction.PlaceSweet:
                return PlaceFood(oil, input);
            case InteractionAction.TurnDry:
                if (life.Restraint != RestraintKind.None || life.Carried) return Unavailable("先解除限制、放稳，再帮助翻身。 ");
                if (oil.Pain > 50) return Feedback(oil, "touch-no", Expression.Hurt, "现在翻动会让它疼，先护理伤处。", "guard", false, true);
                life.Supine = true; life.SupineRemaining = 12; life.WetRestRemaining = 12;
                return Feedback(oil, "sun-bottom", Expression.Content, "把底部朝上，能继续擦干；普通时间里会慢慢晾干。", "sun-bottom");
            case InteractionAction.TakeAccessoryCare:
            case InteractionAction.TakeAccessory:
                if (accessory.Place != AccessoryPlace.Worn) return Unavailable("饰品已经取下了。 ");
                bool care = input.Action == InteractionAction.TakeAccessoryCare;
                if (care && (oil.Trust < 25 || oil.Fear > 65 || accessory.WasStolen && oil.Trust < 45))
                    return Feedback(oil, "accessory-wary", Expression.Defiant, "它护住饰品，不愿交给你。", "guard", false, true);
                accessory.Place = AccessoryPlace.Hand; accessory.X = oil.X + 1; accessory.Z = oil.Z; accessory.Height = .7;
                accessory.BorrowedForCare = care;
                if (!care)
                {
                    accessory.WasStolen = true; accessory.Losses++; Distress(oil, 5);
                    RecordInteraction(oil, "accessory-loss", "自己的饰品被突然拿走。", 5);
                }
                return Feedback(oil, care ? "accessory-care" : "accessory-taken", care ? Expression.Curious : Expression.Angry,
                    care ? "它看着你整理饰品，等待归还。" : "饰品被拿走，它追着饰品看。", "accessory-watch");
            case InteractionAction.ReturnAccessory:
                if (accessory.Place == AccessoryPlace.Worn) return Unavailable("饰品已经戴着。 ");
                if (accessory.Integrity <= 0) return Unavailable("这件已经无法戴好，需要更换；原来的经历仍会留下。 ");
                accessory.Place = AccessoryPlace.Worn; accessory.Height = 0;
                accessory.BorrowedForCare = false;
                if (!accessory.WasStolen) ImproveAffection(oil, .6);
                RecordInteraction(oil, "accessory-return", "自己的饰品回到了头上。", .5);
                return Feedback(oil, accessory.WasStolen ? "accessory-wary" : "accessory-return", Expression.Shy,
                    accessory.WasStolen ? "它检查着归还的饰品，仍记得被拿走的事。" : "重新戴好，轻轻调整到熟悉的位置。", "proud");
            case InteractionAction.ReplaceAccessory:
                if (accessory.Integrity > 0) return Unavailable("原饰品仍可使用或修补，先保留它珍视的东西。 ");
                accessory.Id = Guid.NewGuid().ToString("N"); accessory.Integrity = 100; accessory.Dirt = 0;
                accessory.Place = AccessoryPlace.Worn; accessory.Replacements++; accessory.RepairProgress = 0;
                accessory.BorrowedForCare = false;
                RecordInteraction(oil, "replacement", "收到了新饰品，也还记得旧饰品。", 2);
                return Feedback(oil, "new-accessory", Expression.Shy, "戴上替代饰品，旧物的经历没有被清空。", "accessory-watch");
            case InteractionAction.StoreAccessory:
            case InteractionAction.HangAccessory:
                if (accessory.Place == AccessoryPlace.Worn) return Unavailable("先把饰品取下来。 ");
                accessory.Place = input.Action == InteractionAction.StoreAccessory ? AccessoryPlace.Stored : AccessoryPlace.Hung;
                accessory.X = Math.Clamp(oil.X + 1.3, -7, 7); accessory.Z = oil.Z - .5; accessory.Height = 1.8;
                accessory.WasStolen = true; Distress(oil, 2);
                accessory.BorrowedForCare = false;
                return Feedback(oil, accessory.Place == AccessoryPlace.Hung ? "accessory-high" : "accessory-taken",
                    Expression.Calling, accessory.Place == AccessoryPlace.Hung ? "饰品挂到了够不到的位置，它仍试着索回。" : "饰品被收起，它开始寻找。", "reach");
            case InteractionAction.DamageAccessory:
                if (accessory.Place == AccessoryPlace.Worn) return Unavailable("先取下饰品，再明确选择损坏。 ");
                if (accessory.Integrity <= 0) return Unavailable("这件饰品已经损坏，无法重复结算。 ");
                accessory.Integrity = Math.Max(0, accessory.Integrity - (20 + input.Force * 50));
                accessory.BorrowedForCare = false; accessory.WasStolen = true;
                Distress(oil, 8); RecordInteraction(oil, "accessory-damage", "珍视的饰品被损坏了。", 8);
                return Feedback(oil, "accessory-broken", Expression.Hurt,
                    accessory.Integrity > 0 ? "饰品破损，仍有可以修补的部分。" : "饰品损坏到无法佩戴，它还盯着残片。", "cry");
            case InteractionAction.GiveBall:
                if (State.RoomObjects.Any(o => o.Kind == RoomObjectKind.Ball && !o.Stored)) return Unavailable("球已经在小院里，选“推球”与它玩。 ");
                State.RoomObjects.Add(new() { Kind = RoomObjectKind.Ball, X = input.X, Z = input.Z });
                return Feedback(oil, "ball", Expression.Curious, "软球放好了。推过去，看看它会不会接。", "greet");
            case InteractionAction.GiveTreasure:
                if (State.RoomObjects.Any(o => o.Kind == RoomObjectKind.Treasure && o.OwnerId == oil.Id)) return Unavailable("它已经有自己的宝物，会在普通时间拿出来看看。 ");
                State.RoomObjects.Add(new() { Kind = RoomObjectKind.Treasure, X = oil.X + .7, Z = oil.Z, OwnerId = oil.Id });
                ImproveAffection(oil, 1); RecordInteraction(oil, "treasure", "收到一颗属于自己的小石头。", 2);
                return Feedback(oil, "treasure", Expression.Delighted, "它仔细端详自己的小宝物。", "proud");
            case InteractionAction.Sing:
            case InteractionAction.Stretch:
                if (oil.Energy < 18 || oil.Fear > 65) return Feedback(oil, "too-tired", Expression.Exhausted, "现在不想参与，先让它休息。", "withdraw", false, true);
                ImproveAffection(oil, .4);
                return Feedback(oil, input.Action == InteractionAction.Sing ? "sing" : "stretch", Expression.Content,
                    input.Action == InteractionAction.Sing ? "它跟着你唱起短短的小调。" : "身体慢慢伸展，再缩回舒服的姿势。",
                    input.Action == InteractionAction.Sing ? "sing" : "stretch");
            case InteractionAction.TeachFollow: case InteractionAction.TeachStop: case InteractionAction.TeachWait:
            case InteractionAction.Follow: case InteractionAction.Stop: case InteractionAction.Wait:
                return ApplyCue(oil, input.Action);
            case InteractionAction.Inspect:
                life.LastObservedCondition = DescribeInteractionCondition(oil);
                return Feedback(oil, "inspect", oil.Pain > 20 ? Expression.Hurt : Expression.Curious,
                    life.LastObservedCondition, "inspect", false);
            case InteractionAction.Rest:
                life.RestRemaining = 30; life.FollowPlayer = false;
                var cushion = State.RoomObjects.Where(o => o.Kind == RoomObjectKind.Cushion).MinBy(o => Distance(oil.X, oil.Z, o.X, o.Z));
                if (cushion != null && CanSelfMove(oil)) { oil.TargetX = cushion.X; oil.TargetZ = cushion.Z; }
                RecordInteraction(oil, "rest", "被安排在安静处休息。", .5);
                return Feedback(oil, "rest", Expression.Sleeping, "它需要在普通时间里真正休息；行动不便时请托过去。", "rest");
            case InteractionAction.Poke:
                life.CheekSensitivity = Clamp100(life.CheekSensitivity + 2); Distress(oil, 1);
                return Feedback(oil, "poke", Expression.Defiant, "脸颊缩了一下，它鼓起脸抗议。", "poke");
            case InteractionAction.Scold:
                bool reason = life.Memories.GetValueOrDefault("teasing") == 0 && oil.CurrentOffenseId != null;
                life.FollowPlayer = false; life.WaitRemaining = 1;
                Distress(oil, reason ? .4 : 1.5);
                return Feedback(oil, reason ? "scold" : "confused-scold", Expression.Defiant,
                    reason ? "它停住动作，但还需要理解发生了什么。" : "它没有明白缘由，暂时停住看着你。", "startle");
            case InteractionAction.Cold:
            case InteractionAction.Belittle:
                Distress(oil, input.Action == InteractionAction.Cold ? 1.5 : 4);
                RecordInteraction(oil, "cold", "请求没有得到温和回应。", 1);
                return Feedback(oil, oil.Courage > .6 && input.Action == InteractionAction.Belittle ? "angry" : "cold",
                    oil.Courage > .6 ? Expression.Defiant : Expression.Shy, "它改变了看向你的神情。", "withdraw");
            case InteractionAction.Flick:
            case InteractionAction.Slap:
                double severity = input.Action == InteractionAction.Flick ? 1.2 + input.Force * 3 : 3 + input.Force * 7;
                Distress(oil, severity, Math.Max(0, severity - 1), input.Action == InteractionAction.Flick ? "弹额" : "掌掴");
                if(input.Action==InteractionAction.Slap)life.CheekSensitivity=Clamp100(life.CheekSensitivity+severity);
                else life.HeadSensitivity = Clamp100(life.HeadSensitivity + severity);
                Impulse(oil, input.DirectionX, input.DirectionZ, severity * .16);
                RecordInteraction(oil, "hand-hurt", "手的突然接触让自己疼痛。", severity);
                return Feedback(oil, "hurt", Expression.Hurt, "接触产生了位移，它护住刚被碰到的部位。", input.Action == InteractionAction.Flick ? "flick" : "slap");
            case InteractionAction.Drop:
                if (!life.Carried) return Unavailable("先托起来，才可以松手。 ");
                life.Carried = false; life.Falling = true; life.VerticalVelocity = 0;
                return Feedback(oil, "fall", Expression.Frightened, "失去支撑，开始下落。", "fall");
            case InteractionAction.Flip:
                if (life.Restraint != RestraintKind.None || life.Carried) return Unavailable("先放稳并解除装置。 ");
                life.Supine = !life.Supine; life.SupineRemaining = 4;
                return Feedback(oil, "flipped", Expression.Curious, "它试着找回正常的朝向。", "flip");
            case InteractionAction.Pin:
            case InteractionAction.Box:
            case InteractionAction.Hang:
                if (life.Restraint != RestraintKind.None || life.Carried || life.Falling) return Unavailable("先放稳，解除当前装置再使用另一个。 ");
                var fixture=input.Action switch { InteractionAction.Pin=>RestraintKind.Frame,InteractionAction.Box=>RestraintKind.Box,_=>RestraintKind.Hanging };
                if(!CanOccupyRoom(oil,oil.X,oil.Z,fixture))return Unavailable("旁边没有足够空间摆好装置，先搬到空处。 ");
                life.Restraint = input.Action switch { InteractionAction.Pin => RestraintKind.Frame, InteractionAction.Box => RestraintKind.Box, _ => RestraintKind.Hanging };
                life.RestraintSeconds = 0; life.Supine = life.Restraint == RestraintKind.Frame;
                life.Height = life.Restraint == RestraintKind.Hanging ? 1.3*(oil.IsYoung?.73:1) : 0;
                life.ContainerFear = Clamp100(life.ContainerFear + 3); Distress(oil, 3);
                RecordInteraction(oil, "restraint", $"被限制在{(life.Restraint == RestraintKind.Box ? "箱子里" : life.Restraint == RestraintKind.Frame ? "固定具上" : "悬挂装置上")}。", 3);
                string key = life.Restraint == RestraintKind.Box ? oil.Courage > .6 ? "box-brave" : "box" : life.Restraint == RestraintKind.Hanging ? "hang" : "restrained";
                return Feedback(oil, key, Expression.Calling, "限制已经生效。取消工具不会解除装置。", life.Restraint.ToString().ToLowerInvariant());
            case InteractionAction.Release:
            case InteractionAction.OpenBox:
            case InteractionAction.Lower:
                RestraintKind expected = input.Action switch { InteractionAction.OpenBox => RestraintKind.Box, InteractionAction.Lower => RestraintKind.Hanging, _ => RestraintKind.Frame };
                if (life.Restraint != expected) return Unavailable("这个解除动作与当前装置不对应。 ");
                life.Restraint = RestraintKind.None; life.Height = 0; life.Supine = false; life.SupineRemaining = 0;
                RecordInteraction(oil, "release", "装置被解除，重新得到活动空间。", .5);
                return Feedback(oil, expected == RestraintKind.Box ? "box-open" : "released", Expression.Shy,
                    CanSelfMove(oil) ? "限制已解除，它可以决定往哪里去。" : "限制已解除，但已有身体伤势仍影响活动。", "settle");
            default:
                return Unavailable("这个动作需要按住并直接操作对象。 ");
        }
    }

    private InteractionFeedback PlaceFood(Creature c, InteractionInput input)
    {
        if (!State.UnlimitedResources && State.Food <= 0) return Unavailable("没有食物了。 ");
        if (State.RoomObjects.Count(o => o.Kind is RoomObjectKind.Meal or RoomObjectKind.Sweet && !o.Stored) >= 24)
            return Unavailable("先收起或吃完地上的食物。 ");
        if (!State.UnlimitedResources) State.Food--;
        State.RoomObjects.Add(new() { Kind = input.Action == InteractionAction.PlaceSweet ? RoomObjectKind.Sweet : RoomObjectKind.Meal,
            X = input.X, Z = input.Z });
        c.DecisionIn = 0;
        return new(true, "一份食物已经放下，位置会影响它能否吃到。", "", "place", Changed: true);
    }

    private void Impulse(Creature c, double x, double z, double strength)
    {
        if (c.Interaction.Restraint != RestraintKind.None || c.Interaction.Carried) return;
        double magnitude = Math.Sqrt(x * x + z * z);
        if (magnitude < .001) { x = 1; z = 0; magnitude = 1; }
        c.Interaction.VelocityX += x / magnitude * strength;
        c.Interaction.VelocityZ += z / magnitude * strength;
    }

    public static string DescribeInteractionCondition(Creature c)
    {
        var i = c.Interaction;
        var details = new List<string>();
        if (c.Condition == PhysicalCondition.Critical) details.Add("重伤，急需稳定伤势");
        if (i.FootIntegrity <= 25 && UsesGroundFoot(c)) details.Add("底部受伤，无法自行走动");
        else if (i.FootIntegrity < 80) details.Add("底部有伤，动作受影响");
        if (i.FootTreated) details.Add("底部已经护理，仍需普通时间休养");
        if (i.Restraint != RestraintKind.None) details.Add("活动受装置或支撑方式限制");
        if (i.Dirt > 10) details.Add("身上有污处");
        if (i.Wetness > 15) details.Add("身体有些湿");
        if (c.Pain > 15) details.Add("碰到痛处会躲开");
        if (!i.HasAccessory) details.Add("没有戴着自己的饰品");
        if (c.Fullness < 30) details.Add("肚子饿了");
        if (c.Energy < 25) details.Add("需要休息");
        return details.Count > 0 ? string.Join("；", details) + "。" : "外表和动作暂时没有明显不适，仍可继续观察。";
    }
}
