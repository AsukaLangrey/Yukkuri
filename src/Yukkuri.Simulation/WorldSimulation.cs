namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    public WorldState State { get; }
    public SimulationConfig Config { get; }
    public WorldSimulation(WorldState state, SimulationConfig? config = null)
    {
        State = state; Config = state.InteractionPlaytest ? new() { HungerPerSecond = .025, EnergyPerSecond = .02, MoveSpeed = .72 } : state.MilestoneScenario ? MilestoneConfig() : config ?? new();
        if(state.InteractionPlaytest)
            foreach(var c in state.Creatures)
            {
                if(c.Interaction.Restraint==RestraintKind.Hand)c.Interaction.Restraint=RestraintKind.None;
                NormalizeDeadInteractionSupport(c);
            }
    }
    public Creature? Selected => State.Creatures.Find(c => c.Id == State.SelectedId);
    public RegionState ActiveRegion => State.Regions.Single(r => r.Id == State.ActiveRegionId);
    public CreatureView View(Creature c) => new(c.Id, c.Name, c.Body.Id, c.RegionId, c.Fullness, c.Energy, c.Health,
        c.Activity, c.Observation.Hypothesis, c.Observation.Confidence, c.Quarantine);

    public static WorldSimulation Create(int population = 10, bool sandbox = false, SimulationConfig? config = null)
    {
        if (population is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(population));
        var state = new WorldState { UnlimitedResources = sandbox, Regions = [
            new() { Id = "home", Name = "油库里乐园", Kind = RegionKind.Sanctuary, StaffEnabled = true,
                Facilities = [new() { Kind = FacilityKind.Feeder, X = -4, Z = -2, Stock = 12 }, new() { Kind = FacilityKind.Bed, X = 4, Z = -2 }] },
            new() { Id = "wild", Name = "林缘野地", Kind = RegionKind.Wilderness }
        ]};
        var sim = new WorldSimulation(state, config);
        for (int i = 0; i < population + 6; i++)
        {
            var c = new Creature { Name = i < population ? $"团子 {i + 1:00}" : $"野生 {i - population + 1:00}",
                RegionId = i < population ? "home" : "wild", X = sim.Next() * 12 - 6, Z = sim.Next() * 12 - 6,
                Fullness = 40 + sim.Next() * 45, Energy = 50 + sim.Next() * 40,
                TrueDisposition = new[] { "curious", "shy", "greedy" }[i % 3],
                Body = new() { ReproductionRole = i % 2 == 0 ? ReproductionRole.Maternal : ReproductionRole.Paternal } };
            c.TargetX = c.X; c.TargetZ = c.Z;
            if (i < population) c.Observation.Notes.Add("养护日记：已登记入住；性格仍待观察。");
            state.Creatures.Add(c);
        }
        state.SelectedId = state.Creatures[0].Id;
        sim.Log("乐园开始营业。观察、照护和外出探索会留下记录。");
        return sim;
    }
    public double Next()
    {
        ulong x = State.RandomState;
        x ^= x << 13; x ^= x >> 7; x ^= x << 17;
        State.RandomState = x;
        return (x >> 11) * (1.0 / (1UL << 53));
    }
    public void Log(string message)
    {
        State.Journal.Add($"[{FormatClock(State.WorldSeconds)}] {message}");
        if (State.Journal.Count > 80) State.Journal.RemoveAt(0);
    }
    public static string FormatClock(double seconds)=>$"{(long)(seconds/60):00}:{(int)(seconds%60):00}";
    public void Advance(double delta)
    {
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        if (State.Mode == PlayMode.CloseUp || State.Settings.Pause) return;
        State.TickRemainder += delta;
        while (State.TickRemainder + 1e-9 >= Config.TickSeconds)
        {
            State.TickRemainder = Math.Max(0, State.TickRemainder - Config.TickSeconds);
            Tick(Config.TickSeconds);
        }
    }
    private void Tick(double dt)
    {
        State.WorldSeconds += dt;
        if (State.InteractionPlaytest)
        {
            SynchronizeCareParcels();
            TickInteractionWorld(dt);TickPhysiology(dt);TickFamilies(dt);TickFamilySocial(dt);
            TickAutonomousBreeding();SynchronizeCareParcels();TickColdStorage(dt);return;
        }
        if(State.MilestoneScenario) TickMilestoneWorld(dt);
        // An autonomous arrival/retreat may move the selected creature to another region.
        // Keep the authoritative state saveable before any UI refresh or autosave runs.
        if(Selected is { } selected&&selected.RegionId!=State.ActiveRegionId)
            State.SelectedId=State.Creatures.FirstOrDefault(c=>c.RegionId==State.ActiveRegionId)?.Id;
        foreach (var region in State.Regions)
        {
            var residents = State.Creatures.Where(c => c.RegionId == region.Id).ToArray();
            foreach (var c in residents)
            {
                if(State.MilestoneScenario && c.Condition==PhysicalCondition.Dead) continue;
                c.InnerAgeSeconds += dt; c.Body.AgeSeconds += dt;
                c.Fullness = Math.Max(0, c.Fullness - Config.HungerPerSecond * dt);
                c.Energy = Math.Max(0, c.Energy - Config.EnergyPerSecond * dt);
                if (c.Fullness <= 0)
                {
                    if(State.MilestoneScenario) Hurt(c,dt*.15,"饥饿",null);
                    else c.Health = Math.Max(1, c.Health - dt * 0.15);
                }
                if(State.MilestoneScenario && TickMilestoneCreature(c,region,dt)) continue;
                c.DecisionIn -= dt;
                if (c.DecisionIn <= 0)
                {
                    c.DecisionIn = 2 + Next() * 3;
                    var feeder = region.Facilities.Where(f => f.Kind == FacilityKind.Feeder && f.Stock > 0).MinBy(f => Distance(c.X, c.Z, f.X, f.Z));
                    var bed = region.Facilities.Where(f => f.Kind == FacilityKind.Bed).MinBy(f => Distance(c.X, c.Z, f.X, f.Z));
                    if (c.Fullness < 65 && feeder != null) { c.TargetX = feeder.X; c.TargetZ = feeder.Z; c.Activity = "寻找食物"; }
                    else if (c.Energy < 40 && bed != null) { c.TargetX = bed.X; c.TargetZ = bed.Z; c.Activity = "寻找睡床"; }
                    else if (c.Energy < 25) { c.TargetX = c.X; c.TargetZ = c.Z; c.Activity = "原地休息"; }
                    else { c.TargetX = Next() * 16 - 8; c.TargetZ = Next() * 16 - 8; c.Activity = region.Kind == RegionKind.Wilderness ? "野外觅食" : "闲逛"; }
                }
                if(c.Quarantine)
                {
                    c.TargetX=Math.Clamp(c.TargetX,-7,-4.5);c.TargetZ=Math.Clamp(c.TargetZ,4.2,6.6);
                    c.Activity="隔离观察";
                }
                double distance = Distance(c.X, c.Z, c.TargetX, c.TargetZ);
                if (distance > 0.15)
                {
                    double mobility=State.MilestoneScenario?(CanSelfMove(c)?UsesGroundFoot(c)?Math.Clamp(c.Interaction.FootIntegrity/100,.3,1):1:0):1;
                    double step = Math.Min(distance, Config.MoveSpeed * mobility * dt);
                    c.X += (c.TargetX - c.X) / distance * step; c.Z += (c.TargetZ - c.Z) / distance * step;
                    if(mobility==0)c.Activity="身体无法自行走动，需要辅助生活";
                }
                else
                {
                    var food = region.Facilities.FirstOrDefault(f => f.Kind == FacilityKind.Feeder && f.Stock > 0 && Distance(c.X,c.Z,f.X,f.Z)<0.9);
                    if (food != null && c.Fullness < 75) { food.Stock--; c.Fullness = Math.Min(100,c.Fullness+Config.FeedGain); c.Activity="进食"; }
                    if (c.Activity is "寻找睡床" or "原地休息" or "休息") { c.Energy = Math.Min(100,c.Energy+dt*6); c.Activity="休息"; }
                    if (region.Kind == RegionKind.Wilderness && c.Fullness < 70 && Next() < dt * 0.08) { c.Fullness += 10; c.Activity="找到了野果"; }
                }
            }
            // Deterministic local separation for the prototype's <= 100 residents.
            // A spatial index can replace this pair pass when navigation becomes richer.
            for(int pass=0;pass<6;pass++)
            for(int i=0;i<residents.Length;i++)
            for(int j=i+1;j<residents.Length;j++)
            {
                var a=residents[i];var b=residents[j];
                if(State.MilestoneScenario && (a.Condition==PhysicalCondition.Dead || b.Condition==PhysicalCondition.Dead))continue;
                if(a.Quarantine!=b.Quarantine)continue;
                double dx=b.X-a.X,dz=b.Z-a.Z,dist=Math.Sqrt(dx*dx+dz*dz);
                if(dist>=1.05)continue;
                double overlap=1.05-dist;
                if(dist<0.0001){double angle=(i*31+j*17)*2.39996;dx=Math.Cos(angle);dz=Math.Sin(angle);dist=1;}
                double push=Math.Min(.4,overlap*.5);
                a.X-=dx/dist*push;a.Z-=dz/dist*push;b.X+=dx/dist*push;b.Z+=dz/dist*push;
                ClampResident(a);ClampResident(b);
            }
            region.StaffCooldown -= dt;
            if (region.StaffEnabled && region.StaffCooldown <= 0 && (State.Money > 0 || State.UnlimitedResources))
            {
                region.StaffCooldown = 8;
                var hungry = residents.Where(c => !c.IsIntruder && c.Fullness < 35 && c.Condition is PhysicalCondition.Active or PhysicalCondition.Recovering).MinBy(c => c.Fullness);
                if (hungry != null && State.Food > 0) Feed(hungry.Id, true);
                var injured = residents.Where(c => !c.IsIntruder && c.Condition!=PhysicalCondition.Dead&&
                    (State.MilestoneScenario?NeedsInteractionCare(c):c.Health<80)).MinBy(c => c.Health);
                if (injured != null)
                {
                    if(State.MilestoneScenario) StaffCare(injured);
                    else { injured.Health = Math.Min(80, injured.Health + 2); injured.Activity = "接受基础照护"; }
                }
            }
        }
        State.WageTimer += dt;
        if (State.WageTimer >= 60)
        {
            State.WageTimer -= 60;
            int wages = State.Regions.Count(r => r.StaffEnabled);
            if (!State.UnlimitedResources) State.Money = Math.Max(0, State.Money - wages);
        }
    }
    public static double Distance(double x, double z, double x2, double z2) => Math.Sqrt((x-x2)*(x-x2)+(z-z2)*(z-z2));
    private static void ClampResident(Creature c)
    {c.X=Math.Clamp(c.X,c.Quarantine?-7:-8,c.Quarantine?-4.5:8);c.Z=Math.Clamp(c.Z,c.Quarantine?4.2:-8,c.Quarantine?6.6:8);}
    public bool Select(string id)
    {
        if (!State.Creatures.Any(c=>c.Id==id && c.RegionId==State.ActiveRegionId)) return false;
        State.SelectedId=id; return true;
    }
    public bool EnterCloseUp()
    {
        if (Selected == null || Selected.RegionId != State.ActiveRegionId) return false;
        State.Mode = PlayMode.CloseUp; return true;
    }
    public void ExitCloseUp() => State.Mode = PlayMode.Management;
    public bool Travel(string id)
    {
        if (State.Mode==PlayMode.CloseUp || !State.Regions.Any(r=>r.Id==id)) return false;
        State.ActiveRegionId=id; State.SelectedId=State.Creatures.FirstOrDefault(c=>c.RegionId==id)?.Id;
        State.PlayerX=0; State.PlayerZ=4; Log($"前往{ActiveRegion.Name}。"); return true;
    }
    public bool Feed(string id, bool staff = false)
    {
        var c = State.Creatures.Find(c=>c.Id==id);
        if (c==null || c.Condition is PhysicalCondition.Critical or PhysicalCondition.Dead || (!staff && c.RegionId!=State.ActiveRegionId) || (!State.UnlimitedResources && State.Food<=0)) return false;
        if (!State.UnlimitedResources) State.Food--;
        c.Fullness=Math.Min(100,c.Fullness+Config.FeedGain); c.Trust=Math.Min(100,c.Trust+2); c.Activity=c.Condition==PhysicalCondition.Recovering?"休养中接受喂食":"接受喂食";
        if (!staff)
        {
            c.Observation.FeedingEvidence++;
            AddNote(c, c.Condition==PhysicalCondition.Recovering ? "休养期间接受喂食，仍留在原地恢复。" : c.TrueDisposition=="greedy" ? "接近食物很快，还在寻找下一份。" : c.TrueDisposition=="shy" ? "接近食物前先看了看周围。" : "吃完后继续探索周围。");
            c.Memories.Add("被玩家喂食"); if (c.Memories.Count>20) c.Memories.RemoveAt(0);
        }
        if(State.MilestoneScenario) OnMilestoneFeed(c,staff);
        Log($"{(staff?"员工":"你")}喂食了{c.Name}。"); return true;
    }
    public bool Observe()
    {
        var c=Selected; if(c==null) return false;
        var o=c.Observation; o.Samples++; State.ObservationSkill=Math.Min(10,State.ObservationSkill+0.05);
        AddNote(c,$"观察到：{c.Activity}；饱腹约{c.Fullness:0}。");
        if(o.FeedingEvidence>=2)
        {
            o.Confidence=Math.Min(0.95,0.5+o.FeedingEvidence*0.08+State.ObservationSkill*0.01);
            o.Hypothesis=c.TrueDisposition switch { "greedy"=>"多次进食反馈：可能偏贪食", "shy"=>"多次进食反馈：可能偏谨慎", _=>"多次进食反馈：可能好奇" };
        }
        else { o.Confidence=0.2; o.Hypothesis="暂时判断：可能谨慎（证据不足，可修正）"; }
        Log($"记录了{c.Name}的行为，观察经验增加。"); return true;
    }
    private static void AddNote(Creature c,string note) { c.Observation.Notes.Add(note); if(c.Observation.Notes.Count>16)c.Observation.Notes.RemoveAt(0); }
    public bool Rescue()
    {
        if(State.MilestoneScenario) return InviteToSanctuary().Success;
        var c=Selected;
        if(c==null || c.RegionId=="home" || State.Mode==PlayMode.CloseUp) return false;
        c.RegionId="home"; c.Quarantine=true; c.X=-6; c.Z=5; c.TargetX=c.X; c.TargetZ=c.Z;
        c.Activity="隔离观察"; Log($"{c.Name}已安置到乐园观察区；接纳由玩家决定。");
        State.SelectedId=State.Creatures.FirstOrDefault(x=>x.RegionId==State.ActiveRegionId)?.Id; return true;
    }
    public bool Place(FacilityKind kind,double x,double z)
    {
        if(State.Mode!=PlayMode.Management || ActiveRegion.Kind!=RegionKind.Sanctuary || !double.IsFinite(x) || !double.IsFinite(z) || Math.Abs(x)>8 || Math.Abs(z)>8) return false;
        x=Math.Round(x);z=Math.Round(z);
        if(ActiveRegion.Facilities.Any(f=>Distance(x,z,f.X,f.Z)<2)) return false;
        int cost=kind==FacilityKind.Feeder?Config.FeederCost:Config.BedCost;
        if(!State.UnlimitedResources && State.Money<cost)return false;
        if(!State.UnlimitedResources)State.Money-=cost;
        ActiveRegion.Facilities.Add(new(){Kind=kind,X=x,Z=z});
        Log($"建造了{(kind==FacilityKind.Feeder?"食盆":"睡床")}，花费 {cost}。"); return true;
    }
    public bool BuyFood()
    {
        if(!State.UnlimitedResources && State.Money<10)return false;
        if(!State.UnlimitedResources)State.Money-=10;
        State.Food+=20;Log("补给：20份食物入库。");return true;
    }
    public bool CompleteCareRecord()
    {
        var c=Selected;
        if(c==null||c.RegionId!="home"||c.Quarantine||c.Fullness<70||c.Health<80||c.Observation.Samples<2||c.Observation.FeedingEvidence<2||!State.CompletedCareRecords.Add(c.Id))return false;
        State.Money+=40;Log($"{c.Name}的基础照护记录完成，示例委托奖励 40。");return true;
    }
    public bool Refill()
    {
        var feeder=ActiveRegion.Facilities.Where(f=>f.Kind==FacilityKind.Feeder).MinBy(f=>f.Stock);
        if(feeder==null)return false;
        int amount=State.UnlimitedResources?10:Math.Min(State.Food,10);
        if(amount==0)return false;
        if(!State.UnlimitedResources)State.Food-=amount;
        feeder.Stock+=amount;Log($"食盆补充 {amount} 份。");return true;
    }
    public void MoveCreature(string id,double x,double z)
    {
        if(!double.IsFinite(x)||!double.IsFinite(z))return;
        var c=State.Creatures.Find(c=>c.Id==id);if(c==null)return;
        c.X=Math.Clamp(x,-8,8);c.Z=Math.Clamp(z,-8,8);c.TargetX=c.X;c.TargetZ=c.Z;
    }
}
