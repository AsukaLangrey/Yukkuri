namespace Yukkuri.Simulation;

public sealed partial class InteractionLife
{
    public Dictionary<BodyPart,double> SurfaceDirt { get; set; }=[];
    public Dictionary<BodyPart,double> SurfaceWetness { get; set; }=[];
    private static readonly BodyPart[] SurfaceParts=[BodyPart.Head,BodyPart.Cheek,BodyPart.Side,BodyPart.Bottom,BodyPart.Mouth];
    private void PrepareSurface()
    {
        foreach(var p in SurfaceParts)
        {SurfaceDirt.TryAdd(p,Dirt);SurfaceWetness.TryAdd(p,Wetness);}
    }
    public double DirtAt(BodyPart part)=>SurfaceDirt.GetValueOrDefault(part,Dirt);
    public double WetnessAt(BodyPart part)=>SurfaceWetness.GetValueOrDefault(part,Wetness);
    public void WipeSurface(BodyPart part,double amount,bool dry)
    {
        if(part==BodyPart.Accessory)return;
        PrepareSurface();
        if(dry)SurfaceWetness[part]=Math.Max(0,SurfaceWetness[part]-amount);
        else
        {
            double removed=Math.Min(SurfaceDirt[part],amount);
            SurfaceDirt[part]-=removed;SurfaceWetness[part]=Math.Min(100,SurfaceWetness[part]+removed*.085);
        }
        ReconcileSurface();
    }
    public void DrySurfaceOverTime(double amount,bool bottomOnly=false)
    {
        PrepareSurface();
        foreach(var p in SurfaceParts)if(!bottomOnly||p==BodyPart.Bottom)SurfaceWetness[p]=Math.Max(0,SurfaceWetness[p]-amount);
        ReconcileSurface();
    }
    public void SoilSurface(double amount)
    {
        PrepareSurface();foreach(var p in SurfaceParts)SurfaceDirt[p]=Math.Min(100,SurfaceDirt[p]+amount);
        ReconcileSurface();
    }
    private void ReconcileSurface(){Dirt=SurfaceDirt.Values.Average();Wetness=SurfaceWetness.Values.Average();}
}
