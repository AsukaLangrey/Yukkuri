namespace Yukkuri.Simulation;

public sealed record CreatureArrival(string SpeciesId,bool Young=false,
    ReproductionRole Role=ReproductionRole.Maternal,string? Name=null);

public sealed partial class WorldSimulation
{
    public const int NewCreaturePrice=30;
    public const int ArrivalPopulationLimit=200;

    // Read-only: previews and rejected purchases must not advance the world RNG.
    public string? NewCreaturePlacementReason(CreatureArrival profile,double x,double z)
    {
        if(!State.InteractionPlaytest)return "请在相处小院投放新油。";
        if(State.Settings.Pause||State.Mode==PlayMode.CloseUp)return "先恢复小院时间并回到场景，再投放新油。";
        if(HasInteractionGesture)return "先结束当前接触，再投放新油。";
        if(profile.SpeciesId is not ("reimu" or "marisa")||!Enum.IsDefined(profile.Role))return "请选择已有的油种和繁育角色。";
        if(profile.Name is { } name&&(name.Length>24||name.Any(char.IsControl)))return "名字请使用24字以内的单行文字。";
        if(!State.Regions.Any(r=>r.Id==State.ActiveRegionId)||State.ActiveRegionId=="courtyard-container")return "这里不能投放新油。";
        if(State.Creatures.Count>=10000||State.Creatures.Count(c=>c.RegionId==State.ActiveRegionId&&!c.Family.Memorialized)>=ArrivalPopulationLimit)
            return "当前场景的个体数量已达上限。";
        if(State.Money<NewCreaturePrice)return $"余额不足，需要 {NewCreaturePrice} 元。";
        if(!double.IsFinite(x)||!double.IsFinite(z))return "请选择有效的地面落点。";
        var candidate=ArrivalCreature(profile,x,z);
        double radius=InteractionFootprint(candidate);
        if(!CanOccupyRoom(candidate,x,z)||State.Shelters.Any(s=>s.RegionId==candidate.RegionId&&
            ShelterGeometry.OverlapsInterior(x-s.X,z-s.Z,radius))||
            State.RoomObjects.Any(o=>o.RegionId==candidate.RegionId&&!o.Stored&&!o.HeldByHand&&o.Kind!=RoomObjectKind.RiverWater&&
                Distance(x,z,o.X,o.Z)<radius+(o.Kind==RoomObjectKind.Cushion?1.3:StationObjectRadius(o.Kind))))
            return "这里放不下，请避开水面、身体、物品和小家，选择干燥空地。";
        return null;
    }

    private Creature ArrivalCreature(CreatureArrival profile,double x,double z)=>new()
    {
        Name=string.IsNullOrWhiteSpace(profile.Name)?(profile.SpeciesId=="marisa"?"新来的魔理沙":"新来的灵梦"):profile.Name.Trim(),
        RegionId=State.ActiveRegionId,X=x,Z=z,TargetX=x,TargetZ=z,IsYoung=profile.Young,
        Trust=20,Attachment=0,Fullness=75,Energy=85,Activity="初来乍到，正在观察小院",
        Body=new(){SpeciesId=profile.SpeciesId,ReproductionRole=profile.Role,AgeSeconds=profile.Young?0:FamilyRules.YoungSeconds},
        Interaction=new(){Enabled=true,Hydration=80,NextAutonomyAt=State.WorldSeconds+3,
            Accessory=new(){X=x,Z=z}},
        Family=new(){NextSocialAt=State.WorldSeconds+6,NextBreedingDecisionAt=State.WorldSeconds+120}
    };

    public InteractionFeedback PlaceNewCreature(CreatureArrival profile,double x,double z)
    {
        if(NewCreaturePlacementReason(profile,x,z) is { } reason)return Unavailable(reason);
        var c=ArrivalCreature(profile,x,z);
        c.TrueDisposition=new[]{"curious","shy","proud"}[(int)(Next()*3)];
        InitializePersonality(c);
        State.Money-=NewCreaturePrice;State.Creatures.Add(c);State.SelectedId=c.Id;
        State.LastActionMessage=$"{c.Name}已来到小院，花费 {NewCreaturePrice} 元。先打个招呼，慢慢熟悉。";
        Log(State.LastActionMessage);
        return new(true,State.LastActionMessage,"","idle",Changed:true);
    }
}
