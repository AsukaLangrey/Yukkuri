namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    private static double BallBodyRadius(Creature c)
    {
        double scale=c.IsYoung?.73:c.IsElder?1.03:1;
        return (c.Interaction.Supine||c.Interaction.Rolling?InteractionFixtureGeometry.SupineFootprint:c.Interaction.Restraint==RestraintKind.Wrap?.68:.565)*scale;
    }
    private void AdvanceRoomBall(RoomObject ball,double dt)
    {
        double speed=Distance(0,0,ball.VelocityX,ball.VelocityZ);
        int steps=Math.Clamp((int)Math.Ceiling(speed*dt/.04),1,32);
        double slice=dt/steps,edge=InteractionFixtureGeometry.YardFenceInner-InteractionFixtureGeometry.BallRadius;
        bool woodland=WoodlandLayout.HasRiver(State,ball.RegionId);
        ball.X=Math.Clamp(ball.X,-edge,edge);ball.Z=Math.Clamp(ball.Z,-edge,edge);
        for(int step=0;step<steps;step++)
        {
            double previousX=ball.X,previousZ=ball.Z;
            ball.X+=ball.VelocityX*slice;ball.Z+=ball.VelocityZ*slice;
            if(Math.Abs(ball.X)>edge){ball.X=Math.Clamp(ball.X,-edge,edge);ball.VelocityX=-Math.Sign(ball.X)*Math.Abs(ball.VelocityX)*.6;}
            if(Math.Abs(ball.Z)>edge){ball.Z=Math.Clamp(ball.Z,-edge,edge);ball.VelocityZ=-Math.Sign(ball.Z)*Math.Abs(ball.VelocityZ)*.6;}
            foreach(var cushion in State.RoomObjects)
                if(!cushion.Stored&&cushion.RegionId==ball.RegionId&&cushion.Kind==RoomObjectKind.Cushion)
                    ResolveBallBox(ball,cushion.X,cushion.Z,InteractionFixtureGeometry.CushionWidth*.5,InteractionFixtureGeometry.CushionDepth*.5,.25);
            if(woodland)
                foreach(var tree in WoodlandLayout.Trees)
                {
                    var trunk=tree.TrunkAt(InteractionFixtureGeometry.BallRadius);
                    ResolveBallCircle(ball,trunk.X,trunk.Z,trunk.Radius+.006,.48);
                }
            foreach(var c in State.Creatures)
            {
                if(c.RegionId!=ball.RegionId||c.Family.ColdStorage.Stored||c.Family.Memorialized)continue;
                double scale=c.IsYoung?.73:c.IsElder?1.03:1;var life=c.Interaction;
                if(life.Restraint==RestraintKind.Box)
                {ResolveBallBox(ball,c.X,c.Z,InteractionFixtureGeometry.BoxWidth*.5*scale,InteractionFixtureGeometry.BoxDepth*.5*scale,.48);continue;}
                if(life.Restraint==RestraintKind.Hanging)
                    ResolveBallBox(ball,c.X+.8*scale,c.Z-.55*scale,.45*scale,.35*scale,.48);
                if(life.Height>.55)continue;
                double dx=ball.X-c.X,dz=ball.Z-c.Z,length=Math.Sqrt(dx*dx+dz*dz),radius=BallBodyRadius(c)+InteractionFixtureGeometry.BallRadius;
                if(length>=radius)continue;
                double nx=length>.00001?dx/length:1,nz=length>.00001?dz/length:0;
                double impact=-(ball.VelocityX*nx+ball.VelocityZ*nz);
                ball.X=c.X+nx*radius;ball.Z=c.Z+nz*radius;
                ReflectBall(ball,nx,nz,.42);
                if(impact>.2&&life.BallContactCooldown<=0&&c.Condition!=PhysicalCondition.Dead)
                {
                    life.BallContactCooldown=1;life.SetReactionDirection(-nx,-nz);
                    Feedback(c,"ball-touch",c.Fear>45?Expression.Worried:Expression.Curious,"软球碰到身体后弹开，它跟着缩了一下。","ball-bump",false);
                }
            }
            // A narrow gap behind a body is not an exit through the fence.
            if(Math.Abs(ball.X)>edge||Math.Abs(ball.Z)>edge){ball.X=previousX;ball.Z=previousZ;}
        }
        bool inWater=woodland&&RoomBallEnvironment.InWater(State,ball);
        double drag=Math.Exp(-dt*(inWater?3.2:.65));ball.VelocityX*=drag;
        double current=inWater&&State.Mode!=PlayMode.CloseUp?RoomBallEnvironment.CurrentSpeed:0;
        ball.VelocityZ=current+(ball.VelocityZ-current)*drag;
        if(current==0&&Math.Abs(ball.VelocityX)+Math.Abs(ball.VelocityZ)<.015){ball.VelocityX=0;ball.VelocityZ=0;}
    }
    private bool CanPlaceRoomBall(Creature owner,double x,double z)
    {
        double radius=InteractionFixtureGeometry.BallRadius,edge=InteractionFixtureGeometry.YardFenceInner-radius;
        if(Math.Abs(x)>edge||Math.Abs(z)>edge)return false;
        if(!WoodlandLayout.GroundFits(State,owner.RegionId,x,z,radius))return false;
        bool Box(double cx,double cz,double w,double d)
        {
            double dx=Math.Max(0,Math.Abs(x-cx)-w),dz=Math.Max(0,Math.Abs(z-cz)-d);
            return dx*dx+dz*dz<radius*radius;
        }
        foreach(var cushion in State.RoomObjects)
            if(!cushion.Stored&&cushion.RegionId==owner.RegionId&&cushion.Kind==RoomObjectKind.Cushion&&Box(cushion.X,cushion.Z,InteractionFixtureGeometry.CushionWidth*.5,InteractionFixtureGeometry.CushionDepth*.5))return false;
        foreach(var c in State.Creatures)
        {
            if(c.RegionId!=owner.RegionId||c.Family.ColdStorage.Stored||c.Family.Memorialized)continue;double scale=c.IsYoung?.73:c.IsElder?1.03:1;var life=c.Interaction;
            if(life.Restraint==RestraintKind.Box&&Box(c.X,c.Z,InteractionFixtureGeometry.BoxWidth*.5*scale,InteractionFixtureGeometry.BoxDepth*.5*scale))return false;
            if(life.Restraint==RestraintKind.Hanging&&Box(c.X+.8*scale,c.Z-.55*scale,.45*scale,.35*scale))return false;
            if(life.Height<=.55&&Distance(x,z,c.X,c.Z)<BallBodyRadius(c)+radius)return false;
        }
        return true;
    }
    private static void ReflectBall(RoomObject ball,double nx,double nz,double bounce)
    {
        double toward=ball.VelocityX*nx+ball.VelocityZ*nz;
        if(toward>=0)return;
        ball.VelocityX-=(1+bounce)*toward*nx;ball.VelocityZ-=(1+bounce)*toward*nz;
    }
    private static void ResolveBallCircle(RoomObject ball,double x,double z,double solidRadius,double bounce)
    {
        double dx=ball.X-x,dz=ball.Z-z,distance=Math.Sqrt(dx*dx+dz*dz),radius=InteractionFixtureGeometry.BallRadius+solidRadius;
        if(distance>=radius)return;
        double nx=distance>.00001?dx/distance:1,nz=distance>.00001?dz/distance:0;
        ball.X=x+nx*radius;ball.Z=z+nz*radius;ReflectBall(ball,nx,nz,bounce);
    }
    private static void ResolveBallBox(RoomObject ball,double x,double z,double halfWidth,double halfDepth,double bounce)
    {
        double dx=ball.X-Math.Clamp(ball.X,x-halfWidth,x+halfWidth),dz=ball.Z-Math.Clamp(ball.Z,z-halfDepth,z+halfDepth);
        double distance=Math.Sqrt(dx*dx+dz*dz),radius=InteractionFixtureGeometry.BallRadius;
        if(distance>=radius)return;
        double nx,nz;
        if(distance>.00001)
        {nx=dx/distance;nz=dz/distance;ball.X+=nx*(radius-distance);ball.Z+=nz*(radius-distance);}
        else
        {
            double sideX=halfWidth-Math.Abs(ball.X-x),sideZ=halfDepth-Math.Abs(ball.Z-z);
            if(sideX<sideZ){nx=ball.X<x?-1:1;nz=0;ball.X=x+nx*(halfWidth+radius);}
            else{nx=0;nz=ball.Z<z?-1:1;ball.Z=z+nz*(halfDepth+radius);}
        }
        ReflectBall(ball,nx,nz,bounce);
    }
}
