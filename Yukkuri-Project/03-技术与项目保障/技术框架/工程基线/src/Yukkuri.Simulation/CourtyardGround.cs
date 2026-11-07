namespace Yukkuri.Simulation;
// Coordinates expand about the same origin; existing saved layouts keep their positions.
public static class CourtyardGround
{
    public const double Expansion=1.4142135623730951;
    public const double HalfSize=8*Expansion, PointerEdge=HalfSize-.3, PlacementEdge=HalfSize-.2;
    public const double SaveLimit=HalfSize+1;
}
public sealed partial class WorldState
{
    public double GrassNutritionCredit { get; set; }=300;
    public long GrassSequence { get; set; }
}
public sealed partial class WorldSimulation
{
    public const double GrassNutritionPerDay=300;
    public const int GrassPortionCap=42; // 294 nutrition on ground, plus fractional regeneration credit.
    private void TickWildGrass(double dt)
    {
        if(!Config.WildGrassEnabled)return;
        var grass=State.RoomObjects.Where(o=>o.Kind==RoomObjectKind.Leaves&&o.RegionId==State.ActiveRegionId&&!o.Stored).ToArray();
        int stock=grass.Sum(o=>o.Portions)+(State.Creatures.Count(c=>c.RegionId==State.ActiveRegionId&&c.Interaction.NaturalIntakePending&&c.Interaction.GroundIntakeKind==RoomObjectKind.Leaves));
        if(stock>=GrassPortionCap){State.GrassNutritionCredit=0;return;}
        State.GrassNutritionCredit=Math.Min(GrassNutritionPerDay,State.GrassNutritionCredit+dt*Config.WildGrassNutritionPerDay/GrowthRules.Day);
        // Bounded placement work; failed placements retain credit but never invent food.
        for(int attempt=0;attempt<8&&stock<GrassPortionCap&&State.GrassNutritionCredit>=7;attempt++)
        {
            double x=(Next()*2-1)*(CourtyardGround.HalfSize-1),z=(Next()*2-1)*(CourtyardGround.HalfSize-1);
            if(!CanGrowGrass(x,z))continue;
            State.RoomObjects.RemoveAll(o=>o.Kind==RoomObjectKind.Leaves&&o.Portions==0&&NaturalResources.Consumer(State,o)==null&&
                !State.Creatures.Any(c=>c.Interaction.NaturalIntakePending&&c.Interaction.GroundIntakeSourceId==o.Id));
            string id="grass-"+(++State.GrassSequence).ToString(System.Globalization.CultureInfo.InvariantCulture);
            while(State.RoomObjects.Any(o=>o.Id==id))id="grass-"+(++State.GrassSequence).ToString(System.Globalization.CultureInfo.InvariantCulture);
            State.RoomObjects.Add(new(){Id=id,Kind=RoomObjectKind.Leaves,RegionId=State.ActiveRegionId,X=x,Z=z,Portions=1,Contamination=.5+Next()*.35});
            stock++;State.GrassNutritionCredit-=7;
        }
    }
    private bool CanGrowGrass(double x,double z)
    {
        string region=State.ActiveRegionId;
        if(!WoodlandLayout.GroundFits(State,region,x,z,.55)||!ShelterGroundFits(region,x,z,.6,allowInterior:false))return false;
        if(State.Shelters.Any(s=>s.RegionId==region&&Math.Abs(x-s.X)<ShelterGeometry.OuterWidth*.5*ShelterGeometry.Scale(s)+.6&&
            z>s.Z-ShelterGeometry.OuterDepth*.5*ShelterGeometry.Scale(s)-.6&&z<s.Z+ShelterGeometry.ApproachDepth(ShelterGeometry.Scale(s))+.7))return false;
        if(State.SupplyStations.Any(s=>s.RegionId==region&&Distance(x,z,s.X,s.Z)<SupplyStationRadius+1.2)||
            State.RoomObjects.Any(o=>o.RegionId==region&&!o.Stored&&Distance(x,z,o.X,o.Z)<.8))return false;
        var probe=new Creature{Id="grass-reach-probe",RegionId=region};GrowthRules.Initialize(probe,false);
        for(int n=0;n<8;n++)if(CanOccupyRoom(probe,x+Math.Sin(n*Math.Tau/8)*.7,z+Math.Cos(n*Math.Tau/8)*.7))return true;
        return false;
    }
}
