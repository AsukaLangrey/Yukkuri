namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    public InteractionFeedback BeginSupplyStationGesture(string id,InteractionInput input)=>BeginInteractionGesture(id,
        input with {TargetKind=InteractionTargetKind.SupplyStation,ObjectId=input.ObjectId??id});
    private string? ValidateStationInput(string id,InteractionInput input)
    {
        if(input.TargetKind!=InteractionTargetKind.SupplyStation||input.ObjectId!=id||input.Part!=BodyPart.Side||
            !Enum.IsDefined(input.Side)||input.SupplyObjectId!=null||
            input.Action is not (InteractionAction.RefillSupplyStation or InteractionAction.Lift)||
            !new[]{input.Duration,input.Force,input.Motion,input.X,input.Z,input.Height,input.DirectionX,input.DirectionZ,
                input.StopImpact,input.TouchTangentFraction,input.TouchNormalSpeed}.All(double.IsFinite)||
            input.Duration is <0 or >1||input.Force is <0 or >1||input.Motion<0||input.Height is <0 or >3||
            input.StopImpact is <0 or >1||input.TouchTangentFraction is <0 or >1||
            Math.Abs(input.X)>8||Math.Abs(input.Z)>8)return "请用补给工具或托举，接触当前供给器的真实外表。";
        return StationError(Station(id));
    }
    private InteractionFeedback BeginStationCore(string id,InteractionInput input)
    {
        if(ValidateStationInput(id,input) is { } error)return Unavailable(error);
        var s=Station(id)!;if(StationActionError(s,input.Action) is { } refused)return Unavailable(refused);
        var g=new ContactGesture{TargetId=id,ObjectId=id,Input=input};_interactionGesture=g;
        if(input.Action==InteractionAction.Lift)s.HeldByHand=true;
        return g.Last=new(true,input.Action==InteractionAction.Lift?"已低托器具，沿空地移动；松开留在当前合法位置。":
            "对准仓体持续补充 1.6 秒，完成时才转入实际库存。","",input.Action==InteractionAction.Lift?"station-carry":"station-refill");
    }
    private InteractionFeedback ContinueStationGesture(ContactGesture g,InteractionInput input,bool inContact)
    {
        if(input.Action!=g.Input.Action||input.TargetKind!=g.Input.TargetKind||input.ObjectId!=g.ObjectId||
            ValidateStationInput(g.TargetId,input) is { })
        {EndInteractionGesture(true);return Unavailable("工具、对象或小院状态已改变，供给器动作停止。");}
        var s=Station(g.TargetId)!;
        if(g.Applied)return g.Last;
        g.Input=input;g.Elapsed+=input.Duration;
        if(input.Action==InteractionAction.Lift)
        {
            if(!s.HeldByHand||StationOutletBusy(s)){EndInteractionGesture(true);return Unavailable("器具已放下或出料处正被使用，搬动停止。");}
            // Substeps prohibit dragging a solid warehouse through bodies or fixtures.
            double dx=input.X-s.X,dz=input.Z-s.Z;int steps=Math.Max(1,(int)Math.Ceiling(Math.Sqrt(dx*dx+dz*dz)/.06));
            dx/=steps;dz/=steps;
            for(int n=0;n<steps;n++)
            {
                double x=s.X+dx,z=s.Z+dz;
                if(!CanPlaceStation(s.RegionId,x,z,s))return g.Last=new(true,"这里放不下仓体和前方出料盘，停在最后的合法位置。","","station-carry");
                var outlet=StationOutlet(s);s.X=x;s.Z=z;
                if(outlet!=null){outlet.X=x;outlet.Z=z+SupplyStationOutletOffset;outlet.Height=RoomSupplySupport.Height(State,s.RegionId,outlet.X,outlet.Z,outlet.Kind);}
            }
            return g.Last with {Changed=true};
        }
        if(!inContact){g.ContactActive=false;g.ContactElapsed=0;return g.Last with {Changed=false};}
        if(StationActionError(s,input.Action) is { } error){EndInteractionGesture(true);return Unavailable(error);}
        g.ContactActive=true;g.ContactElapsed+=input.Duration;
        if(g.ContactElapsed+1e-9<1.6)return g.Last;
        int room=StationCapacity(s)-s.StoredPortions-StationOutletPortions(s);
        int amount=Math.Min(room,s.Kind==SupplyStationKind.Food?State.Food:State.StationWaterPortions);
        if(amount<=0){EndInteractionGesture(true);return Unavailable("仓已满或实际库存不足，未转入任何份额。");}
        if(s.Kind==SupplyStationKind.Food)State.Food-=amount;else State.StationWaterPortions-=amount;
        s.StoredPortions+=amount;s.LastEvent=$"已补充 {amount} 份，实际出料仍逐份进行。";
        g.Applied=true;g.CompletionReleased=true;
        return g.Last=new(true,s.LastEvent,"","station-refill",Changed:true);
    }
    private InteractionFeedback EndStationGesture(ContactGesture g)
    {
        if(Station(g.TargetId) is { } s)s.HeldByHand=false;
        return g.Last with {Pose="idle",Changed=g.Applied};
    }
}
