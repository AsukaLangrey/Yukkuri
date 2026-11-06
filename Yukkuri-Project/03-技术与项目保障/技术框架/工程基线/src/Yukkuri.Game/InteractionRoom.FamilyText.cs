using System;
using System.Collections.Generic;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    // Historical parent labels only. Active partners, care targets and selection stay on Creatures.
    private string ParentName(string? id,string missing)=>id==null?"未记录":
        Sim.State.KnownCreatures.FirstOrDefault(o=>o.Id==id)?.Name??missing;

    private string ObjectFamilyDetails(Creature c)
    {
        string Name(string? id)=>Sim.State.Creatures.Find(o=>o.Id==id)?.Name??"未记录";
        return $"家庭：母亲 {ParentName(c.Family.MotherId,"未记录")} · 父亲 {ParentName(c.Family.FatherId,"未记录")}\n"+
            "伴侣："+(c.Family.PartnerIds.Count==0?"暂无":string.Join("、",c.Family.PartnerIds.Select(id=>Name(id))));
    }

    private string FamilyCareSummary(Creature c)
    {
        string NameFor(string id)=>Sim.State.Creatures.FirstOrDefault(o=>o.Id==id)?.Name??"这位家人";
        string Delivery(Creature caregiver)
        {
            var parcel=Sim.State.RoomObjects.FirstOrDefault(o=>o.Id==caregiver.Family.CareParcelId&&
                o.CarrierId==caregiver.Id&&!o.Stored&&!o.HeldByHand);
            return parcel==null?"已安排照护，目前没有搬送中的食水":parcel.Kind switch
            {
                RoomObjectKind.Water=>"正在搬送一份水",
                RoomObjectKind.Sweet=>"正在搬送一份甜甜",
                _=>"正在搬送一份食物"
            };
        }
        var details=new List<string>();
        if(c.Family.CareTargetId is { } target)
        {
            string? reason=Sim.FamilyCareReason(c.Id,target);
            details.Add("当前照护："+c.Name+" → "+NameFor(target)+"；"+
                (reason==null?Delivery(c):"目前受阻："+reason));
        }
        foreach(var caregiver in Sim.State.Creatures.Where(o=>o.Id!=c.Id&&o.Family.CareTargetId==c.Id).OrderBy(o=>o.Id,StringComparer.Ordinal))
        {
            string? reason=Sim.FamilyCareReason(caregiver.Id,c.Id);
            details.Add("正在照护它："+caregiver.Name+"；"+
                (reason==null?Delivery(caregiver):"目前受阻："+reason));
        }
        return string.Join("\n",details);
    }

    private string FamilySummary(Creature c)
    {
        string NameFor(string? id)=>id==null?"未记录":Sim.State.Creatures.FirstOrDefault(o=>o.Id==id)?.Name??"不在这里的家人";
        var details=new List<string>{c.Name+" · "+(c.Body.ReproductionRole==ReproductionRole.Maternal?"母体角色":"父体角色")};
        if(c.Family.MotherId!=null||c.Family.FatherId!=null)details.Add("母亲："+ParentName(c.Family.MotherId,"不在这里的家人")+"；父亲："+ParentName(c.Family.FatherId,"不在这里的家人"));
        if(c.Family.PartnerIds.Count>0)details.Add("伴侣："+string.Join("、",c.Family.PartnerIds.OrderBy(id=>id).Select(id=>NameFor(id))));
        if(c.Family.CaregiverIds.Count>0)
        {
            var parents=c.Family.CaregiverIds.Where(id=>id==c.Family.MotherId||id==c.Family.FatherId||id==c.ParentId).ToArray();
            var foster=c.Family.CaregiverIds.Except(parents).ToArray();
            if(parents.Length>0)details.Add("亲代照护者："+string.Join("、",parents.Select(id=>NameFor(id))));
            if(foster.Length>0)details.Add("持续照顾后认下的养亲："+string.Join("、",foster.Select(id=>NameFor(id))));
        }
        string care=FamilyCareSummary(c);
        if(care.Length>0)details.Add(care);
        if(c.Family.CareLastEvent.Length>0)details.Add("照护记录："+c.Family.CareLastEvent);
        string situation=c.Condition==PhysicalCondition.Dead?"":FamilyRules.Describe(c);
        if(situation.Length>0)details.Add(situation);
        string aftermath=FamilyRules.DescribeAftermath(c);
        if(aftermath.Length>0)details.Add(aftermath);
        string risk=c.Condition==PhysicalCondition.Dead?"":FamilyRules.DescribeRisk(c);
        if(risk.Length>0)details.Add(risk);
        if(c.Condition!=PhysicalCondition.Dead)details.Add(FamilyRules.DescribeLearning(c));
        return string.Join("\n",details);
    }

}
