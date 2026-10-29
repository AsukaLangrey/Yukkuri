namespace Yukkuri.Simulation;

public sealed class InteractionDressing
{
    public BodyPart Part { get; set; }
    public SurfaceSide Side { get; set; }
    public double Remaining { get; set; }
}

public sealed partial class InteractionLife
{
    public List<InteractionDressing> Dressings { get; set; }=[];
    public static SurfaceSide DressingSiteSide(BodyPart part,SurfaceSide side)=>HasPairedSides(part)?side==SurfaceSide.Whole?SurfaceSide.Right:side:SurfaceSide.Whole;
    private void ImportLegacyDressing()
    {
        if(Dressings.Count==0&&DressingPart is { } part)
            Dressings.Add(new(){Part=part,Side=DressingSiteSide(part,DressingSide),Remaining=DressingRemaining});
    }
    public bool HasDressing(BodyPart part,SurfaceSide side=SurfaceSide.Whole)
    {
        side=DressingSiteSide(part,side);
        if(Dressings.Count>0)return FindDressing(part,side)!=null;
        return DressingPart==part&&DressingSiteSide(part,DressingSide)==side;
    }
    private InteractionDressing? FindDressing(BodyPart part,SurfaceSide side)
    {
        for(int index=0;index<Dressings.Count;index++)if(Dressings[index].Part==part&&Dressings[index].Side==side)return Dressings[index];
        return null;
    }
    public void Dress(BodyPart part,SurfaceSide side,double duration=45)
    {
        ImportLegacyDressing();side=DressingSiteSide(part,side);
        var current=FindDressing(part,side);
        if(current==null)Dressings.Add(new(){Part=part,Side=side,Remaining=duration});else current.Remaining=duration;
        DressingPart=part;DressingSide=side;DressingRemaining=duration;
    }
    public void RemoveDressing(BodyPart part)
    {
        ImportLegacyDressing();
        for(int index=Dressings.Count-1;index>=0;index--)if(Dressings[index].Part==part)Dressings.RemoveAt(index);
        SyncLegacyDressing();
    }
    public void AdvanceDressings(Creature creature,double dt)
    {
        ImportLegacyDressing();
        bool settled=creature.Health>=95&&creature.Pain<=5&&creature.Condition==PhysicalCondition.Active;
        for(int index=Dressings.Count-1;index>=0;index--)
        {
            var dressing=Dressings[index];dressing.Remaining=Math.Max(0,dressing.Remaining-dt);
            bool siteSettled=dressing.Part switch {BodyPart.Bottom=>!FootTreated,BodyPart.Head=>HeadSensitivity<=15,BodyPart.Cheek=>CheekSensitivityAt(dressing.Side)<=15,_=>true};
            if(dressing.Remaining<=0&&settled&&siteSettled)Dressings.RemoveAt(index);
        }
        SyncLegacyDressing();
    }
    private void SyncLegacyDressing()
    {
        // Keep the old fields as the most recent remaining site for older consumers.
        // Empty lists must also clear those fields, or a removed dressing would reimport next tick.
        var latest=DressingPart is { } part?FindDressing(part,DressingSiteSide(part,DressingSide)):null;
        if(latest==null&&Dressings.Count>0)latest=Dressings[^1];
        DressingPart=latest?.Part;DressingSide=latest?.Side??SurfaceSide.Whole;DressingRemaining=latest?.Remaining??0;
    }
}
