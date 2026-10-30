namespace Yukkuri.Simulation;

public sealed partial class WorldSimulation
{
    public InteractionFeedback BeginDetachedStemGesture(string eventId,InteractionInput input)=>
        BeginInteractionGesture(eventId,input with {TargetKind=InteractionTargetKind.DetachedStem,ObjectId=input.ObjectId??eventId});

    private string? ValidateDetachedStemInput(string eventId,InteractionInput input)
    {
        if(input.TargetKind!=InteractionTargetKind.DetachedStem||input.ObjectId!=eventId||
            !Enum.IsDefined(input.Action)||!Enum.IsDefined(input.Part)||!Enum.IsDefined(input.Side)||
            !new[]{input.Force,input.Duration,input.Motion,input.X,input.Z,input.Height,input.DirectionX,input.DirectionZ,
                input.StopImpact,input.TouchTangentFraction,input.TouchNormalSpeed}.All(double.IsFinite)||
            input.Force is <0 or >1||input.Duration is <0 or >1||input.Motion<0||input.Height is <0 or >3||
            Math.Abs(input.X)>8||Math.Abs(input.Z)>8||input.StopImpact is <0 or >1||input.TouchTangentFraction is <0 or >1)
            return "离体茎的接触身份或输入无效。";
        var s=DetachedStem(eventId);var error=DetachedStemError(s,allowTerminal:input.Action==InteractionAction.ClearStem);if(error!=null)return error;
        if(input.Action==InteractionAction.ClearStem&&s!.Phase is not (DetachedStemPhase.Failed or DetachedStemPhase.Born))
            return "这窝仍然活着，不能作为残留收起。";
        if(input.Action==InteractionAction.UnbottleStem&&input.SupplyObjectId!=null)
            return "撤茎需要护理用品和真实瓶中活窝，不使用手持食水。";
        if(input.Action==InteractionAction.UnbottleStem&&input.Part!=BodyPart.Side)
            return "请把撤茎支护用品贴住真实瓶身或枝茎，瓶口空隙不能代替接触。";
        return input.Action is InteractionAction.BottleStem or InteractionAction.Drink or InteractionAction.FeedMeal or
            InteractionAction.Lift or InteractionAction.Punch or InteractionAction.Burn or InteractionAction.ClearStem or InteractionAction.UnbottleStem?null:"这个工具不能用于离体茎或培养瓶。";
    }
    private RoomObject? DetachedStemSupply(DetachedStemBrood s,InteractionInput input)=>State.RoomObjects.Find(o=>
        o.Id==input.SupplyObjectId&&o.RegionId==s.RegionId&&o.HeldByHand&&!o.Stored&&o.CarrierId==null&&o.Portions>0&&
        o.Kind==(input.Action==InteractionAction.Drink?RoomObjectKind.Water:RoomObjectKind.Meal));
    private string? DetachedStemActionError(DetachedStemBrood s,InteractionInput input)
    {
        bool vessel=s.Phase==DetachedStemPhase.Bottled&&s.VesselIntegrity>25;
        if(s.HeldByHand)return "先把培养瓶放稳，再进行接触照护。";
        if(State.Creatures.Any(c=>c.Interaction.Carried)||State.DetachedStemBroods.Any(b=>b.HeldByHand)||
            input.Action is not (InteractionAction.Drink or InteractionAction.FeedMeal)&&State.RoomObjects.Any(o=>o.HeldByHand))
            return "手里还有其他实际对象，先放下再使用这个工具。";
        if(input.Action==InteractionAction.BottleStem)
        {
            if(vessel)return "这窝已经在完整的培养瓶中。";
            if(State.StemVessels<=0)return "没有可用培养瓶，先购入一个。";
            if(!CanPlaceDetachedStem(s.RegionId,s.X,s.Z,s.EventId))return "这里被占用或不够干燥，暂时无法安放培养瓶。";
        }
        if(input.Action==InteractionAction.UnbottleStem)
        {
            if(s.Phase!=DetachedStemPhase.Bottled)return "只有仍活着的瓶中茎可以撤出。";
            if(State.Medicine<=0)return "没有护理用品，先购买一包再撤出活窝。";
            if(!CanPlaceDetachedStem(s.RegionId,s.X,s.Z,s.EventId))return "原位置被占用或不够干燥，先把培养瓶移到安全空地。";
        }
        if(input.Action is InteractionAction.Drink or InteractionAction.FeedMeal)
        {
            if(!vessel)return "先移入完整的培养瓶，破瓶留不住食水。";
            if(input.Action==InteractionAction.Drink&&s.Hydration>=95)return "瓶里的水分已经充足。";
            if(input.Action==InteractionAction.FeedMeal&&(s.Nutrition>=95||s.Hydration<10))return "营养已经充足，或需要先给培养瓶补水。";
            if(DetachedStemSupply(s,input)==null)return "手里没有绑定的那份实际食水，先拿起水杯或正餐。";
        }
        if(input.Action==InteractionAction.Lift)
        {
            if(s.Phase!=DetachedStemPhase.Bottled)return "先把松散的离体茎移入瓶中，再搬动整瓶。";
            if(State.Creatures.Any(c=>c.Interaction.Carried)||State.RoomObjects.Any(o=>o.HeldByHand)||State.DetachedStemBroods.Any(b=>b.HeldByHand))
                return "手里还有其他东西，先放稳。";
        }
        return null;
    }
    private InteractionFeedback BeginDetachedStemCore(string eventId,InteractionInput input)
    {
        var error=ValidateDetachedStemInput(eventId,input);if(error!=null)return Unavailable(error);
        var s=DetachedStem(eventId)!;error=DetachedStemActionError(s,input);if(error!=null)return Unavailable(error);
        if(input.Action==InteractionAction.Punch)return ExecuteDetachedStemInteraction(eventId,input);
        var g=new ContactGesture{TargetId=eventId,ObjectId=eventId,Input=input,ObjectX=s.X,ObjectZ=s.Z};
        _interactionGesture=g;
        if(input.Action==InteractionAction.Lift)s.HeldByHand=true;
        g.Last=new(true,input.Action switch
        {
            InteractionAction.BottleStem=>"持续接触这一窝，完成装瓶才消耗培养瓶。",
            InteractionAction.Drink=>"把实际水杯对准培养瓶，持续接触完成补水。",
            InteractionAction.FeedMeal=>"把实际正餐对准培养瓶，持续接触完成补养。",
            InteractionAction.Lift=>"已低托培养瓶，沿干燥空地慢慢移动；松开或取消会在当前合法位置放稳。",
            InteractionAction.ClearStem=>"保持接触收起残留；这窝的出生或失败记录会保留。",
            InteractionAction.UnbottleStem=>"护理用品已对准瓶中活窝，保持接触 1.6 秒完成撤出；松开或移开可中止。",
            _=>"火焰已接近这一窝，保持接触才会持续损伤。"
        },"",DetachedStemPose(input.Action));return g.Last;
    }
    private static string DetachedStemPose(InteractionAction action)=>action switch
    {
        InteractionAction.BottleStem=>"stem-bottle",InteractionAction.Drink=>"stem-water",InteractionAction.FeedMeal=>"stem-feed",
        InteractionAction.Lift=>"stem-carry",InteractionAction.Punch=>"stem-hit",InteractionAction.ClearStem=>"stem-clear",
        InteractionAction.UnbottleStem=>"stem-unbottle",_=>"stem-burn"
    };
    private InteractionFeedback ContinueDetachedStemGesture(ContactGesture g,InteractionInput input,bool inContact)
    {
        if(input.TargetKind!=InteractionTargetKind.DetachedStem||input.ObjectId!=g.ObjectId||input.Action!=g.Input.Action||
            input.SupplyObjectId!=g.Input.SupplyObjectId)
        {EndInteractionGesture(true);return Unavailable("离体茎、工具或实际食水已经改变，重新对准后再操作。");}
        var error=ValidateDetachedStemInput(g.TargetId,input);
        if(error!=null){EndInteractionGesture(true);return Unavailable(error);}
        var s=DetachedStem(g.TargetId)!;
        if(g.Applied&&input.Action is InteractionAction.BottleStem or InteractionAction.Drink or InteractionAction.FeedMeal or InteractionAction.UnbottleStem)
            return g.Last with {Changed=false};
        double dt=Math.Min(.1,input.Duration);if(dt<=0)return g.Last;
        g.Elapsed+=dt;g.Input=input;
        if(input.Action==InteractionAction.Lift)
        {
            double dx=input.X-g.ObjectX,dz=input.Z-g.ObjectZ,length=Math.Sqrt(dx*dx+dz*dz);
            double step=Math.Min(length,dt*6);
            int slices=Math.Max(1,(int)Math.Ceiling(step/.06));
            for(int n=0;n<slices&&length>.00001;n++)
            {
                double x=g.ObjectX+dx/length*step/slices,z=g.ObjectZ+dz/length*step/slices;
                if(!CanPlaceDetachedStem(s.RegionId,x,z,s.EventId))break;
                g.ObjectX=x;g.ObjectZ=z;s.X=x;s.Z=z;g.Motion+=step/slices;
            }
            return g.Last with {Changed=g.Motion>0};
        }
        g.ContactActive=inContact;
        if(!inContact){g.ContactElapsed=0;return g.Last with {Changed=false,Message="已经移开，连续接触计时重新开始。"};}
        error=DetachedStemActionError(s,input);
        if(error!=null){EndInteractionGesture(true);return Unavailable(error);}
        g.ContactElapsed+=dt;
        if(input.Action==InteractionAction.Burn)
        {
            double amount=dt*(2+input.Force*7);DamageDetachedStem(s,amount,"人类灼烧离体茎","player",true);
            g.Applied=true;return g.Last=new(true,s.Phase==DetachedStemPhase.Failed?s.LastEvent:"火焰持续损伤茎体和培养瓶。","",s.Phase==DetachedStemPhase.Failed?"stem-loss":"stem-burn",Changed:true,Effect:amount);
        }
        if(g.ContactElapsed+1e-9<1.6)return g.Last;
        if(input.Action==InteractionAction.UnbottleStem)
        {
            // Only remove the vessel. The same living brood keeps every accrued
            // injury, need, cause, parent, child count and maturation second.
            State.Medicine--;s.Phase=DetachedStemPhase.Loose;s.VesselIntegrity=0;
            s.LastEvent="已用一包护理用品将活窝撤出，留在原来的干燥位置；旧瓶不退库存。伤势与食水需求保留，离瓶后需要及时照护。";
        }
        else if(input.Action==InteractionAction.ClearStem)
        {
            s.Cleared=true;s.LastEvent=s.Phase==DetachedStemPhase.Born?"出生后的空瓶残留已收起，家谱和出生记录保留。":
                "失败的茎生残留已收起，原来的失败原因和生命记录保留。";
        }
        else if(input.Action==InteractionAction.BottleStem)
        {
            State.StemVessels--;s.Phase=DetachedStemPhase.Bottled;s.VesselIntegrity=100;
            s.LastEvent="离体茎已移入培养瓶；水和营养继续消耗，需要真实补给。";
        }
        else
        {
            var source=DetachedStemSupply(s,input)!;source.Portions--;
            if(input.Action==InteractionAction.Drink)s.Hydration=Clamp100(s.Hydration+45);
            else s.Nutrition=Clamp100(s.Nutrition+45);
            s.LastEvent=input.Action==InteractionAction.Drink?"实际水杯的一份水已加入培养瓶。":"实际正餐的一份营养已加入培养瓶。";
        }
        g.Applied=true;g.CompletionReleased=true;
        return g.Last=new(true,s.LastEvent,"",DetachedStemPose(input.Action),Changed:true);
    }
    private InteractionFeedback EndDetachedStemGesture(ContactGesture g,bool cancelled)
    {
        var s=DetachedStem(g.TargetId);if(s==null)return g.Last;
        if(g.Input.Action==InteractionAction.Lift)
        {
            bool moved=g.Motion>0;
            if(moved)s.LastEvent="培养瓶已沿实际空位移动，并在当前最后合法位置放稳。";
            s.HeldByHand=false;
            return g.Last with {Message=moved?s.LastEvent:"已在当前合法位置放下培养瓶。",Pose="idle",Changed=moved};
        }
        return g.Last with {Pose="idle",Changed=false};
    }
    private InteractionFeedback ExecuteDetachedStemInteraction(string eventId,InteractionInput input)
    {
        var error=ValidateDetachedStemInput(eventId,input);if(error!=null)return Unavailable(error);
        if(input.Action==InteractionAction.Punch&&HasInteractionGesture)EndInteractionGesture(true);
        var s=DetachedStem(eventId)!;error=DetachedStemActionError(s,input);if(error!=null)return Unavailable(error);
        if(input.Action!=InteractionAction.Punch)return Unavailable("这个离体茎动作需要真实持续接触。");
        DamageDetachedStem(s,5+input.Force*15,"人类击打离体茎和培养瓶","player",true);
        return new(true,s.Phase==DetachedStemPhase.Failed?s.LastEvent:"这一窝受到击打，茎体和瓶体损伤已经保留。","",
            s.Phase==DetachedStemPhase.Failed?"stem-loss":"stem-hit",Changed:true);
    }
}
