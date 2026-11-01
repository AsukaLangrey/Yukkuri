namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    private InteractionFeedback GiveInteractionTreasure(Creature c)
    {
        var existing=State.RoomObjects.FirstOrDefault(o=>o.Kind==RoomObjectKind.Treasure&&o.OwnerId==c.Id);
        if(existing is { Stored:false })return Unavailable("它已经有自己的宝物，先看看放在了哪里。 ");
        (double X,double Z)? place=null;
        foreach(double distance in new[]{.85,1.25,1.65})
        {
            for(int n=0;n<16;n++)
            {
                double angle=n*Math.PI/8,x=c.X+distance*Math.Cos(angle),z=c.Z+distance*Math.Sin(angle);
                // Conservative clearance includes the owner's current pose, nearby bodies and supports.
                if(CanPlaceRoomBall(c,x,z)){place=(x,z);break;}
            }
            if(place.HasValue)break;
        }
        if(place is not { } spot)return Unavailable("旁边太挤，先给小石头留一个能看见的空位置。 ");
        if(existing!=null)
        {
            existing.Stored=false;existing.RegionId=c.RegionId;existing.X=spot.X;existing.Z=spot.Z;existing.Height=0;
            RecordInteraction(c,"treasure-return","被收走的那颗小石头回来了。",.5);
            return Feedback(c,"treasure-return",Expression.Shy,"原来的小宝物回来了，它先仔细看看。","proud");
        }
        State.RoomObjects.Add(new(){Kind=RoomObjectKind.Treasure,RegionId=c.RegionId,X=spot.X,Z=spot.Z,OwnerId=c.Id});
        ImproveAffection(c,1);RecordInteraction(c,"treasure","收到一颗属于自己的小石头。",2);
        return Feedback(c,"treasure",Expression.Delighted,"把小石头放在旁边的空处，它仔细端详自己的小宝物。","proud");
    }
}
