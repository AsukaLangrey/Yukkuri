using System.Collections.Generic;
using Yukkuri.Simulation;

// Adapts already-known simulation events; presentation must not reveal hidden incidents.
internal sealed class CanvasNoticeSource
{
    private WorldState? _world;
    private readonly HashSet<string> _seen=new();
    public IEnumerable<CanvasNotice> Read(WorldState world)
    {
        if(!ReferenceEquals(_world,world))
        {
            _world=world;_seen.Clear();_seen.UnionWith(world.PlayerKnownIncidents);yield break;
        }
        foreach(var incident in world.Incidents)
        {
            if(!world.PlayerKnownIncidents.Contains(incident.Id)||!_seen.Add(incident.Id))continue;
            if(incident.Kind is not (IncidentKind.Death or IncidentKind.Rescue or IncidentKind.Intrusion or IncidentKind.Protection))continue;
            yield return new("incident:"+incident.Kind+":"+incident.TargetId,incident.Description,incident.Kind==IncidentKind.Death?3:2,incident.TargetId,true);
        }
    }
}
