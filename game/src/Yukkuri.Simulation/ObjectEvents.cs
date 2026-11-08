namespace Yukkuri.Simulation;
public sealed class ObjectEventRecord
{
    public string ObjectId { get; set; }="";
    public string[] ParticipantIds { get; set; }=[];
    public double At { get; set; }
    public string Text { get; set; }="";
}
public sealed partial class WorldState
{
    public List<ObjectEventRecord> ObjectEvents { get; set; }=[];
}
public sealed partial class WorldSimulation
{
    private void RecordObjectEvent(string objectId,string text,params string[] participants)
    {
        State.ObjectEvents.Add(new(){ObjectId=objectId,Text=text,At=State.WorldSeconds,ParticipantIds=participants.Distinct(StringComparer.Ordinal).ToArray()});
        if(State.ObjectEvents.Count>512)State.ObjectEvents.RemoveAt(0);
    }
    public IEnumerable<string> EventsFor(string id)=>State.ObjectEvents.Where(e=>e.ObjectId==id||e.ParticipantIds.Contains(id,StringComparer.Ordinal)).Select(e=>e.Text);
}
