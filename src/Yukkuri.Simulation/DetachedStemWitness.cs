namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    private bool SeesDetachedStem(Creature observer,DetachedStemBrood s)=>observer.RegionId==s.RegionId&&
        observer.Condition==PhysicalCondition.Active&&!observer.Family.ColdStorage.Stored&&!observer.Family.Memorialized&&
        !observer.Interaction.Resting&&!observer.Quarantine&&observer.Interaction.Restraint!=RestraintKind.Box&&
        Distance(observer.X,observer.Z,s.X,s.Z)<=4.5&&HasGroundSightLine(observer,s.X,s.Z);

    private void NotifyDetachedStemWitnesses(DetachedStemBrood s,string description,bool playerHarm,double severity)
    {
        if(severity<=0)return;
        var incident=playerHarm&&State.WorldSeconds-s.ThreatIncidentAt<2.5?
            State.Incidents.Find(e=>e.Id==s.ThreatIncidentId&&e.ActorId=="player"&&e.Kind==IncidentKind.Attack):null;
        if(incident==null)
        {
            // Object identity lives in the brood's own ledger. TargetId stays
            // null rather than pretending the remote mother was struck here.
            incident=new(){Id=$"event-{++State.IncidentSequence:000000}",Kind=playerHarm?IncidentKind.Attack:
                s.Phase==DetachedStemPhase.Failed?IncidentKind.Help:IncidentKind.Care,
                ActorId=playerHarm?"player":null,RegionId=s.RegionId,X=s.X,Z=s.Z,At=State.WorldSeconds,
                Description=description,Severity=severity};
            State.Incidents.Add(incident);TrimIncidentHistory();
            if(playerHarm){s.ThreatIncidentId=incident.Id;s.ThreatIncidentAt=State.WorldSeconds;}
        }
        else incident.Severity+=severity;
        if(s.RegionId==State.ActiveRegionId)State.PlayerKnownIncidents.Add(incident.Id);
        foreach(var observer in State.Creatures.Where(c=>c.RegionId==s.RegionId&&c.Condition!=PhysicalCondition.Dead&&
            !c.Family.ColdStorage.Stored&&!c.Family.Memorialized))
        {
            bool sees=SeesDetachedStem(observer,s);
            double audible=playerHarm?Math.Max(0,incident.Severity-4)-Math.Max(0,incident.Severity-severity-4):0;
            bool hears=!sees&&audible>0&&Distance(observer.X,observer.Z,s.X,s.Z)<=7;
            if(!sees&&!hears)continue;
            var old=observer.Knowledge.Find(k=>k.IncidentId==incident.Id);bool firstSight=sees&&old?.Source!=KnowledgeSource.Witness;
            Learn(observer,incident,sees?KnowledgeSource.Witness:KnowledgeSource.Heard,sees&&playerHarm);
            if(sees&&observer.Knowledge.Find(k=>k.IncidentId==incident.Id) is { } knowledge)
            {knowledge.Source=KnowledgeSource.Witness;knowledge.InformantId=null;}
            bool parent=observer.Id==s.Mother.Id||observer.Id==s.Father.Id;
            double perceived=(sees?severity:audible)*(parent?1.45:1);
            bool danger=playerHarm||s.Phase==DetachedStemPhase.Failed;
            if(danger)
            {
                observer.Fear=Clamp100(observer.Fear+perceived*(sees?.5:.15));RememberThreat(observer,perceived*(sees?.8:.25));
                if(playerHarm&&sees)observer.Trust=Math.Max(0,observer.Trust-perceived*.22);
            }
            var memory=observer.PersonalMemories.Find(m=>m.IncidentId==incident.Id);
            string text=sees?description:"听到了培养瓶附近不安的声音，还不知道是谁造成的。";
            if(memory==null)Remember(observer,incident,text,perceived);
            else{memory.EmotionalWeight+=perceived;if(sees)memory.Text=text;}
            if(sees&&(memory==null||firstSight))
                React(observer,danger?Expression.Frightened:Expression.Curious,
                    danger?(parent?"那是我们的宝宝！":"不要这样……") :"宝宝出来了。",text,4);
        }
    }
}
