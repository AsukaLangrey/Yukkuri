namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    public bool CanPlaceInteractionAt(string id,double x,double z)
    {
        var c=InteractionTarget(id);
        return c!=null&&double.IsFinite(x)&&double.IsFinite(z)&&CanOccupyRoom(c,x,z);
    }
    public static double InteractionFootprint(Creature c,RestraintKind? restraint=null)
    {
        double scale=c.IsYoung?.73:c.IsElder?1.03:1;
        return scale*((restraint??c.Interaction.Restraint) switch
        {RestraintKind.Box=>1.12,RestraintKind.Frame=>.98,RestraintKind.Hanging=>1.05,_=>.62});
    }
    private bool CanOccupyRoom(Creature c,double x,double z,RestraintKind? restraint=null)
    {
        double own=InteractionFootprint(c,restraint);
        if(Math.Abs(x)>8-own||Math.Abs(z)>8-own)return false;
        foreach(var other in State.Creatures)
        {
            if(other.Id==c.Id||other.RegionId!=c.RegionId||other.Interaction.Height>1&&other.Interaction.Restraint==RestraintKind.None)continue;
            double gap=own+InteractionFootprint(other);
            if(Distance(x,z,other.X,other.Z)<gap-.00001)return false;
        }
        return true;
    }
    private bool GroundMove(Creature c,double x,double z,bool slide)
    {
        double dx=x-c.X,dz=z-c.Z,length=Math.Sqrt(dx*dx+dz*dz);
        if(length<.00001)return true;
        int slices=Math.Max(1,(int)Math.Ceiling(length/.06));
        dx/=slices;dz/=slices;
        bool complete=true;
        for(int n=0;n<slices;n++)
        {
            if(CanOccupyRoom(c,c.X+dx,c.Z+dz)){c.X+=dx;c.Z+=dz;continue;}
            complete=false;
            if(!slide)break;
            // Try a small turn around the obstacle. Never displace a restrained or injured neighbor.
            bool moved=false;
            foreach(double angle in new[]{Math.PI/4,-Math.PI/4,Math.PI/2,-Math.PI/2})
            {
                double sx=dx*Math.Cos(angle)-dz*Math.Sin(angle),sz=dx*Math.Sin(angle)+dz*Math.Cos(angle);
                if(!CanOccupyRoom(c,c.X+sx,c.Z+sz))continue;
                c.X+=sx;c.Z+=sz;moved=true;break;
            }
            if(!moved)break;
        }
        return complete;
    }
    private void ClearLandingOverlap(Creature c)
    {
        if(CanOccupyRoom(c,c.X,c.Z))return;
        double originX=c.X,originZ=c.Z;
        for(double radius=.08;radius<=3;radius+=.08)
            for(int angle=0;angle<32;angle++)
            {
                double x=originX+Math.Cos(angle*Math.Tau/32)*radius,z=originZ+Math.Sin(angle*Math.Tau/32)*radius;
                if(!CanOccupyRoom(c,x,z))continue;
                c.X=x;c.Z=z;c.TargetX=x;c.TargetZ=z;return;
            }
    }
}
