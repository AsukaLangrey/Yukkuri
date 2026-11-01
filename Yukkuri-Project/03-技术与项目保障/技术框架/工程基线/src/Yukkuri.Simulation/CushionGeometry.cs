namespace Yukkuri.Simulation;

// The top surface is shared with the visible cushion and supply placement.
public static class CushionGeometry
{
    public const float RadiusX=InteractionFixtureGeometry.CushionWidth*.5f,RadiusZ=InteractionFixtureGeometry.CushionDepth*.5f;
    public const float CenterY=.12f,HalfHeight=.11f,VerticalPower=.7f,HorizontalPower=.55f;
    public static float TopHeight(float x,float z)
    {
        float q=MathF.Pow(MathF.Abs(x)/RadiusX,2/HorizontalPower)+MathF.Pow(MathF.Abs(z)/RadiusZ,2/HorizontalPower);
        if(q>1.00001f)return 0;
        float across=MathF.Exp(-(x-.32f)*(x-.32f)/.008f)+MathF.Exp(-(x+.32f)*(x+.32f)/.008f);
        float along=MathF.Exp(-(z-.23f)*(z-.23f)/.008f)+MathF.Exp(-(z+.23f)*(z+.23f)/.008f);
        return CenterY+HalfHeight*MathF.Pow(MathF.Max(0,1-MathF.Pow(q,HorizontalPower/VerticalPower)),VerticalPower*.5f)-.010f*across*along;
    }
}

public static class RoomSupplySupport
{
    public static double Height(WorldState state,string region,double x,double z,RoomObjectKind kind=RoomObjectKind.Meal)
    {
        double height=0;
        foreach(var c in state.Creatures)
        {
            if(c.RegionId!=region||c.Interaction.Restraint is not (RestraintKind.Box or RestraintKind.Frame))continue;
            double scale=c.IsYoung?.73:c.IsElder?1.03:1;
            double width=c.Interaction.Restraint==RestraintKind.Box?InteractionFixtureGeometry.BoxWidth:InteractionFixtureGeometry.FrameWidth;
            double depth=c.Interaction.Restraint==RestraintKind.Box?InteractionFixtureGeometry.BoxDepth:InteractionFixtureGeometry.FrameDepth;
            if(Math.Abs(x-c.X)<width*.5*scale&&Math.Abs(z-c.Z)<depth*.5*scale)
                height=Math.Max(height,(.46+InteractionFixtureGeometry.BoxFloorY+InteractionFixtureGeometry.BoxFloorThickness*.5)*scale);
        }
        foreach(var cushion in state.RoomObjects)
        {
            if(cushion.Stored||cushion.RegionId!=region||cushion.Kind!=RoomObjectKind.Cushion)continue;
            if(Math.Abs(x-cushion.X)>CushionGeometry.RadiusX+.2||Math.Abs(z-cushion.Z)>CushionGeometry.RadiusZ+.2)continue;
            // Flat plates and cups rest on the highest point beneath their small base.
            for(int n=0;n<9;n++)
            {
                double angle=n*Math.Tau/8,radius=n==8?0:kind==RoomObjectKind.Water?.08:kind==RoomObjectKind.Sweet?.18:.2;
                float top=CushionGeometry.TopHeight((float)(x-cushion.X+Math.Cos(angle)*radius),(float)(z-cushion.Z+Math.Sin(angle)*radius));
                if(top>0)height=Math.Max(height,cushion.Height+top);
            }
        }
        return height;
    }
}
