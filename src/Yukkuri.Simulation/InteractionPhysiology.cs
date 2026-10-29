using System.Text.Json;
using System.Text.Json.Serialization;

namespace Yukkuri.Simulation;

public enum ExcretionKind { None, Urine, Feces }
public enum HealthStatus { Healthy, Injured, Critical, Recovering, Dead }

public sealed class PhysiologyState : IJsonOnDeserialized
{
    private double _bladder=35,_bowel=20,_remaining;
    private ExcretionKind _excreting;
    private int _urinations,_defecations;
    private static double Number(double value,double maximum)
    {
        if(!double.IsFinite(value)||value<0||value>maximum)throw new JsonException("排泄状态数值无效。");
        return value;
    }
    public double Bladder {get=>_bladder;set=>_bladder=Number(value,100);}
    public double Bowel {get=>_bowel;set=>_bowel=Number(value,100);}
    public double Remaining {get=>_remaining;set=>_remaining=Number(value,4);}
    public ExcretionKind Excreting
    {
        get=>_excreting;
        set{if(!Enum.IsDefined(value))throw new JsonException("排泄动作无效。");_excreting=value;}
    }
    public int Urinations {get=>_urinations;set=>_urinations=value>=0?value:throw new JsonException("排尿次数无效。");}
    public int Defecations {get=>_defecations;set=>_defecations=value>=0?value:throw new JsonException("排便次数无效。");}
    [JsonIgnore] public bool Active=>Excreting!=ExcretionKind.None&&Remaining>0;
    void IJsonOnDeserialized.OnDeserialized()
    {
        if((Excreting==ExcretionKind.None)!=(Remaining==0))throw new JsonException("排泄动作与剩余时间不一致。");
    }
}

public sealed partial class InteractionLife
{
    private PhysiologyState _physiology=new();
    public PhysiologyState Physiology
    {
        get=>_physiology;
        set=>_physiology=value??throw new JsonException("排泄状态缺失。");
    }
}

public sealed partial class Creature
{
    // Exact health stays in saved simulation data; the UI exposes only this category.
    [JsonIgnore] public HealthStatus HealthStatus=>Condition switch
    {
        PhysicalCondition.Dead=>HealthStatus.Dead,
        PhysicalCondition.Critical=>HealthStatus.Critical,
        PhysicalCondition.Recovering=>HealthStatus.Recovering,
        _=>Health<=60||Interaction.FootIntegrity<80||Interaction.Injuries.HasActiveWounds||Interaction.Injuries.Sites.Any(s=>s.PermanentLoss>0)?HealthStatus.Injured:HealthStatus.Healthy
    };
}

public sealed partial class WorldSimulation
{
    public static bool IsWaste(RoomObjectKind kind)=>kind is RoomObjectKind.Urine or RoomObjectKind.Feces;
    public bool CleanRoomWaste(string id)
    {
        var waste=State.RoomObjects.Find(o=>o.Id==id&&o.RegionId==State.ActiveRegionId&&!o.Stored&&IsWaste(o.Kind));
        if(waste==null)return false;
        State.RoomObjects.Remove(waste);return true;
    }

    // Shared by world and presentation updates. Returns whether the current
    // body state blocks excretion; cancelling here never advances time or needs.
    private bool SynchronizeInterruptedPhysiology(Creature c)
    {
        var i=c.Interaction;
        bool interrupted=(c.Condition is PhysicalCondition.Dead or PhysicalCondition.Critical)||
            c.Family.ColdStorage.Stored||
            i.Carried||i.Falling||i.Lowering||i.Height>.25||i.Supine||
            Math.Abs(i.VelocityX)+Math.Abs(i.VelocityZ)>.05||i.EatingRemaining>0||
            (InteractionGestureTarget==c.Id&&InteractionGestureBlocksAutonomy);
        if(!interrupted)return false;
        var p=i.Physiology;
        if(p.Active&&(c.Activity is "正在撒尿" or "正在排便"))c.Activity="排泄暂时中断";
        p.Excreting=ExcretionKind.None;p.Remaining=0;
        return true;
    }

    private void TickPhysiology(double dt)
    {
        foreach(var c in State.Creatures)
        {
            var i=c.Interaction;var p=i.Physiology;
            bool interrupted=SynchronizeInterruptedPhysiology(c);
            if(c.Condition is PhysicalCondition.Dead or PhysicalCondition.Critical)continue;
            double bodyDt=BiologicalDelta(c,dt);
            // Intake affects subsequent production rather than turning a fresh drink instantly into urine.
            // Stored bodies keep accumulating biological needs at the preservation
            // rate, but cannot perform an action or leave waste at their old yard position.
            p.Bladder=Math.Min(100,p.Bladder+bodyDt*(.10+.12*i.Hydration/100));
            p.Bowel=Math.Min(100,p.Bowel+bodyDt*(.06+.08*c.Fullness/100+(i.Digestion.Symptomatic?.16:0)));
            if(interrupted)continue;
            if(p.Active)
            {
                c.Activity=p.Excreting==ExcretionKind.Urine?"正在撒尿":"正在排便";
                p.Remaining=Math.Max(0,p.Remaining-dt);
                if(p.Remaining>0)continue;
                bool urine=p.Excreting==ExcretionKind.Urine;
                AddRoomWaste(c,urine?RoomObjectKind.Urine:RoomObjectKind.Feces);
                if(urine){p.Bladder=10;p.Urinations=Math.Min(int.MaxValue-1,p.Urinations)+1;}
                else{p.Bowel=10;p.Defecations=Math.Min(int.MaxValue-1,p.Defecations)+1;}
                p.Excreting=ExcretionKind.None;c.Activity=urine?"撒完尿，轻松了一些":"排便结束，轻松了一些";
                c.DecisionIn=0;
            }
            else if(p.Bladder>=85||p.Bowel>=85)
            {
                i.Resting=false;
                p.Excreting=p.Bladder>=85?ExcretionKind.Urine:ExcretionKind.Feces;
                p.Remaining=p.Excreting==ExcretionKind.Urine?2.5:3.5;
                c.TargetX=c.X;c.TargetZ=c.Z;
                c.Activity=p.Excreting==ExcretionKind.Urine?"正在撒尿":"正在排便";
            }
        }
    }

    private void AddRoomWaste(Creature c,RoomObjectKind kind)
    {
        // Bound long-running saves without deleting existing waste or unrelated objects.
        var waste=State.RoomObjects.Where(o=>o.RegionId==c.RegionId&&o.Kind==kind&&!o.Stored).ToArray();
        if(waste.Length>=64)
        {
            var nearest=waste.MinBy(o=>Distance(c.X,c.Z,o.X,o.Z))!;
            nearest.Portions=Math.Min(1000,nearest.Portions+1);return;
        }
        // Automatic production must resume identically from the same save.
        // Widen before adding: completed counts saturate at int.MaxValue.
        var p=c.Interaction.Physiology;
        long sequence=(long)(kind==RoomObjectKind.Urine?p.Urinations:p.Defecations)+1;
        string prefix=FormattableString.Invariant($"waste:{c.Id}:{(int)kind}:{sequence}");
        var usedIds=State.RoomObjects.Select(o=>o.Id).ToHashSet(StringComparer.Ordinal);
        string id=prefix;
        for(long suffix=1;usedIds.Contains(id);suffix++)
            id=FormattableString.Invariant($"{prefix}:{suffix}");
        State.RoomObjects.Add(new(){Id=id,Kind=kind,RegionId=c.RegionId,X=c.X,Z=c.Z,OwnerId=c.Id,Contamination=1});
    }
}
