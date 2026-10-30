namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    // One bodily strike, one fact and one victim memory. Human contact memory
    // is deliberately separate: witnessing another creature is not player harm.
    private Incident InflictPeerAttack(Creature actor,Creature target,double damage,double severity,
        string cause,string description,string victimMemory,double fearIncrease)
    {
        var incident=Emit(IncidentKind.Attack,target,actor.Id,description,severity,visibleActor:actor);
        RememberThreat(target,damage*2);
        target.Fear=Clamp100(target.Fear+fearIncrease);
        Remember(target,incident,victimMemory,severity);
        Hurt(target,damage,cause,actor.Id,BodyPart.Side);
        foreach(var observer in State.Creatures.Where(c=>c!=actor&&c!=target&&c.RegionId==target.RegionId&&
            IsAvailable(c)&&!c.Family.ColdStorage.Stored))
        {
            var knowledge=observer.Knowledge.Find(k=>k.IncidentId==incident.Id);
            if(knowledge==null)continue;
            bool saw=knowledge.Source==KnowledgeSource.Witness;
            bool identified=saw&&knowledge.KnowsActor;
            double closeness=FamilyRules.IsCloseFamily(observer,target)?1.45:1;
            double perceived=severity*(saw?closeness:.3);
            observer.Fear=Clamp100(observer.Fear+perceived*.5);
            RememberThreat(observer,perceived*.7);
            string memory;
            if(identified)
            {
                var bond=FamilyBond(observer,actor);
                bond.Fear=Clamp100(bond.Fear+perceived*.5);
                bond.Resentment=Clamp100(bond.Resentment+perceived*.2);
                bond.Affection=Math.Max(0,bond.Affection-perceived*.15);
                bond.LastEvent=$"亲眼看见它伤害{target.Name}，不敢再放心靠近";
                memory=$"亲眼看见{actor.Name}伤害{target.Name}，记住了施害者。";
            }
            else memory=saw?"看见同伴受到伤害，但没看清施害者是谁。":"听见同伴痛苦的声音，还不知道是谁造成的。";
            Remember(observer,incident,memory,perceived);
            React(observer,identified?Expression.Frightened:Expression.Worried,
                identified?"不要伤害它！":"发生什么了……？",memory,5);
        }
        return incident;
    }
}
