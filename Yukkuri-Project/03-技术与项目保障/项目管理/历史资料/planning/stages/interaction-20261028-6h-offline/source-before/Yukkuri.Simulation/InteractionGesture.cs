namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    private sealed class ContactGesture
    {
        public required string TargetId;
        public required InteractionInput Input;
        public double Elapsed;
        public double ContactElapsed;
        public double Motion;
        public double FeedbackAt = -10;
        public bool Applied;
        public bool Refused;
        public bool NuzzleWelcomed;
        public bool RoughTouch;
        public double Effect;
        public InteractionFeedback Last = new(true, "", "", "idle");
    }

    private ContactGesture? _interactionGesture;
    public bool HasInteractionGesture => _interactionGesture != null;
    public string? InteractionGestureTarget => _interactionGesture?.TargetId;
    public InteractionAction? CurrentInteractionAction => _interactionGesture?.Input.Action;
    public double InteractionGestureSeconds => _interactionGesture?.Elapsed ?? 0;

    public InteractionFeedback BeginInteractionGesture(string id, InteractionInput input)
    {
        EndInteractionGesture(true);
        var c = InteractionTarget(id);
        string? invalid = ValidateInteraction(c, input);
        if (invalid != null) return Unavailable(invalid);
        if (!InteractionCatalog.Choice(input.Action).Continuous) return ExecuteInteraction(id, input);
        var oil = c!; var i = oil.Interaction;
        if (input.Action == InteractionAction.Rock && i.Restraint != RestraintKind.Wrap) return Unavailable("先包好，再轻轻摇。 ");
        if (input.Action == InteractionAction.Lift && (i.Restraint != RestraintKind.None || i.Falling)) return Unavailable("先解除装置或等它落稳，再托起。 ");
        if(input.Action==InteractionAction.Lift&&State.Creatures.Any(other=>other.Id!=oil.Id&&other.Interaction.Carried))
            return Unavailable("手里还托着另一只油，先把它放稳。 ");
        if (input.Action == InteractionAction.Throw && !i.Carried) return Unavailable("先托起，才可以抛出。 ");
        if(input.Action==InteractionAction.AdjustHanging&&i.Restraint!=RestraintKind.Hanging)return Unavailable("先使用悬挂装置，再调整支点。 ");
        if (input.Action is InteractionAction.Hold or InteractionAction.Press or InteractionAction.Roll && (i.Carried || i.Falling)) return Unavailable("先放到支撑面上。 ");
        if (input.Action is InteractionAction.CleanAccessory or InteractionAction.RepairAccessory && i.Accessory.Place == AccessoryPlace.Worn) return Unavailable("先取下饰品，再整理。 ");
        if (input.Action == InteractionAction.RepairAccessory && i.Accessory.Integrity <= 0) return Unavailable("这件已经无法修补，需要替换。 ");
        if (input.Action is InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Drink && i.EatingRemaining > 0) return Unavailable("等它咽下这一口，再递下一份。 ");
        if (input.Action == InteractionAction.PlayBall && !State.RoomObjects.Any(o => o.Kind == RoomObjectKind.Ball && !o.Stored)) return Unavailable("先把软球拿出来。 ");
        if (input.Action == InteractionAction.Care && !NeedsInteractionCare(oil)) return Unavailable("目前没有需要使用护理包的伤势。 ");
        var g = new ContactGesture { TargetId = id, Input = input };
        _interactionGesture = g;
        i.LastContact = input.Action.ToString();
        oil.LastPlayerTouchAt = State.WorldSeconds;
        if (input.Action == InteractionAction.Lift)
        {
            i.Carried = true; i.Height = Math.Max(.8, input.Height); i.Supine = false;
            i.VelocityX = 0; i.VelocityZ = 0; g.Applied = true;
            g.Last = Feedback(oil, oil.Fear > 50 || i.Memories.GetValueOrDefault("fall") > 0 ? "lift-fear" : "lift",
                oil.Fear > 50 ? Expression.Frightened : Expression.Curious, "已托稳。拖动搬运，选择“放稳”结束托举。", "carry");
        }
        else if (input.Action == InteractionAction.Hold)
        {
            if (i.Restraint != RestraintKind.None) { _interactionGesture = null; return Unavailable("它已被装置限制，无需再用手按住。 "); }
            i.Restraint = RestraintKind.Hand; g.Applied = true;
            g.Last = Feedback(oil, "restrained", Expression.Defiant, "手正在限制它，松开就会解除手的限制。", "held");
        }
        else if (input.Action == InteractionAction.BurnFeet && i.FireFear > 15)
            g.Last = Feedback(oil, "fire-memory", Expression.Frightened, "看见这个工具，它想起了以前的经历。", "guard", false);
        else g.Last = new(true, InteractionCatalog.Choice(input.Action).Hint, oil.Speech, PoseFor(input.Action));
        return g.Last;
    }

    public InteractionFeedback ContinueInteractionGesture(InteractionInput input, bool inContact)
    {
        var g = _interactionGesture;
        if (g == null) return Unavailable("没有进行中的动作。 ");
        var c = InteractionTarget(g.TargetId);
        if (c == null || input.Action != g.Input.Action) { EndInteractionGesture(true); return Unavailable("动作对象或工具已改变。 "); }
        string? invalid = ValidateInteraction(c, input);
        if (invalid != null) { EndInteractionGesture(true); return Unavailable(invalid); }
        // Samples never catch up a missed frame; leaving the contact surface pauses contact effects.
        double dt = Math.Min(.1, input.Duration);
        if (dt <= 0) return g.Last;
        bool freeMotion = input.Action is InteractionAction.Lift or InteractionAction.PlayBall or InteractionAction.Throw or InteractionAction.AdjustHanging;
        if (!inContact && !freeMotion)
        {
            g.ContactElapsed=0;g.NuzzleWelcomed=false;
            return g.Last with { Message = "手已移开，接触停止；松开结束这次动作。", Pose = "idle" };
        }
        bool careLocationChanged=input.Action==InteractionAction.Care&&input.Part!=g.Input.Part;
        g.Elapsed += dt; g.Input = input;
        bool validPart=input.Action switch
        {
            InteractionAction.Stroke=>input.Part==BodyPart.Head,
            InteractionAction.Nuzzle=>input.Part==BodyPart.Cheek,
            InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Drink=>input.Part==BodyPart.Mouth,
            InteractionAction.CleanAccessory or InteractionAction.RepairAccessory=>input.Part==BodyPart.Accessory,
            InteractionAction.BurnFeet=>input.Part==BodyPart.Bottom,
            InteractionAction.Care=>CanCareAt(c,input.Part),
            _=>true
        };
        g.ContactElapsed=validPart?(careLocationChanged?dt:g.ContactElapsed+dt):0;
        double motion = Math.Min(input.Motion, dt * 2.5);
        g.Motion += motion;
        var i = c.Interaction;
        double force = input.Force;
        if (g.Refused) return g.Last;
        bool speak = g.Elapsed - g.FeedbackAt >= 3;
        InteractionFeedback Say(string key, Expression expression, string description, string? pose = null)
        {
            if (speak) { g.FeedbackAt = g.Elapsed; g.Last = Feedback(c, key, expression, description, pose ?? PoseFor(input.Action)); }
            else g.Last = g.Last with { Message = description, Pose = pose ?? PoseFor(input.Action), Changed = true };
            return g.Last;
        }
        InteractionFeedback Refuse(string key, string reason)
        {
            g.Refused = true;
            return g.Last = Feedback(c, key, c.Pain > 30 ? Expression.Hurt : g.RoughTouch?Expression.Defiant:Expression.Shy, reason, "withdraw", false, true);
        }
        switch (input.Action)
        {
            case InteractionAction.Stroke:
            case InteractionAction.Nuzzle:
                bool nuzzle = input.Action == InteractionAction.Nuzzle;
                if (!validPart)
                {
                    g.NuzzleWelcomed=false;
                    return g.Last = new(true, nuzzle ? "靠近脸侧，再邀请它蹭蹭。" : "把手移到头顶，顺着头发摸。", "", "invite");
                }
                if (c.Fear > 68 || c.Trust < 12 || (input.Part == BodyPart.Head ? i.HeadSensitivity : i.CheekSensitivity) > 45)
                    return Refuse("touch-no", "它躲开接触，先让它缓一缓。 ");
                if(force>.65||motion/dt>1.8)
                {
                    // The affection tool does not make hard pressure or scrubbing affectionate.
                    Distress(c,.6+force);g.Applied=true;g.RoughTouch=true;
                    if(nuzzle)i.CheekSensitivity=Clamp100(i.CheekSensitivity+1+force);
                    else i.HeadSensitivity=Clamp100(i.HeadSensitivity+1+force);
                    return Refuse("touch-rough", "太用力或太急，它缩开了。先松手，等它愿意再轻轻接触。");
                }
                if (nuzzle && g.ContactElapsed < .45)
                    return g.Last = new(true, "手留在脸侧，等它回应……", "", "invite");
                if(nuzzle&&!g.NuzzleWelcomed)
                {
                    g.NuzzleWelcomed=true;g.FeedbackAt=g.Elapsed;
                    return g.Last=Feedback(c,"nuzzle-ready",c.Trust<35?Expression.Shy:Expression.Curious,
                        "它把脸侧轻轻靠过来了。小幅慢慢移动，和它一起蹭蹭。","nuzzle-ready");
                }
                if (motion <= .0005) return g.Last = g.Last with {
                    Message = nuzzle?"它还贴着手，等你轻轻回蹭。":"手停在这里，慢慢移动才会继续摸。",
                    Pose = nuzzle?"nuzzle-ready":"stroke" };
                double affinity = motion * (nuzzle ? 1.4 : 1) * (.5 + i.TouchPreference);
                ImproveAffection(c, affinity); i.NuzzleFamiliarity = Clamp100(i.NuzzleFamiliarity + (nuzzle ? motion * 3 : motion * .2));
                g.Applied = true; g.Effect += affinity;
                return Say(nuzzle ? c.Trust < 35 ? "nuzzle-shy" : "nuzzle" : "stroke", Expression.Content,
                    nuzzle ? "它贴着脸侧，轻轻回蹭。" : "它压低身子，顺着手的方向放松。");
            case InteractionAction.Lift:
                MoveCreature(c.Id, input.X, input.Z); i.Height = Math.Clamp(input.Height, .4, 3);
                return g.Last with { Pose = "carry", Message = "托稳了。Shift＋滚轮调高度；选择“放稳”再点落点。" };
            case InteractionAction.AdjustHanging:
                if(!CanOccupyRoom(c,input.X,input.Z))return g.Last=new(false,"支点旁没有足够空间，装置保持原位。",c.Speech,"hang");
                double hangingHeight=Math.Clamp(input.Height,.35*(c.IsYoung?.73:1),1.55*(c.IsYoung?.73:1));
                g.Applied|=Distance(c.X,c.Z,input.X,input.Z)>.01||Math.Abs(i.Height-hangingHeight)>.01;
                MoveCreature(c.Id,input.X,input.Z);i.Height=hangingHeight;
                return Say("hang",Expression.Calling,"支点与承托仍然连接。松开停止移动，选择“托住解下”才能解除。","hang");
            case InteractionAction.Rock:
                if (motion <= .0005) return g.Last;
                if (motion / dt > 1.8 || force > .65)
                {
                    Distress(c, dt * 1.5); g.Applied = true;
                    return Say("touch-no", Expression.Defiant, "摇得太急，它扭动着要求停下。");
                }
                ImproveAffection(c, motion * .6); i.WrapFamiliarity = Clamp100(i.WrapFamiliarity + motion * 2);
                g.Applied = true;
                return Say("rock", Expression.Content, "包裹稳稳托住它，随着小幅轻摇慢慢放松。");
            case InteractionAction.FeedMeal:
            case InteractionAction.FeedSweet:
            case InteractionAction.Drink:
                if (input.Part != BodyPart.Mouth) return g.Last = new(true, "把食物递到嘴边。", "", "offer");
                if (g.Applied) return g.Last;
                if (c.Fullness >= (input.Action == InteractionAction.FeedSweet ? 98 : 92) && input.Action != InteractionAction.Drink)
                    return Refuse("full", "它已经吃饱，不再接这一口。 ");
                if (i.FoodSuspicion > 35 && c.Trust < 40) return Refuse("suspicious-food", "它不敢直接接食，先把饭放到旁边。 ");
                if (g.ContactElapsed < .65) return g.Last = new(true, "递到了嘴边，等它接过去……", "", "offer");
                if (input.Action != InteractionAction.Drink && !State.UnlimitedResources && State.Food <= 0)
                    return Refuse("hungry", "食物已经用完。 ");
                if (input.Action != InteractionAction.Drink && !State.UnlimitedResources) State.Food--;
                c.Fullness = Clamp100(c.Fullness + (input.Action == InteractionAction.FeedMeal ? 19 : input.Action == InteractionAction.FeedSweet ? 9 : 0));
                i.EatingRemaining = 1.6; i.FoodSuspicion = Math.Max(0, i.FoodSuspicion - 2);
                ImproveAffection(c, input.Action == InteractionAction.FeedSweet ? .8 : .5);
                g.Applied = true;
                return Say(input.Action == InteractionAction.FeedSweet ? "sweet" : input.Action == InteractionAction.Drink ? "drink" : "eat",
                    Expression.Delighted, "这一口已经接下，等它吃完再递下一份。", "eat");
            case InteractionAction.Clean:
            case InteractionAction.Dry:
                if (motion <= .0005) return g.Last;
                if(input.Part==BodyPart.Accessory)return g.Last=new(true,"这是饰品，取下后用“擦饰品”整理。",c.Speech,"accessory-watch");
                bool dry = input.Action == InteractionAction.Dry;
                if (input.Part == BodyPart.Bottom && !i.Supine && !i.Carried) return g.Last = new(true, "先帮它翻身或托起，才能接触底部。", "", "invite");
                if (input.Part == BodyPart.Bottom && i.FootIntegrity < 35 && c.Pain > 30) return Refuse("touch-no", "碰到伤处会疼，先做适用护理。 ");
                double before = dry ? i.Wetness : i.Dirt;
                i.WipeSurface(input.Part,motion*(dry?16:18),dry);
                g.Applied |= before > (dry ? i.Wetness : i.Dirt);
                if (g.Applied) ImproveAffection(c, motion * .12);
                return Say(dry ? "dry" : "clean", Expression.Content, dry ? "毛巾带走湿润，伤势仍需单独护理。" : "擦到的污处正在变干净。");
            case InteractionAction.CleanAccessory:
                if(input.Part!=BodyPart.Accessory)return g.Last=new(true,"把毛巾移到取下的饰品上。","","accessory-watch");
                if(i.Accessory.Place==AccessoryPlace.Stored)return g.Last=Unavailable("饰品已收起，先拿出来。 ");
                if (motion <= .0005) return g.Last;
                i.Accessory.Dirt = Math.Max(0, i.Accessory.Dirt - motion * 20); g.Applied = true;
                return Say("accessory-care", Expression.Curious, "它盯着手里的饰品，看着污处被擦净。", "accessory-watch");
            case InteractionAction.RepairAccessory:
                if(input.Part!=BodyPart.Accessory)return g.Last=new(true,"要接触取下的饰品，才能继续修补。","","accessory-watch");
                if(i.Accessory.Place==AccessoryPlace.Stored)return g.Last=Unavailable("饰品已收起，先拿出来。 ");
                if (i.Accessory.Integrity >= 100) return g.Last = new(true, "饰品已经整理好了，可以戴回去。", c.Speech, "accessory-watch");
                i.Accessory.RepairProgress += dt * 12;
                i.Accessory.Integrity = Math.Min(100, i.Accessory.Integrity + dt * 9);
                g.Applied = true;
                return Say("repair", Expression.Curious, "修补正在进行，松开会保留已经完成的部分。", "accessory-watch");
            case InteractionAction.PlayBall:
                return g.Last = new(true, "拖出方向后松开，把球推过去。", "", "ball-aim");
            case InteractionAction.Care:
                if (g.Applied) return g.Last;
                if(!CanCareAt(c,input.Part))return g.Last=new(true,"这里不是需要处理的伤处。先看看，再把护理包移到相应部位。",c.Speech,"invite");
                if (g.ContactElapsed < 1.6) return g.Last = new(true, "正在处理伤处……保持接触，松开可中止。", c.Speech, "nursing");
                if (!State.UnlimitedResources && State.Medicine <= 0) return Refuse("inspect", "护理用品不足。 ");
                if (!State.UnlimitedResources) State.Medicine--;
                if (c.Condition == PhysicalCondition.Critical)
                {
                    c.Condition = PhysicalCondition.Recovering; c.Stabilized = true; c.Health = 8;
                    c.CriticalRemaining = 0; c.RecoveryRemaining = 55;
                }
                else c.Health = Clamp100(c.Health + 12);
                if (input.Part==BodyPart.Bottom&&i.FootIntegrity < 100) { i.FootTreated = true; i.FootRecovery = Math.Max(i.FootRecovery, 60); }
                c.Pain = Math.Max(0, c.Pain - 18);
                if(input.Part==BodyPart.Head)i.HeadSensitivity = Math.Max(0, i.HeadSensitivity - 10);
                if(input.Part==BodyPart.Cheek)i.CheekSensitivity = Math.Max(0, i.CheekSensitivity - 10);
                i.CareContinuity = Clamp100(i.CareContinuity + 5);
                ImproveAffection(c, .8); g.Applied = true;
                return Say("care", Expression.Shy, "护理完成。实际身体恢复需要普通时间、进食与休息。", "nursing");
            case InteractionAction.Block:
                c.TargetX = c.X; c.TargetZ = c.Z; i.WaitRemaining = .5; g.Applied = true;
                return Say("blocked", Expression.Defiant, "它前方被挡住，转头寻找其他方向。", "blocked");
            case InteractionAction.Tease:
                if (CanSelfMove(c) && i.FoodSuspicion < 75)
                { c.TargetX = input.X; c.TargetZ = input.Z; }
                if (motion > .005 && g.Elapsed > 1)
                { i.FoodSuspicion = Clamp100(i.FoodSuspicion + motion * 8); g.Applied = true; }
                return Say(g.Elapsed > 2 ? "teased" : "hungry", g.Elapsed > 2 ? Expression.Defiant : Expression.Curious,
                    i.FoodSuspicion >= 75 ? "它停下追逐，不再相信这次递来的甜甜。" : "它看着甜甜的位置，期待又落空。", "reach");
            case InteractionAction.Push:
            case InteractionAction.Throw:
                return g.Last = new(true, "方向已经指明，松开执行；右键取消。", c.Speech, input.Action == InteractionAction.Throw ? "carry" : "push-aim");
            case InteractionAction.Squeeze:
            case InteractionAction.Press:
                double pressure = dt * (.25 + force * 2.8);
                Distress(c, pressure, dt * Math.Max(0, force - .2) * 4, input.Action == InteractionAction.Press ? "按压" : "强握");
                g.Applied = true; g.Effect += pressure;
                return Say("squeeze", Expression.Hurt,
                    input.Action==InteractionAction.Press?"身体被向下压低，持续施力仍在造成不适。松开停止。":"身体从两侧被挤窄，持续施力仍在造成不适。松开停止。");
            case InteractionAction.Roll:
                if (i.Restraint != RestraintKind.None) return g.Last = Unavailable("先解除装置，再滚动。 ");
                if (motion <= .0005) return g.Last;
                GroundMove(c,input.X,input.Z,false);c.TargetX=c.X;c.TargetZ=c.Z;Distress(c, motion * (1 + force));
                i.SoilSurface(motion*2); g.Applied = true; g.Effect += motion;
                return Say("roll", Expression.Frightened, "身体沿着拖动方向滚动，动作与地面接触带来变化。", "roll");
            case InteractionAction.Hold:
                g.Applied = true;
                return Say("restrained", Expression.Defiant, "它被手按住；松手即可解除这层限制。", "held");
            case InteractionAction.BurnFeet:
                if (input.Part != BodyPart.Bottom) return g.Last = new(true, "工具还没有接触底部。", c.Speech, "tool-approach");
                double injury = dt * (2.5 + force * 10);
                i.FootIntegrity = Math.Max(0, i.FootIntegrity - injury);
                i.FootTreated = false; i.FootRecovery = 0; i.FireFear = Clamp100(i.FireFear + injury * .6);
                Distress(c, injury * .5, injury * .1, "底部受伤"); g.Applied = true; g.Effect += injury;
                return Say("foot-hurt", Expression.Hurt, UsesGroundFoot(c) && i.FootIntegrity <= 25
                    ? "底部已无法支撑自主移动。移开工具后，仍需辅助生活和护理。"
                    : "底部受到影响；移动能力按实际身体形态判断。", "foot-hurt");
            default:
                return g.Last;
        }
    }

    public InteractionFeedback EndInteractionGesture(bool cancelled = false)
    {
        var g = _interactionGesture;
        _interactionGesture = null;
        if (g == null) return new(false, "", "", "idle");
        var c = InteractionTarget(g.TargetId);
        if (c == null) return g.Last;
        var i = c.Interaction;
        if (g.Input.Action == InteractionAction.Hold && i.Restraint == RestraintKind.Hand) i.Restraint = RestraintKind.None;
        if (!cancelled && g.Input.Action == InteractionAction.Push && g.Motion > .04)
        {
            Impulse(c, g.Input.DirectionX, g.Input.DirectionZ, .6 + g.Input.Force * 1.8);
            Distress(c, 1 + g.Input.Force * 2); g.Applied = true;
            g.Last = Feedback(c, "angry", Expression.Defiant, "被向一旁推开，它稳住身体再看向你。", "push");
        }
        if (!cancelled && g.Input.Action == InteractionAction.Throw && i.Carried && g.Motion > .04)
        {
            i.Carried = false; i.Falling = true; i.VerticalVelocity = 1.5 + g.Input.Force * 2;
            Impulse(c, g.Input.DirectionX, g.Input.DirectionZ, 1 + g.Input.Force * 2.5);
            g.Applied = true;
            g.Last = Feedback(c, "fall", Expression.Frightened, "它离开手的支撑，沿着选定方向抛出。", "fall");
        }
        if (!cancelled && g.Input.Action == InteractionAction.PlayBall && g.Motion > .03)
        {
            var ball = State.RoomObjects.FirstOrDefault(o => o.Kind == RoomObjectKind.Ball && !o.Stored);
            if (ball != null)
            {
                double length = Math.Sqrt(g.Input.DirectionX * g.Input.DirectionX + g.Input.DirectionZ * g.Input.DirectionZ);
                if (length > .001)
                {
                    ball.VelocityX = g.Input.DirectionX / length * Math.Min(3.5, .8 + g.Motion * 2);
                    ball.VelocityZ = g.Input.DirectionZ / length * Math.Min(3.5, .8 + g.Motion * 2);
                    g.Applied = true; g.Last = new(true, "球滚出去了，看看它怎样接。", "", "play", Changed: true);
                }
            }
        }
        if (g.Applied)
        {
            c.ActionCounts[g.Input.Action.ToString()] = c.ActionCounts.GetValueOrDefault(g.Input.Action.ToString()) + 1;
            string key = g.RoughTouch?"rough-touch":g.Input.Action switch
            {
                InteractionAction.Stroke => "stroke", InteractionAction.Nuzzle => "nuzzle",
                InteractionAction.Tease => "teasing", InteractionAction.BurnFeet => "fire",
                InteractionAction.Care => "nursing", _ => g.Input.Action.ToString()
            };
            string description = g.RoughTouch?"本想轻轻接触，却被过重或过急地揉蹭，缩开了身体。":g.Input.Action switch
            {
                InteractionAction.Stroke => "被顺着头顶慢慢摸过。", InteractionAction.Nuzzle => "和手指贴着脸蹭蹭。",
                InteractionAction.Tease => "期待的甜甜被反复移开。", InteractionAction.BurnFeet => "底部受伤，记住了这件工具。",
                InteractionAction.Care => "伤处被认真护理，还需要休息。", _ => $"经历了{InteractionCatalog.Choice(g.Input.Action).Label}。"
            };
            RecordInteraction(c, key, description, Math.Max(.3, Math.Min(8, g.Effect)));
        }
        return g.Last with { Pose = i.Carried ? "carry" : "idle" };
    }

    private static bool NeedsInteractionCare(Creature c) => c.Health < 95 || c.Pain > 5 ||
        c.Condition == PhysicalCondition.Critical || c.Interaction.FootIntegrity < 100 && !c.Interaction.FootTreated;
    private static bool CanCareAt(Creature c,BodyPart part)=>part!=BodyPart.Accessory&&
        (c.Condition==PhysicalCondition.Critical||c.Health<95||c.Pain>5||
         part==BodyPart.Bottom&&c.Interaction.FootIntegrity<100&&!c.Interaction.FootTreated);

    public static string PoseFor(InteractionAction action) => action switch
    {
        InteractionAction.Stroke => "stroke", InteractionAction.Nuzzle => "nuzzle", InteractionAction.Lift => "carry",
        InteractionAction.Rock => "rock", InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Drink => "offer",
        InteractionAction.Clean or InteractionAction.Dry => "wipe", InteractionAction.Care => "nursing",
        InteractionAction.Press => "pressure", InteractionAction.Squeeze => "squeeze", InteractionAction.Roll => "roll",
        InteractionAction.BurnFeet => "foot-hurt", InteractionAction.Tease => "reach", _ => "idle"
    };
}
