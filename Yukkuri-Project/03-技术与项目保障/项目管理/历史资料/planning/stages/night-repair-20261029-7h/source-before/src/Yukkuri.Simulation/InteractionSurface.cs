namespace Yukkuri.Simulation;

public sealed class SplitSurface
{
    public double Left {get;set;}
    public double Right {get;set;}
    public double Average=>(Left+Right)*.5;
}

public sealed partial class InteractionLife
{
    public Dictionary<BodyPart,double> SurfaceDirt { get; set; }=[];
    public Dictionary<BodyPart,double> SurfaceWetness { get; set; }=[];
    public Dictionary<BodyPart,SplitSurface> SideDirt { get; set; }=[];
    public Dictionary<BodyPart,SplitSurface> SideWetness { get; set; }=[];
    private static readonly BodyPart[] SurfaceParts=[BodyPart.Head,BodyPart.Cheek,BodyPart.Side,BodyPart.Bottom,BodyPart.Mouth];
    private void PrepareSurface()
    {
        foreach(var p in SurfaceParts)
        {SurfaceDirt.TryAdd(p,Dirt);SurfaceWetness.TryAdd(p,Wetness);}
    }
    public static bool HasPairedSides(BodyPart part)=>part is BodyPart.Cheek or BodyPart.Side;
    public double DirtAt(BodyPart part,SurfaceSide side=SurfaceSide.Whole)=>SideValue(SideDirt,part,side,SurfaceDirt.GetValueOrDefault(part,Dirt));
    public double WetnessAt(BodyPart part,SurfaceSide side=SurfaceSide.Whole)=>SideValue(SideWetness,part,side,SurfaceWetness.GetValueOrDefault(part,Wetness));
    private static double SideValue(Dictionary<BodyPart,SplitSurface> values,BodyPart part,SurfaceSide side,double fallback)=>
        values.TryGetValue(part,out var pair)?side==SurfaceSide.Left?pair.Left:side==SurfaceSide.Right?pair.Right:pair.Average:fallback;
    private void PrepareSides(BodyPart part)
    {
        double dirt=DirtAt(part),wet=WetnessAt(part);
        SideDirt.TryAdd(part,new(){Left=dirt,Right=dirt});SideWetness.TryAdd(part,new(){Left=wet,Right=wet});
    }
    public void WipeSurface(BodyPart part,double amount,bool dry,SurfaceSide side=SurfaceSide.Whole)
    {
        if(part==BodyPart.Accessory)return;
        PrepareSurface();
        bool split=HasPairedSides(part)&&(side!=SurfaceSide.Whole||SideDirt.ContainsKey(part)||SideWetness.ContainsKey(part));
        if(split)
        {
            PrepareSides(part);var dirt=SideDirt[part];var wet=SideWetness[part];
            if(side!=SurfaceSide.Right)
            {
                double removed=Math.Min(dirt.Left,amount);
                if(dry)wet.Left=Math.Max(0,wet.Left-amount);else{dirt.Left-=removed;wet.Left=Math.Min(100,wet.Left+removed*.085);}
            }
            if(side!=SurfaceSide.Left)
            {
                double removed=Math.Min(dirt.Right,amount);
                if(dry)wet.Right=Math.Max(0,wet.Right-amount);else{dirt.Right-=removed;wet.Right=Math.Min(100,wet.Right+removed*.085);}
            }
        }
        else if(dry)SurfaceWetness[part]=Math.Max(0,SurfaceWetness[part]-amount);
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
        foreach(var (part,wet) in SideWetness)if(!bottomOnly||part==BodyPart.Bottom)
        {wet.Left=Math.Max(0,wet.Left-amount);wet.Right=Math.Max(0,wet.Right-amount);}
        ReconcileSurface();
    }
    public void SoilSurface(double amount)
    {
        PrepareSurface();foreach(var p in SurfaceParts)SurfaceDirt[p]=Math.Min(100,SurfaceDirt[p]+amount);
        foreach(var dirt in SideDirt.Values){dirt.Left=Math.Min(100,dirt.Left+amount);dirt.Right=Math.Min(100,dirt.Right+amount);}
        ReconcileSurface();
    }
    private void ReconcileSurface()
    {
        foreach(var (part,dirt) in SideDirt)SurfaceDirt[part]=dirt.Average;
        foreach(var (part,wet) in SideWetness)SurfaceWetness[part]=wet.Average;
        Dirt=SurfaceDirt.Values.Average();Wetness=SurfaceWetness.Values.Average();
    }
}
