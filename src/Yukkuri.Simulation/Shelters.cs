namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    public const int ShelterPrice=45;
    private ShelterState? Shelter(string id)=>State.Shelters.Find(s=>s.Id==id);
    private string? ShelterWorldError()=>!State.InteractionPlaytest||!State.Regions.Any(r=>r.Id==State.ActiveRegionId)?"先回到有效的小院。":
        State.Settings.Pause?"先恢复时间，再操作住居。":State.Mode==PlayMode.CloseUp?"先退出特写，再操作住居。":null;
    private Creature? ValidShelterGuest(ShelterState s)=>State.Creatures.Find(c=>c.Id==s.GuestId&&c.RegionId==s.RegionId&&
        c.Condition!=PhysicalCondition.Dead&&!c.Family.ColdStorage.Stored&&!c.Family.Memorialized&&!c.Interaction.Carried&&!c.Interaction.Falling);
    private string? ShelterDoorError(ShelterState s,bool open)=>ShelterWorldError()??(s.RegionId!=State.ActiveRegionId?"住居不在当前小院。":
        s.DoorOpen==open?open?"门已经打开。":"门已经关闭。":!open&&!ShelterDoorClear(s)?"门槛上有身体或实际物件，不能穿过去关门。":null);
    private string? ShelterCallError(ShelterState s)
    {
        if(ShelterWorldError() is { } error)return error;
        if(s.RegionId!=State.ActiveRegionId)return "住居不在当前小院。";
        var c=ValidShelterGuest(s);if(c==null||!ShelterBodyPresent(c,s))return "屋内没有可以回应的住户。";
        if(!s.DoorOpen)return "门还关着，先打开真实出口。";
        if(!CanSelfMove(c))return "它现在不能自己走出来，需要实际护理或经门洞低托救出。";
        if(c.Interaction.EatingRemaining>0||FamilyRules.UnderIntervention(c)||c.Interaction.ActiveCue!=null)
            return "它正在完成身体动作，等结束后再邀请。";
        if(c.Trust<35||c.Fear>=55||c.Interaction.HumanBond.LastHarmAt>=0&&State.WorldSeconds-c.Interaction.HumanBond.LastHarmAt<120)
            return "它听见了，但仍不够信任或还记着刚才的伤害，暂时不愿离开安全处。";
        return s.ExitRequested?"它已经准备出来，给真实步行留一点时间。":null;
    }
    public bool BuyShelter()
    {
        if(!State.InteractionPlaytest||State.ShelterStock>=99||!State.UnlimitedResources&&State.Money<ShelterPrice)return false;
        if(!State.UnlimitedResources)State.Money-=ShelterPrice;State.ShelterStock++;Log("购入一间空住居，放置时需留出门前通路。");return true;
    }
    public InteractionFeedback PlaceShelter(double x,double z)
    {
        if(ShelterWorldError() is { } error)return Unavailable(error);
        if(State.ShelterStock<=0||StationHandOccupied())return Unavailable("先购买空住居，并放下手中的实际对象。");
        if(!CanPlaceShelter(State.ActiveRegionId,x,z))return Unavailable("屋体和门前通路需要连续干燥空地，不能盖住身体或已有物件。");
        long sequence=State.ShelterSequence;string id;
        do{if(sequence==long.MaxValue)return Unavailable("住居身份序号已用尽。");id="shelter-"+(++sequence).ToString(System.Globalization.CultureInfo.InvariantCulture);}
        while(StationIdentityUsed(id)||State.Shelters.Any(s=>s.Id==id));
        State.ShelterSequence=sequence;State.Shelters.Add(new(){Id=id,RegionId=State.ActiveRegionId,X=x,Z=z});State.ShelterStock--;
        return new(true,"住居已放好，门朝前打开；它需要自己沿门口走进去。","","shelter-place",Changed:true);
    }
    public ShelterView? ShelterStatus(string id)=>Shelter(id) is { } s?DescribeShelter(s):null;
    public IReadOnlyList<ShelterView> Shelters(string? regionId=null)=>State.Shelters.Where(s=>regionId==null||s.RegionId==regionId).Select(DescribeShelter).ToArray();
    private ShelterView DescribeShelter(ShelterState s)
    {
        var guest=ValidShelterGuest(s);var physical=PhysicalShelterGuest(s);
        var phase=guest==null?ShelterVisitPhase.Empty:s.Phase;
        string? reason=guest!=null?ShelterExitBlockReason(s,guest):null;
        return new(s.Id,s.RegionId,s.X,s.Z,s.DoorOpen,phase,guest!=null&&phase is ShelterVisitPhase.Approaching or ShelterVisitPhase.Entering?guest.Id:null,
            physical?.Id,guest!=null&&s.ExitRequested,guest!=null?s.BlockedSeconds:0,ShelterDoorError(s,true)==null,
            ShelterDoorError(s,false)==null,ShelterCallError(s)==null,reason??ShelterWorldError(),s.LastEvent);
    }
    public CreatureShelterView? CreatureShelterStatus(Creature c)
    {
        if(!State.Creatures.Contains(c)||c.Condition==PhysicalCondition.Dead||c.Family.ColdStorage.Stored||c.Family.Memorialized)return null;
        var s=State.Shelters.Find(s=>s.GuestId==c.Id&&ValidShelterGuest(s)==c);if(s==null)return null;
        string? reason=ShelterExitBlockReason(s,c);
        return new(s.Id,s.Phase,s.ExitRequested,reason!=null||s.BlockedSeconds>=1,reason);
    }
    public InteractionFeedback ExecuteShelterInteraction(string id,InteractionInput input)=>ExecuteInteraction(id,
        input with {TargetKind=InteractionTargetKind.Shelter,ObjectId=input.ObjectId??id});
    private InteractionFeedback ExecuteShelterCore(string id,InteractionInput input)
    {
        if(ShelterWorldError() is { } error)return Unavailable(error);
        var s=Shelter(id);
        if(s==null||s.RegionId!=State.ActiveRegionId||input.ObjectId!=id||input.TargetKind!=InteractionTargetKind.Shelter||input.Part!=BodyPart.Side||
            input.SupplyObjectId!=null||!new[]{input.X,input.Z,input.Height,input.Force,input.Duration,input.Motion}.All(double.IsFinite)||
            input.Height<0||input.Height>ShelterGeometry.CeilingY+.12||Math.Abs(input.X-s.X)>ShelterGeometry.OuterWidth*.5+.12||
            Math.Abs(input.Z-s.Z)>ShelterGeometry.OuterDepth*.5+.12)
            return Unavailable("请点击当前住居的真实表面。");
        if(input.Action is InteractionAction.OpenShelterDoor or InteractionAction.CloseShelterDoor)
        {
            if(!ShelterGeometry.DoorControlHit(input.X-s.X,input.Height,input.Z-s.Z))return Unavailable("请点击前门帘或门边的拉带。");
            bool open=input.Action==InteractionAction.OpenShelterDoor;
            if(ShelterDoorError(s,open) is { } refused)return Unavailable(refused);
            s.DoorOpen=open;s.LastEvent=open?"门已打开，住户仍需实际走过门槛。":"门已关闭；住户的位置和情绪没有改变。";
            return new(true,s.LastEvent,"","shelter-door",Changed:true);
        }
        if(input.Action==InteractionAction.CallFromShelter)
        {
            if(ShelterCallError(s) is { } refused)return Unavailable(refused);
            var c=ValidShelterGuest(s)!;s.ExitRequested=true;c.Interaction.ShelterSearchAfter=State.WorldSeconds+30;
            s.LastEvent="它回应了呼唤，准备沿门口慢慢走出来。";
            return Feedback(c,"shelter-call",EmotionalReadiness.Settled(c)?Expression.Curious:Expression.Worried,s.LastEvent,"idle",false);
        }
        return Unavailable("住居只接受真实门控或呼唤工具，不能当作身体或食水来源。");
    }
}
