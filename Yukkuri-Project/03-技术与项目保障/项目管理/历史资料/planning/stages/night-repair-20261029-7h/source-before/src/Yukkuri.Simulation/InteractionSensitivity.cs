namespace Yukkuri.Simulation;

public sealed partial class InteractionLife
{
    // Older saves have one value for both cheeks. Import it only when a side is changed.
    public SplitSurface? CheekSensitivitySides { get; set; }
    public double CheekSensitivityAt(SurfaceSide side)=>CheekSensitivitySides is { } pair?
        side==SurfaceSide.Left?pair.Left:side==SurfaceSide.Right?pair.Right:Math.Max(pair.Left,pair.Right):CheekSensitivity;
    public void ChangeCheekSensitivity(SurfaceSide side,double amount)
    {
        if(CheekSensitivitySides==null)
        {
            if(side==SurfaceSide.Whole){CheekSensitivity=Math.Clamp(CheekSensitivity+amount,0,100);return;}
            CheekSensitivitySides=new(){Left=CheekSensitivity,Right=CheekSensitivity};
        }
        if(side!=SurfaceSide.Right)CheekSensitivitySides.Left=Math.Clamp(CheekSensitivitySides.Left+amount,0,100);
        if(side!=SurfaceSide.Left)CheekSensitivitySides.Right=Math.Clamp(CheekSensitivitySides.Right+amount,0,100);
        // The general observation must not hide one sore cheek by averaging it with a healthy one.
        CheekSensitivity=Math.Max(CheekSensitivitySides.Left,CheekSensitivitySides.Right);
    }
}
