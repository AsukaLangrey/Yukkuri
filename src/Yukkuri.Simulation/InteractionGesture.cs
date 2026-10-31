namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    private sealed class ContactGesture
    {
        public required string TargetId;
        public required InteractionInput Input;
        public string? ObjectId;
        public double Elapsed;
        public double ContactElapsed;
        public double Motion;
        public double FeedbackAt = -10;
        public string LastSituation="";
        public bool Applied;
        public bool ContactActive=true;
        public bool Refused;
        public bool NuzzleWelcomed;
        public bool RoughTouch;
        public bool StopImpact;
        public bool RepairFinished;
        public bool CompletionReleased;
        public bool CarryHeightConcern;
        public double FastTouchSeconds;
        public double BaitAnticipation;
        public bool BaitReady;
        public int BaitWithdrawals;
        public int ReactionSerial=-1;
        public double Effect;
        public double ObjectX;
        public double ObjectZ;
        public InteractionFeedback Last = new(true, "", "", "idle");
    }

    private ContactGesture? _interactionGesture;
    private const double NuzzleReplyHoldSeconds=.35;
    public bool HasInteractionGesture => _interactionGesture != null;
    // A completed external support hold is retained only to consume the release,
    // not to reserve its mother against feeding, family care or birth.
    public string? InteractionGestureTarget
    {
        get
        {
            if(_interactionGesture is not { } g)return null;
            if(g.Input.TargetKind is InteractionTargetKind.DetachedStem or InteractionTargetKind.SupplyStation)return null;
            if(g.Input.TargetKind==InteractionTargetKind.Stem&&(!g.ContactActive||
                g.Applied&&g.Input.Action==InteractionAction.SupportStem||
                State.Creatures.Find(c=>c.Id==g.TargetId) is not { } mother||AttachedStem(mother)?.EventId!=g.ObjectId))return null;
            return g.TargetId;
        }
    }
    public string? InteractionGestureObject=>_interactionGesture?.ObjectId;
    public InteractionTargetKind? CurrentInteractionTargetKind=>_interactionGesture?.Input.TargetKind;
    public SurfaceSide CurrentInteractionSide=>_interactionGesture?.Input.Side??SurfaceSide.Whole;
    public InteractionAction? CurrentInteractionAction => _interactionGesture?.Input.Action;
    public double InteractionGestureSeconds => _interactionGesture?.Elapsed ?? 0;
    public bool InteractionGestureOwnsPose=>_interactionGesture is { Refused:false } g&&
        State.Creatures.Find(o=>o.Id==g.TargetId) is { Condition:not PhysicalCondition.Dead }&&
        (g.Input.TargetKind!=InteractionTargetKind.Stem||
            State.Creatures.Find(o=>o.Id==g.TargetId) is { } stemMother&&AttachedStem(stemMother)?.EventId==g.ObjectId&&
            (!g.Applied&&(g.Input.Action is InteractionAction.SupportStem or InteractionAction.CutStem)||g.Input.Action==InteractionAction.Burn))&&
        (g.ContactActive||g.Input.Action is InteractionAction.Lift or InteractionAction.Throw or InteractionAction.AdjustHanging or InteractionAction.Tease or InteractionAction.Roll)&&
        (g.Input.Action switch
        {
            InteractionAction.Touch=>g.Input.Motion>.000001&&!ContactMotion.IsWithdrawing(g.Input.TouchNormalSpeed,g.Input.TouchTangentFraction),
            InteractionAction.Stroke or InteractionAction.Clean or InteractionAction.Dry or InteractionAction.CleanAccessory=>g.Input.Motion>.0005,
            // An invitation has a short stationary contact phase; waiting after
            // that response is not a grip that can prevent the oil from leaving.
            InteractionAction.Nuzzle=>g.Input.Motion>.0005||g.Input.Part==BodyPart.Cheek&&
                (g.ContactElapsed<.45||g.NuzzleWelcomed&&g.Elapsed-g.FeedbackAt<NuzzleReplyHoldSeconds),
            InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Drink=>!g.Applied&&g.Input.Part==BodyPart.Mouth&&
                State.Creatures.Find(o=>o.Id==g.TargetId) is { } recipient&&!MouthfulStillSettling(recipient),
            InteractionAction.Care=>!g.Applied&&State.Creatures.Find(o=>o.Id==g.TargetId) is { } patient&&
                CanCareAt(patient,g.Input.Part,g.Input.Side),
            InteractionAction.RepairAccessory=>!g.RepairFinished&&g.Input.Part==BodyPart.Accessory,
            _=>true
        });
    private bool InteractionGestureBlocksAutonomy=>(InteractionGestureOwnsPose&&CurrentInteractionAction!=InteractionAction.PlayBall)||
        _interactionGesture is { } g&&CompletedGestureStillSettling(g);
    public bool InteractionIntakeAccepted=>_interactionGesture is {Applied:true} g&&
        g.Input.TargetKind==InteractionTargetKind.Body&&
        g.Input.Action is InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Drink;

    private static bool MouthfulStillSettling(Creature c)=>c.Interaction.EatingRemaining>0||
        c.Interaction.ReactionPoseRemaining>0&&c.Interaction.ReactionPose=="swallow"&&c.Interaction.ReactionSituation=="mouthful-done";

    private bool CompletedGestureStillSettling(ContactGesture g)
    {
        bool feeding=g.Input.Action is InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Drink;
        bool completed=(feeding||g.Input.Action==InteractionAction.Care)?g.Applied:
            g.Input.Action==InteractionAction.RepairAccessory&&g.RepairFinished;
        if(!completed||g.CompletionReleased)return false;
        var c=State.Creatures.Find(o=>o.Id==g.TargetId);
        bool settling=c!=null&&(feeding?!c.Interaction.GroundIntake&&MouthfulStillSettling(c):
            c.ReactionSerial==g.ReactionSerial&&c.Interaction.ReactionPoseRemaining>0);
        // Completion keeps its current body response, not the preceding input frame's
        // pose. Once that response ends, later autonomous intake cannot reacquire it.
        if(!settling)g.CompletionReleased=true;
        return settling;
    }

    public InteractionFeedback BeginInteractionGesture(string id, InteractionInput input)
    {
        EndInteractionGesture(true);
        if(ShopActionError(input.Action) is { } shopError)return Unavailable(shopError);
        if(input.TargetKind==InteractionTargetKind.SupplyStation)return BeginStationCore(id,input);
        if(input.TargetKind==InteractionTargetKind.DetachedStem)return BeginDetachedStemCore(id,input);
        var c = InteractionTarget(id);
        string? invalid = ValidateInteraction(c, input);
        if (invalid != null) return Unavailable(invalid);
        if(input.TargetKind==InteractionTargetKind.Stem)return BeginStemGesture(c!,input);
        if (!InteractionCatalog.Choice(input.Action).Continuous) return ExecuteInteraction(id, input);
        var oil = c!; var i = oil.Interaction;
        int previousReaction=oil.ReactionSerial;
        if (input.Action == InteractionAction.Rock && i.Restraint != RestraintKind.Wrap) return Unavailable("先包好，再轻轻摇。 ");
        if (input.Action == InteractionAction.Lift && (i.Restraint != RestraintKind.None || i.Falling)) return Unavailable("先解除装置或等它落稳，再托起。 ");
        if(input.Action==InteractionAction.Lift&&State.Creatures.Any(other=>other.Id!=oil.Id&&other.Interaction.Carried))
            return Unavailable("手里还托着另一只油，先把它放稳。 ");
        if (input.Action == InteractionAction.Throw && !i.Carried) return Unavailable("先托起，才可以抛出。 ");
        if(input.Action==InteractionAction.AdjustHanging&&i.Restraint!=RestraintKind.Hanging)return Unavailable("先使用悬挂装置，再调整支点。 ");
        if (input.Action is InteractionAction.Hold or InteractionAction.Press or InteractionAction.Roll && (i.Carried || i.Falling)) return Unavailable("先放到支撑面上。 ");
        if(input.Action==InteractionAction.Roll)
        {
            if(i.Restraint!=RestraintKind.None)return Unavailable("先解除装置，再滚动。 ");
            if(!CanOccupyRoom(oil,oil.X,oil.Z,supine:true))return Unavailable("这里没有足够的翻滚空间，先搬到空处。 ");
        }
        if (input.Action is InteractionAction.CleanAccessory or InteractionAction.RepairAccessory && i.Accessory.Place == AccessoryPlace.Worn) return Unavailable("先取下饰品，再整理。 ");
        if (input.Action == InteractionAction.RepairAccessory && i.Accessory.Integrity <= 0) return Unavailable("这件已经无法修补，需要替换。 ");
        if (input.Action is InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Drink && i.EatingRemaining > 0) return Unavailable("等它咽下这一口，再递下一份。 ");
        RoomObject? ball=input.Action==InteractionAction.PlayBall?State.RoomObjects.FirstOrDefault(o=>o.Kind==RoomObjectKind.Ball&&!o.Stored&&o.RegionId==oil.RegionId&&(input.ObjectId==null||o.Id==input.ObjectId)):null;
        if(input.Action==InteractionAction.PlayBall&&ball==null)return Unavailable("先点到当前小院里可见的软球，再拖动。 ");
        if (input.Action == InteractionAction.Care && !NeedsInteractionCare(oil)) return Unavailable("目前没有需要使用护理包的伤势。 ");
        if(input.Action==InteractionAction.Care&&UsesConsumableStock&&State.Medicine<=0)
            return Unavailable("护理用品不足，先到补给处准备一包。 ");
        if(input.Action==InteractionAction.Care&&FamilyRules.UnderIntervention(oil))
            CancelIntervention(oil,"开始伤势护理，繁育干预已停止；已用材料和关系后果保留。");
        if(input.Action==InteractionAction.Tease&&UsesConsumableStock&&State.Food<=0)return Unavailable("手边没有甜甜，先准备一份食物。 ");
        bool foodAction=input.Action is InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Drink;
        if(foodAction&&input.ObjectId!=null&&OfferedSupply(input,oil)==null)return Unavailable("刚才拿起的食水已经不在手里。 ");
        if(foodAction&&input.Action==InteractionAction.Drink&&input.ObjectId==null&&UsesConsumableStock&&State.StationWaterPortions<=0)return Unavailable("清水库存不足，请先购买清水。");
        if(foodAction&&input.Action!=InteractionAction.Drink&&input.ObjectId==null&&UsesConsumableStock&&State.Food<=0)
            return Unavailable("食物已经用完，先准备一份。 ");
        var g = new ContactGesture { TargetId = id, Input = input,ObjectId=foodAction?input.ObjectId:ball?.Id };
        if(input.Action==InteractionAction.RepairAccessory&&i.Accessory.Integrity>=100)
        {g.RepairFinished=true;g.CompletionReleased=true;}
        _interactionGesture = g;
        if(input.Action==InteractionAction.Throw)i.Lowering=false;
        i.LastContact = input.Action.ToString();
        oil.LastPlayerTouchAt = State.WorldSeconds;
        if (input.Action == InteractionAction.Lift)
        {
            ClearCompanionship(oil);
            i.Lowering=false;
            i.Resting=false;
            i.Height=i.Carried?Math.Clamp(input.Height,.4,3):Math.Max(.8,input.Height);
            i.Carried = true; i.Supine = false;i.SupineRemaining=0;i.WetRestRemaining=0;
            i.VelocityX = 0; i.VelocityZ = 0; g.Applied = true;
            g.Last = Feedback(oil, oil.Fear > 50 || i.Memories.GetValueOrDefault("fall") > 0 ? "lift-fear" : "lift",
                oil.Fear > 50 ? Expression.Frightened : Expression.Curious, "已托稳。拖动搬运，选择“放稳”结束托举。", "carry");
        }
        else if(input.Action==InteractionAction.PlayBall)
        {
            ball!.VelocityX=0;ball.VelocityZ=0;
            g.Last=new(true,"已经按住这颗软球。拖出方向后松手，右键取消。",oil.Speech,"ball-aim");
        }
        else if(input.Action==InteractionAction.Roll)
        {
            i.BeginRoll();i.VelocityX=0;i.VelocityZ=0;
            oil.TargetX=oil.X;oil.TargetZ=oil.Z;
            g.Last=new(true,"拖动才会滚动；手停下，动作也会停下。",oil.Speech,"roll");
        }
        else if (input.Action == InteractionAction.Hold)
        {
            if (i.Restraint != RestraintKind.None) { _interactionGesture = null; return Unavailable("它已被装置限制，无需再用手按住。 "); }
            i.Restraint = RestraintKind.Hand; g.Applied = true;
            g.Last = Feedback(oil, "restrained", Expression.Defiant, "手正在限制它，松开就会解除手的限制。", "held");
        }
        else if (input.Action is InteractionAction.BurnFeet or InteractionAction.Burn && i.FireFear > 15)
            g.Last = Feedback(oil, "fire-memory", Expression.Frightened, "看见这个工具，它想起了以前的经历。", "guard", false);
        else g.Last = new(true, InteractionCatalog.Choice(input.Action).Hint, oil.Speech, PoseFor(input.Action));
        if(oil.ReactionSerial!=previousReaction)g.ReactionSerial=oil.ReactionSerial;
        SynchronizeWildSelfCare(oil);
        return g.Last;
    }

    public InteractionFeedback ContinueInteractionGesture(InteractionInput input, bool inContact)
    {
        if(ShopActionError(input.Action) is { } shopError){EndInteractionGesture(true);return Unavailable(shopError);}
        var g = _interactionGesture;
        if (g == null) return Unavailable("没有进行中的动作。 ");
        if(g.Input.TargetKind==InteractionTargetKind.SupplyStation)return ContinueStationGesture(g,input,inContact);
        if(g.Input.TargetKind==InteractionTargetKind.DetachedStem)return ContinueDetachedStemGesture(g,input,inContact);
        var c = InteractionTarget(g.TargetId);
        if (c == null || input.Action != g.Input.Action) { EndInteractionGesture(true); return Unavailable("动作对象或工具已改变。 "); }
        if(input.TargetKind!=g.Input.TargetKind||input.TargetKind==InteractionTargetKind.Stem&&input.ObjectId!=g.ObjectId)
        {EndInteractionGesture(true);return Unavailable("接触目标已改变，请收回工具重新对准。 ");}
        if(input.Action is InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Drink&&input.ObjectId!=g.ObjectId)
        {EndInteractionGesture(true);return Unavailable("手里的食水已经改变，先收回再递。 ");}
        string? invalid = ValidateInteraction(c, input);
        if (invalid != null) { EndInteractionGesture(true); return Unavailable(invalid); }
        if(input.TargetKind==InteractionTargetKind.Stem)return ContinueStemGesture(g,c,input,inContact);
        if(input.Action==InteractionAction.PlayBall&&(input.ObjectId!=null&&input.ObjectId!=g.ObjectId||!State.RoomObjects.Any(o=>o.Id==g.ObjectId&&!o.Stored&&o.RegionId==c.RegionId&&o.Kind==RoomObjectKind.Ball)))
        {EndInteractionGesture(true);return Unavailable("刚才按住的软球已经不在这里，动作已停止。 ");}
        // Samples never catch up a missed frame; leaving the contact surface pauses contact effects.
        double dt = Math.Min(.1, input.Duration);
        if (dt <= 0) return g.Last;
        bool freeMotion = input.Action is InteractionAction.Lift or InteractionAction.PlayBall or InteractionAction.Throw or InteractionAction.AdjustHanging or InteractionAction.Tease;
        if (!inContact && !freeMotion)
        {
            g.ContactActive=false;
            g.ContactElapsed=0;g.NuzzleWelcomed=false;g.FastTouchSeconds=0;
            if(input.Action==InteractionAction.Hold&&c.Interaction.Restraint==RestraintKind.Hand)c.Interaction.Restraint=RestraintKind.None;
            if(input.Action==InteractionAction.Push)g.Motion=0;
            if(c.ReactionSerial==g.ReactionSerial){c.Interaction.ReactionPose="idle";c.Interaction.ReactionPoseRemaining=0;}
            return g.Last with { Message = "手已移开，接触停止；松开结束这次动作。", Pose = InteractionPresentation.Pose(c),Changed=false };
        }
        bool resumedContact=!g.ContactActive&&!freeMotion;
        g.ContactActive=inContact;
        bool careLocationChanged=input.Action is InteractionAction.Care or InteractionAction.Nuzzle&&(input.Part!=g.Input.Part||input.Side!=g.Input.Side);
        if(careLocationChanged&&input.Action==InteractionAction.Nuzzle)g.NuzzleWelcomed=false;
        g.Elapsed += dt; g.Input = input;
        bool validPart=input.Action switch
        {
            InteractionAction.Stroke=>input.Part==BodyPart.Head,
            InteractionAction.Nuzzle=>input.Part==BodyPart.Cheek,
            InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Drink=>input.Part==BodyPart.Mouth,
            InteractionAction.CleanAccessory or InteractionAction.RepairAccessory=>input.Part==BodyPart.Accessory,
            InteractionAction.BurnFeet=>input.Part==BodyPart.Bottom,
            InteractionAction.Burn=>input.Part!=BodyPart.Accessory,
            InteractionAction.Care=>CanCareAt(c,input.Part,input.Side),
            _=>true
        };
        double contactProgress=dt;
        if(input.Action is InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Drink)
            contactProgress*=MouthFunction.IntakeRate(c,input.Action==InteractionAction.Drink?IntakeKind.Water:
                input.Action==InteractionAction.FeedSweet?IntakeKind.Sweet:IntakeKind.Meal);
        g.ContactElapsed=validPart?(careLocationChanged?contactProgress:g.ContactElapsed+contactProgress):0;
        double motion = resumedContact?0:Math.Min(input.Motion, dt * 2.5);
        g.Motion += motion;
        var i = c.Interaction;
        if(!g.Refused&&!InteractionGestureOwnsPose&&g.ReactionSerial==c.ReactionSerial&&
            (input.Action is InteractionAction.Touch or InteractionAction.Stroke or InteractionAction.Nuzzle or
                InteractionAction.Clean or InteractionAction.Dry or InteractionAction.CleanAccessory||
                input.Action==InteractionAction.Care&&!g.Applied||input.Action==InteractionAction.RepairAccessory&&!g.RepairFinished))
        {
            // Releasing the idle gesture also releases its own body pose, while
            // preserving a newer autonomous intake, help or injury response.
            i.ReactionPose="idle";i.ReactionPoseRemaining=0;
        }
        double force = input.Force;
        // Refusal permits avoidance, but is not immunity to a later physical impact
        // while the hand still actually reaches the body. One impact per held gesture.
        if (g.Refused && !(input.Action==InteractionAction.Touch&&input.StopImpact>0&&!g.StopImpact&&
            !resumedContact&&input.Part!=BodyPart.Accessory)) return g.Last;
        bool speak = g.Elapsed - g.FeedbackAt >= 3;
        InteractionFeedback Say(string key, Expression expression, string description, string? pose = null)
        {
            if (speak||key!=g.LastSituation)
            {g.FeedbackAt=g.Elapsed;g.LastSituation=key;g.Last=Feedback(c,key,expression,description,pose??PoseFor(input.Action));g.ReactionSerial=c.ReactionSerial;}
            else g.Last = g.Last with { Message = description, Pose = pose ?? PoseFor(input.Action), Changed = true };
            return g.Last;
        }
        InteractionFeedback Refuse(string key, string reason)
        {
            g.Refused = true;
            var expression=key is "full" or "not-thirsty"?Expression.Content:c.Pain>30?Expression.Hurt:c.Fear>60?Expression.Frightened:g.RoughTouch?Expression.Defiant:Expression.Worried;
            return g.Last = Feedback(c,key,expression,reason,"withdraw",false,true);
        }
        switch (input.Action)
        {
            case InteractionAction.Touch:
                if (input.Part == BodyPart.Accessory)
                    return g.Last = new(true,"这里是饰品，移动到身体上再接触。",c.Speech,"idle");
                if(input.StopImpact>0&&!resumedContact)
                {
                    double sensitivity=input.Part==BodyPart.Cheek?i.CheekSensitivityAt(input.Side):i.HeadSensitivity;
                    Distress(c,1+input.StopImpact*3,input.StopImpact*(1+sensitivity*.02),"接触中急停的冲击");
                    i.SetReactionDirection(input.DirectionX,input.DirectionZ);
                    if(input.Part==BodyPart.Cheek)i.ChangeCheekSensitivity(input.Side,1+input.StopImpact*2);
                    else if(input.Part==BodyPart.Head)i.HeadSensitivity=Clamp100(i.HeadSensitivity+1+input.StopImpact*2);
                    g.Applied=true;g.RoughTouch=true;g.StopImpact=true;g.Refused=true;g.Effect+=input.StopImpact*3;
                    return g.Last=Feedback(c,"hurt",Expression.Hurt,"手快速移动后在接触处急停，它受到一下冲击，缩开身体。","slap",true,true);
                }
                if (motion <= .000001)
                    return g.Last = g.Last with { Message="手轻轻停在这里；慢慢移动才会继续抚摸。",Pose="stroke" };
                if(ContactMotion.IsWithdrawing(input.TouchNormalSpeed,input.TouchTangentFraction))
                {
                    g.FastTouchSeconds=0;
                    return g.Last=new(true,"手正从身体旁收开。",c.Speech,"idle",Changed:false);
                }
                if (force > .62)
                {
                    g.FastTouchSeconds += dt;
                    // Speed by itself is brisk rubbing. A stop impact is handled separately above.
                    if (g.FastTouchSeconds < .35)
                        return g.Last = new(true,"手移动得很急，它绷紧了身体。",c.Speech,"guard");
                    Distress(c,.8+force*1.5);
                    if(input.Part==BodyPart.Cheek)i.ChangeCheekSensitivity(input.Side,1+force);
                    else if(input.Part==BodyPart.Head)i.HeadSensitivity=Clamp100(i.HeadSensitivity+1+force);
                    i.SetReactionDirection(input.DirectionX,input.DirectionZ);
                    g.Applied=true;g.RoughTouch=true;g.Effect+=1+force;
                    return Refuse("touch-rough","碰得太急，它缩开身体。先收手，给它留一点距离。");
                }
                g.FastTouchSeconds=Math.Max(0,g.FastTouchSeconds-dt*2);
                if(!InteractionReadiness.WillAcceptAffection(c,input.Part,input.Side))
                    return Refuse(c.Pain>=40?"touch-sore":"touch-no",InteractionReadiness.AffectionRefusal(c,input.Part));
                if(input.TouchTangentFraction<.1)
                    return g.Last=input.TouchNormalSpeed<-.01?
                        new(true,"手轻轻抵着身体；顺着表面移动才是抚摸。",c.Speech,"touch-press"):
                        new(true,"手正从身体旁收开。",c.Speech,"idle");
                double comfort=motion*input.TouchTangentFraction*(.3+i.TouchPreference*.5)*(1-force);
                ImproveAffection(c,comfort);g.Applied=true;g.Effect+=comfort;
                return Say("stroke",Expression.Content,input.Part==BodyPart.Head?
                    "手顺着头顶轻轻移动，它慢慢放松。":"手轻轻贴着身体移动，它留意着接触的方向。","stroke");
            case InteractionAction.Stroke:
            case InteractionAction.Nuzzle:
                bool nuzzle = input.Action == InteractionAction.Nuzzle;
                if (!validPart)
                {
                    g.NuzzleWelcomed=false;g.FastTouchSeconds=0;
                    return g.Last = new(true, nuzzle ? "靠近脸侧，再邀请它蹭蹭。" : "把手移到头顶，顺着头发摸。", "", "invite");
                }
                if (!InteractionReadiness.WillAcceptAffection(c,input.Part,input.Side))
                    return Refuse(c.Pain>=40?"touch-sore":"touch-no",InteractionReadiness.AffectionRefusal(c,input.Part));
                bool rushing=motion/dt>1.8;
                g.FastTouchSeconds=rushing?g.FastTouchSeconds+dt:Math.Max(0,g.FastTouchSeconds-dt*2);
                if(force>.65||g.FastTouchSeconds>=.35)
                {
                    // The affection tool does not make hard pressure or scrubbing affectionate.
                    Distress(c,.6+force);g.Applied=true;g.RoughTouch=true;
                    if(nuzzle)i.ChangeCheekSensitivity(input.Side,1+force);
                    else i.HeadSensitivity=Clamp100(i.HeadSensitivity+1+force);
                    return Refuse(nuzzle?"nuzzle-rough":"touch-rough", "太用力或太急，它缩开了。先松手，等它愿意再轻轻接触。");
                }
                if(rushing)
                {
                    // A single jitter or delayed sample is a cue to slow down, not an attack.
                    g.NuzzleWelcomed=false;g.ContactElapsed=0;
                    return g.Last=new(true,"手移动得有点急，放慢一点再轻轻接触。",c.Speech,"invite");
                }
                if (nuzzle && g.ContactElapsed < .45)
                    return g.Last = new(true, "手留在脸侧，等它回应……", "", "invite");
                if(nuzzle&&!g.NuzzleWelcomed)
                {
                    g.NuzzleWelcomed=true;g.FeedbackAt=g.Elapsed;
                    g.Last=Feedback(c,"nuzzle-ready",c.Trust<35?Expression.Shy:Expression.Curious,
                        "它把脸侧轻轻靠过来了。小幅慢慢移动，和它一起蹭蹭。","nuzzle-ready");
                    g.ReactionSerial=c.ReactionSerial;return g.Last;
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
                MoveCreature(c.Id, input.X, input.Z); i.Height = InteractionFixtureGeometry.ClampSupportHeight(input.Height,1,false);
                if(i.Height>=1.8&&(c.Fear>35||i.Memories.GetValueOrDefault("fall")>0))g.CarryHeightConcern=true;
                if(g.CarryHeightConcern&&i.Height>1.2)
                    return Say("lift-high",Expression.Frightened,"它缩紧身体，请求放低；保持托住，Shift＋滚轮可以慢慢降下来。","carry-tense");
                if(g.CarryHeightConcern)
                {
                    g.CarryHeightConcern=false;
                    return Say("lift-low",c.Fear>50?Expression.Shy:Expression.Curious,"已经降低了，手仍托着它。它看了看下面，慢慢松开紧绷的身体。","carry-ease");
                }
                return g.Last with { Pose = i.ReactionSituation=="lift-low"&&i.ReactionPoseRemaining>0?"carry-ease":"carry", Message = "托稳了。Shift＋滚轮调高度；选择“放稳”再点落点。" };
            case InteractionAction.AdjustHanging:
                if(!CanOccupyRoom(c,input.X,input.Z))return g.Last=new(false,"支点旁没有足够空间，装置保持原位。",c.Speech,"hang");
                double hangingHeight=InteractionFixtureGeometry.ClampSupportHeight(input.Height,c.IsYoung?.73:c.IsElder?1.03:1,true);
                g.Applied|=Distance(c.X,c.Z,input.X,input.Z)>.01||Math.Abs(i.Height-hangingHeight)>.01;
                MoveCreature(c.Id,input.X,input.Z);i.Height=hangingHeight;
                return Say("hang",Expression.Calling,"支点与承托仍然连接。松开停止移动，选择“托住解下”才能解除。","hang");
            case InteractionAction.Rock:
                if (motion <= .0005) return g.Last;
                bool hurriedRock=motion/dt>1.8;
                g.FastTouchSeconds=hurriedRock?g.FastTouchSeconds+dt:Math.Max(0,g.FastTouchSeconds-dt*2);
                if (g.FastTouchSeconds>=.35 || force > .65)
                {
                    Distress(c, .6+force); g.Applied = true;g.RoughTouch=true;
                    return Refuse("rock-rough","摇得太急，它扭动着要求停下。包裹仍然承托着它，先收手。 ");
                }
                if(hurriedRock)return g.Last=new(true,"手移动得有点急，放慢一点再轻摇。",c.Speech,"invite");
                ImproveAffection(c, motion * .6); i.WrapFamiliarity = Clamp100(i.WrapFamiliarity + motion * 2);
                g.Applied = true;
                return Say("rock", Expression.Content, "包裹稳稳托住它，随着小幅轻摇慢慢放松。");
            case InteractionAction.FeedMeal:
            case InteractionAction.FeedSweet:
            case InteractionAction.Drink:
                bool drinking=input.Action==InteractionAction.Drink;
                if (g.Applied) return g.Last=g.Last with {
                    Message=!g.CompletionReleased&&i.EatingRemaining>0?(drinking?"它还在慢慢咽水，杯子可以收回。":"这一口还在嘴里，等它慢慢咽下。"):
                        "这一口已经咽好了。先松开，再递下一份。",
                    Speech=c.Speech,Pose=InteractionPresentation.Pose(c),Changed=false };
                if (input.Part != BodyPart.Mouth) return g.Last = new(true, drinking?"把杯沿递到嘴边。":"把食物递到嘴边。", "", InteractionPresentation.Pose(c));
                if(MouthfulStillSettling(c))
                {
                    // The hand may have waited off-mouth while autonomous intake
                    // started. Do not replace that bite or count its time as an offer.
                    g.ContactElapsed=0;
                    return g.Last=new(true,"等它咽下嘴里的这一口，再递过来。",c.Speech,InteractionPresentation.Pose(c));
                }
                if(drinking&&i.Hydration>=95)return Refuse("not-thirsty","它已经不渴，偏开脸，不再接水。 ");
                if (c.Fullness >= (input.Action == InteractionAction.FeedSweet ? 98 : 92) && input.Action != InteractionAction.Drink)
                    return Refuse("full", "它已经吃饱，不再接这一口。 ");
                if(!drinking&&i.Digestion.Symptomatic&&(c.Fullness>=65||DigestiveFoodPause(c)))
                    return Refuse("stomach-no-food","肚子不舒服，它想先歇一会儿，不急着再吃。");
                bool urgentIntake=(drinking?i.Hydration:c.Fullness)<12;
                if (i.FoodSuspicion > 35 && c.Trust < 40&&!urgentIntake) return Refuse(drinking?"suspicious-water":"suspicious-food", drinking?"它还在提防杯子，先收手，让它确认。 ":"它不敢直接接食，先把饭放到旁边。 ");
                if(c.Trust<15&&!urgentIntake)
                    return Refuse(drinking?"suspicious-water":"suspicious-food","它还不太相信这只手。先放到地上，再退开一些。");
                if (g.ContactElapsed < .65) return g.Last = new(true, drinking?"杯沿到了嘴边，等它自己抿一口……":"递到了嘴边，等它接过去……", "", "offer");
                var supply=OfferedSupply(input,c);
                if(input.ObjectId!=null&&supply==null)return Refuse("suspicious-food","手里的这一份已经没有了，先收手。 ");
                if (input.ObjectId==null&&input.Action != InteractionAction.Drink && UsesConsumableStock && State.Food <= 0)
                    return Refuse("hungry", "食物已经用完。 ");
                if(input.ObjectId==null&&drinking&&UsesConsumableStock&&State.StationWaterPortions<=0)return Refuse("thirsty","清水库存已经用完。");
                if(supply!=null)supply.Portions--;
                else if(drinking&&UsesConsumableStock)State.StationWaterPortions--;
                else if (input.Action != InteractionAction.Drink && UsesConsumableStock) State.Food--;
                c.Fullness = Clamp100(c.Fullness + (input.Action == InteractionAction.FeedMeal ? 19 : input.Action == InteractionAction.FeedSweet ? 9 : 0));
                i.BeginIntake(drinking?IntakeKind.Water:input.Action==InteractionAction.FeedSweet?IntakeKind.Sweet:IntakeKind.Meal);
                if(!drinking&&i.Digestion.Symptomatic)i.Digestion.NextFoodAt=State.WorldSeconds+6;
                if(drinking)i.Hydration=Clamp100(i.Hydration+28);
                i.FoodSuspicion = Math.Max(0, i.FoodSuspicion - 2);
                ImproveAffection(c, input.Action == InteractionAction.FeedSweet ? .8 : .5);
                State.Progress.CareActions=Math.Min(int.MaxValue-1,State.Progress.CareActions+1);
                g.Applied = true;
                if(!i.Digestion.Symptomatic)LearnFoodExperience(c,drinking?RoomObjectKind.Water:
                    input.Action==InteractionAction.FeedSweet?RoomObjectKind.Sweet:RoomObjectKind.Meal,true);
                if(supply!=null)RegisterNaturalExposure(c,supply);
                else RememberRecentIntake(c,null,input.Action==InteractionAction.Drink?RoomObjectKind.Water:
                    input.Action==InteractionAction.FeedSweet?RoomObjectKind.Sweet:RoomObjectKind.Meal);
                return Say(input.Action == InteractionAction.FeedSweet ? "sweet" : input.Action == InteractionAction.Drink ? "drink" : "eat",
                    drinking?Expression.Content:Expression.Delighted, drinking?"它已经抿下一口水，等它咽下；收手不会撤销已经喝到的水。":"这一口已经接下，等它吃完再递下一份。", drinking?"sip":"eat");
            case InteractionAction.Clean:
            case InteractionAction.Dry:
                if (motion <= .0005) return g.Last;
                if(input.Part==BodyPart.Accessory)return g.Last=new(true,"这是饰品，取下后用“擦饰品”整理。",c.Speech,"accessory-watch");
                bool dry = input.Action == InteractionAction.Dry;
                if(c.Fear>68||c.Trust<12)return Refuse("touch-no","它还在躲开毛巾，先留一点距离，让它缓一缓。 ");
                if(input.Part==BodyPart.Head&&i.HeadSensitivity>45||input.Part==BodyPart.Cheek&&i.CheekSensitivityAt(input.Side)>45)
                    return Refuse("touch-sore","这一处还很敏感，先看看伤处，适用护理后再轻轻擦。 ");
                if (input.Part == BodyPart.Bottom && !i.Supine && !i.Carried) return g.Last = new(true, "先帮它翻身或托起，才能接触底部。", "", "invite");
                if (input.Part == BodyPart.Bottom && i.FootIntegrity < 35 && c.Pain > 30) return Refuse("touch-no", "碰到伤处会疼，先做适用护理。 ");
                double before = dry ? i.WetnessAt(input.Part,input.Side) : i.DirtAt(input.Part,input.Side);
                i.WipeSurface(input.Part,motion*(dry?16:18),dry,input.Side);
                double removed=before-(dry?i.WetnessAt(input.Part,input.Side):i.DirtAt(input.Part,input.Side));
                if(removed<=0)return g.Last=new(true,dry?"这里已经擦干了，可以看看别处还有没有湿。":"这里已经干净了，可以看看别处的污迹。",c.Speech,"wipe");
                g.Applied=true;ImproveAffection(c,removed/(dry?16:18)*.12);
                bool finished=(dry?i.WetnessAt(input.Part,input.Side):i.DirtAt(input.Part,input.Side))<=.0001;
                string cleaningVoice=dry?finished?input.Part==BodyPart.Bottom?"dry":input.Part is BodyPart.Cheek or BodyPart.Mouth?"dry-face":"dry-body":"drying":finished?"clean-done":"clean";
                return Say(cleaningVoice,Expression.Content,finished?dry?"这一处已经擦干，看看其他地方是否还湿着。":"这一处已经干净，其他污处还会保留。":dry?"毛巾带走这一处的湿润，伤势仍需单独护理。":"擦到的污处正在变干净。");
            case InteractionAction.CleanAccessory:
                if(input.Part!=BodyPart.Accessory)return g.Last=new(true,"把毛巾移到取下的饰品上。","","accessory-watch");
                if(i.Accessory.Place==AccessoryPlace.Stored)return g.Last=Unavailable("饰品已收起，先拿出来。 ");
                if (motion <= .0005) return g.Last;
                if(i.Accessory.Dirt<=0)return g.Last=new(true,"饰品已经擦干净了，可以检查破损或戴回去。",c.Speech,"accessory-watch");
                i.Accessory.Dirt = Math.Max(0, i.Accessory.Dirt - motion * 20); g.Applied = true;
                return Say(i.Accessory.Dirt<=0?"accessory-clean":"accessory-care", Expression.Curious,
                    i.Accessory.Dirt<=0?"污迹已经擦净。破损仍需要单独修补。":"它盯着手里的饰品，看着污处被擦净。", "accessory-watch");
            case InteractionAction.RepairAccessory:
                if(input.Part!=BodyPart.Accessory)return g.Last=new(true,"要接触取下的饰品，才能继续修补。","","accessory-watch");
                if(i.Accessory.Place==AccessoryPlace.Stored)return g.Last=Unavailable("饰品已收起，先拿出来。 ");
                if (g.RepairFinished||i.Accessory.Integrity>=100)
                {
                    if(!g.RepairFinished){g.RepairFinished=true;g.CompletionReleased=true;}
                    return g.Last = new(true, "这次修补已经结束，可以戴回；如需继续整理，先松开再操作。", c.Speech, InteractionPresentation.Pose(c));
                }
                double integrityBefore=i.Accessory.Integrity;
                i.Accessory.Integrity = Math.Min(100, i.Accessory.Integrity + dt * 9);
                i.Accessory.RepairProgress += (i.Accessory.Integrity-integrityBefore)/.75;
                g.Applied = true;
                if(i.Accessory.Integrity>=100)
                {
                    i.Accessory.CompletedRepairs=Math.Min(int.MaxValue-1,i.Accessory.CompletedRepairs)+1;g.RepairFinished=true;
                    g.Last=Feedback(c,"repair-done",Expression.Curious,"缝补完成了，针脚留在原来的饰品上。可以戴回去。","accessory-watch");
                    g.ReactionSerial=c.ReactionSerial;return g.Last;
                }
                return Say("repair", Expression.Curious, "修补正在进行，松开会保留已经完成的部分。", "accessory-watch");
            case InteractionAction.PlayBall:
                return g.Last = new(true, "拖出方向后松开，把球推过去。", "", "ball-aim");
            case InteractionAction.Care:
                if (g.Applied) return g.Last=g.Last with {Speech=c.Speech,Pose=InteractionPresentation.Pose(c),Changed=false};
                if(!CanCareAt(c,input.Part,input.Side))return g.Last=new(true,"这里不是需要处理的伤处。先看看，再把护理包移到相应部位。",c.Speech,"invite");
                if (g.ContactElapsed < 1.6) return g.Last = new(true, "正在处理伤处……保持接触，松开可中止。", c.Speech, "nursing");
                if (UsesConsumableStock && State.Medicine <= 0) return Refuse("inspect", "护理用品不足。 ");
                if(!ApplyBodyCare(c,input.Part,input.Side))return g.Last=Unavailable("刚做过护理，需要时间观察恢复；重复包扎不能加快愈合。");
                if (UsesConsumableStock) State.Medicine--;
                i.CareContinuity = Clamp100(i.CareContinuity + 5);
                if(i.Digestion.Symptomatic&&input.Part==BodyPart.Side)
                    i.Digestion.CareRemaining=Math.Max(i.Digestion.CareRemaining,60);
                ImproveAffection(c, .8); g.Applied = true;
                State.Progress.CareActions=Math.Min(int.MaxValue-1,State.Progress.CareActions+1);
                return Say("care", Expression.Shy, "护理完成。实际身体恢复需要普通时间、进食与休息。", "nursing");
            case InteractionAction.Block:
                c.TargetX = c.X; c.TargetZ = c.Z; i.WaitRemaining = .5; g.Applied = true;
                return Say("blocked", Expression.Defiant, "它前方被挡住，转头寻找其他方向。", "blocked");
            case InteractionAction.Tease:
                if(UsesConsumableStock&&State.Food<=0)return Refuse("hungry","手边的甜甜已经没有了，先收手补给。 ");
                if(c.Fullness>=98)return Refuse("full","它已经吃饱，对眼前的甜甜没有兴趣。 ");
                if(i.FoodSuspicion>=75)
                {c.TargetX=c.X;c.TargetZ=c.Z;return Refuse("suspicious-food","它停下追逐，不再相信这次递来的甜甜。 ");}
                double baitDistance=Distance(c.X,c.Z,input.X,input.Z);
                if(CanSelfMove(c)){c.TargetX=input.X;c.TargetZ=input.Z;}
                if(baitDistance<=.85)
                {g.BaitAnticipation+=dt;g.BaitReady|=g.BaitAnticipation>=.45;}
                else if(!g.BaitReady)g.BaitAnticipation=0;
                if(g.BaitReady&&baitDistance>1.15&&motion>.005)
                {
                    WithdrawInteractionBait(c,g);
                    return Say("teased",Expression.Defiant,"甜甜刚到嘴边又被移开，它扑了个空。这个落空会影响下次接食。","withdraw");
                }
                if(!CanSelfMove(c)&&baitDistance>.85)
                    return Say("tease-unreachable",Expression.Calling,"它看着甜甜，但身体做不到追过去。","help");
                return Say(g.BaitWithdrawals>0?"teased":"hungry",g.BaitWithdrawals>0?Expression.Defiant:Expression.Curious,
                    baitDistance<=.85?"甜甜到了嘴边，它还在等着；这个工具不会把食物喂进去。":"甜甜随手移动，它在看着还能不能够到。","reach");
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
                double rollX=input.X-c.X,rollZ=input.Z-c.Z,requested=Math.Sqrt(rollX*rollX+rollZ*rollZ);
                if(requested<=.00001)return g.Last;
                double fraction=Math.Min(1,motion/requested),oldX=c.X,oldZ=c.Z;
                GroundMove(c,c.X+rollX*fraction,c.Z+rollZ*fraction,false);c.TargetX=c.X;c.TargetZ=c.Z;
                double travelled=Distance(oldX,oldZ,c.X,c.Z);
                if(travelled<=.00001)return g.Last with {Message="前方没有翻滚空间，手停在这里。",Changed=false};
                i.MoveRoll(c.X-oldX,c.Z-oldZ,c.IsYoung?.73:c.IsElder?1.03:1);
                Distress(c,travelled*(1+force));i.SoilSurface(travelled*2);g.Applied=true;g.Effect+=travelled;
                return Say("roll", Expression.Frightened, "身体沿实际拖动的方向滚过地面；停手就会停下。", "roll");
            case InteractionAction.Hold:
                if(i.Restraint is not (RestraintKind.None or RestraintKind.Hand))return g.Last=Unavailable("这里已由其他装置限制，手不能代替解除装置。 ");
                i.Restraint=RestraintKind.Hand;
                g.Applied = true;
                return Say("restrained", Expression.Defiant, "它被手按住；松手即可解除这层限制。", "held");
            case InteractionAction.Burn:
                if(!validPart)return g.Last=new(true,"火机没有接触身体。",c.Speech,"tool-approach");
                double burn=dt*(1.2+force*5);
                Distress(c,burn*.7,burn,"灼烧",input.Part,input.Side);
                i.FireFear=Clamp100(i.FireFear+burn);
                i.HeatTension=Clamp100(i.HeatTension+dt*(20+force*40));
                if(CanSelfMove(c))
                {
                    double fleeX=c.X-input.X,fleeZ=c.Z-input.Z;
                    if(Math.Abs(fleeX)+Math.Abs(fleeZ)<.01){fleeX=c.X-State.PlayerX;fleeZ=c.Z-State.PlayerZ;}
                    if(Math.Abs(fleeX)+Math.Abs(fleeZ)<.01)fleeZ=-1;
                    RoomMove(c,c.X+fleeX,c.Z+fleeZ,dt*1.7);
                    c.Activity="躲开火机，尝试逃跑";
                }
                g.Applied=true;g.Effect+=burn;
                return Say("hurt",Expression.Hurt,CanSelfMove(c)?"它疼得缩起身体，正在逃离火机。":"它疼得身体绷紧，当前状态使它无法逃开。",
                    CanSelfMove(c)?"burn-flee":"burn-tense");
            case InteractionAction.BurnFeet:
                if (input.Part != BodyPart.Bottom) return g.Last = new(true, "工具还没有接触底部。", c.Speech, "tool-approach");
                double injury = dt * (2.5 + force * 10);
                ApplyLocalInjury(c,BodyPart.Bottom,SurfaceSide.Whole,injury,"底部受伤");
                i.FireFear = Clamp100(i.FireFear + injury * .6);
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
        if(g.Input.TargetKind==InteractionTargetKind.SupplyStation)return EndStationGesture(g);
        if(g.Input.TargetKind==InteractionTargetKind.DetachedStem)return EndDetachedStemGesture(g,cancelled);
        // Cleanup belongs to the object on which the gesture began, even if the view changed.
        var c = State.Creatures.Find(o=>o.Id==g.TargetId);
        if (c == null) return g.Last;
        if(g.Input.TargetKind==InteractionTargetKind.Stem)
        {
            if(c.ReactionSerial==g.ReactionSerial){c.Interaction.ReactionPose="idle";c.Interaction.ReactionPoseRemaining=0;}
            return g.Last with {Pose=InteractionPresentation.Pose(c),Changed=false};
        }
        if(c.Condition==PhysicalCondition.Dead)
        {NormalizeDeadInteractionSupport(c);cancelled=true;}
        // Finishing an old gesture is another action boundary. A pause, loss of
        // body ability or change of view must not release a stored push/throw.
        if(State.Settings.Pause||c.Family.ColdStorage.Stored||c.Condition==PhysicalCondition.Critical||
            c.RegionId!=State.ActiveRegionId||State.Mode==PlayMode.CloseUp&&State.SelectedId!=c.Id)cancelled=true;
        var i = c.Interaction;
        if(g.Input.Action==InteractionAction.Roll)i.EndRoll();
        if(g.Input.Action==InteractionAction.Tease)
        {
            c.TargetX=c.X;c.TargetZ=c.Z;
            if(!cancelled&&g.BaitReady)
            {
                WithdrawInteractionBait(c,g);
                g.Last=Feedback(c,"teased",Expression.Defiant,"嘴边的甜甜被收走了，它望着空下来的地方。","withdraw");
            }
        }
        if (g.Input.Action == InteractionAction.Hold && i.Restraint == RestraintKind.Hand) i.Restraint = RestraintKind.None;
        if (!cancelled && g.ContactActive && g.Input.Action == InteractionAction.Push && g.Motion > .04)
        {
            Impulse(c, g.Input.DirectionX, g.Input.DirectionZ, .6 + g.Input.Force * 1.8);
            Distress(c, 1 + g.Input.Force * 2); g.Applied = true;
            g.Last = Feedback(c, "angry", Expression.Defiant, "被向一旁推开，它稳住身体再看向你。", "push");
        }
        if (!cancelled && g.Input.Action == InteractionAction.Throw && i.Carried && g.Motion > .04)
        {
            i.Lowering=false;
            i.Carried = false; i.Falling = true; i.VerticalVelocity = 1.5 + g.Input.Force * 2;
            Impulse(c, g.Input.DirectionX, g.Input.DirectionZ, 1 + g.Input.Force * 2.5);
            g.Applied = true;
            g.Last = Feedback(c, "fall", Expression.Frightened, "它离开手的支撑，沿着选定方向抛出。", "fall");
        }
        if (!cancelled && g.Input.Action == InteractionAction.PlayBall && g.Motion > .03)
        {
            var ball = State.RoomObjects.FirstOrDefault(o => o.Id==g.ObjectId&&o.RegionId==c.RegionId&&o.Kind == RoomObjectKind.Ball && !o.Stored);
            if (ball != null)
            {
                double length = Math.Sqrt(g.Input.DirectionX * g.Input.DirectionX + g.Input.DirectionZ * g.Input.DirectionZ);
                if (length > .001)
                {
                    ball.VelocityX = g.Input.DirectionX / length * Math.Min(3.5, .8 + g.Motion * 2);
                    ball.VelocityZ = g.Input.DirectionZ / length * Math.Min(3.5, .8 + g.Motion * 2);
                    g.Applied = true; g.Last = new(true, RoomBallEnvironment.InWater(State,ball)?
                        g.Input.DirectionX<-.1?"把球朝岸边推过去了，它留在岸上看。":"球在水面上漂动，它没有跟下去。":
                        "球滚出去了，看看它怎样接。", "", "play", Changed: true);
                }
            }
        }
        if (g.Applied)
        {
            c.ActionCounts[g.Input.Action.ToString()] = c.ActionCounts.GetValueOrDefault(g.Input.Action.ToString()) + 1;
            string key = g.RoughTouch?"rough-touch":g.Input.Action switch
            {
                InteractionAction.Stroke or InteractionAction.Touch => "stroke", InteractionAction.Nuzzle => "nuzzle",
                InteractionAction.Tease => "teasing", InteractionAction.BurnFeet or InteractionAction.Burn => "fire",
                InteractionAction.Care => "nursing", _ => g.Input.Action.ToString()
            };
            string description = g.StopImpact?"手快速移动后突然停在身上，受到了一下冲击。":g.RoughTouch?(g.Input.Action==InteractionAction.Rock?"被过急或过重地摇动，扭动着要求停下。":"本想轻轻接触，却被过重或过急地揉蹭，缩开了身体。"):g.Input.Action switch
            {
                InteractionAction.Touch => "手沿着身体轻轻摸过，记住了这次接触。",
                InteractionAction.Stroke => "被顺着头顶慢慢摸过。", InteractionAction.Nuzzle => "和手指贴着脸蹭蹭。",
                InteractionAction.Tease => g.BaitWithdrawals>1?"期待的甜甜被反复移开。":"眼前的甜甜被移开，没有吃到。", InteractionAction.BurnFeet => "底部受伤，记住了这件工具。",
                InteractionAction.Care => "伤处被认真护理，还需要休息。",
                InteractionAction.RepairAccessory=>g.RepairFinished?"原来的饰品被缝补好了，留下了细细的针脚。":"饰品被耐心修补了一部分，还能继续补好。",
                _ => $"经历了{InteractionCatalog.Choice(g.Input.Action).Label}。"
            };
            RecordInteraction(c, key, description, Math.Max(.3, Math.Min(8, g.Effect)));
        }
        bool releasedImpulse=!cancelled&&g.Applied&&g.Input.Action is InteractionAction.Push or InteractionAction.Throw or InteractionAction.Tease;
        if(!releasedImpulse&&!g.Refused&&c.ReactionSerial==g.ReactionSerial)
        {i.ReactionPose="idle";i.ReactionPoseRemaining=0;}
        string message=g.Last.Message;
        if(g.Applied&&c.Condition!=PhysicalCondition.Dead)
            message=g.Input.Action switch
            {
                InteractionAction.Hold=>i.Restraint!=RestraintKind.None?"手已收回，原有装置仍需要单独解除。":CanSelfMove(c)?"手的限制已经解除，可以观察它自己的动作。":"手的限制已经解除，但身体现在仍无法自行走动。",
                InteractionAction.Press or InteractionAction.Squeeze=>"已经收手，持续施力停止。已有的不适和伤势仍会保留。",
                InteractionAction.Roll=>"已经收手，滚动停止；接下来观察它能否自己恢复姿势。",
                InteractionAction.Block=>"手已经让开，给它一点时间决定往哪里走。",
                InteractionAction.BurnFeet=>UsesGroundFoot(c)&&i.FootIntegrity<=25?"工具已移开，作用已经停止。底部仍不能支撑行走，需要辅助进食和护理。":"工具已移开，作用已经停止；已有伤势仍需观察和护理。",
                InteractionAction.Clean or InteractionAction.Dry or InteractionAction.CleanAccessory=>"毛巾已经移开，擦过的部分会保留；其余污处或湿处需要继续照料。",
                _=>message
            };
        if(cancelled&&g.Input.Action is InteractionAction.Push or InteractionAction.Throw or InteractionAction.PlayBall)
            message=i.Carried?"这次动作已取消，仍然稳稳托着；选择放稳来结束托举。":"这次动作已取消，手已经收回。";
        return g.Last with { Message=message,Speech=c.Speech,Pose = InteractionPresentation.Pose(c) };
    }

    private void WithdrawInteractionBait(Creature c,ContactGesture gesture)
    {
        c.Interaction.FoodSuspicion=Clamp100(c.Interaction.FoodSuspicion+6);
        Distress(c,.8);gesture.Applied=true;gesture.Effect+=1;
        gesture.BaitReady=false;gesture.BaitAnticipation=0;gesture.BaitWithdrawals++;
    }

    private static bool NeedsInteractionCare(Creature c) => c.Condition==PhysicalCondition.Critical||
        c.Interaction.Injuries.CareCooldown<=0&&(c.Health<95||c.Pain>5)||
        Enum.GetValues<BodyPart>().Any(part=>NeedsLocalBodyCare(c,part,SurfaceSide.Left)||NeedsLocalBodyCare(c,part,SurfaceSide.Right));
    private static bool CanCareAt(Creature c,BodyPart part,SurfaceSide side)=>part!=BodyPart.Accessory&&
        (c.Condition==PhysicalCondition.Critical||NeedsLocalBodyCare(c,part,side)||
         c.Interaction.Injuries.CareCooldown<=0&&!c.Interaction.Injuries.HasActiveWounds&&(c.Health<95||c.Pain>5));

    public static string PoseFor(InteractionAction action) => action switch
    {
        InteractionAction.Stroke or InteractionAction.Touch => "stroke", InteractionAction.Nuzzle => "nuzzle", InteractionAction.Lift => "carry",
        InteractionAction.Rock => "rock", InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Drink => "offer",
        InteractionAction.Clean or InteractionAction.Dry => "wipe", InteractionAction.Care => "nursing",InteractionAction.SupportStem=>"stem-care",
        InteractionAction.Press => "pressure", InteractionAction.Squeeze => "squeeze", InteractionAction.Roll => "roll",
        InteractionAction.BurnFeet => "foot-hurt", InteractionAction.Burn=>"burn-tense", InteractionAction.Punch=>"slap", InteractionAction.Tease => "reach", _ => "idle"
    };
}
