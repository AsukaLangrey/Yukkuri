using Godot;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private TextureRect _recycleIcon=null!;
    private Tween? _recycleTween;
    private double _recycleReceipt;
    private int _recycleSuccessCount;
    private void LayoutRecycleBox()
    {
        var canvas=GameUiLayout.Canvas(_ui);
        _recycleBox.Position=new(canvas.Position.X+16,canvas.End.Y-140);
        _recycleBox.Size=new(148,120);
    }
    private string? RecycleRejection(string id)=>_sceneDragKind=="accessory"?"饰品请归还原主，不能连同原主回收。":Sim.CourtyardRecycleReason(id);
    private void RefreshRecycleDrop(Vector2 point)
    {
        if(!_recycleReady)return;
        string? id=_sceneDragId??_heldSupplyId??Sim.InteractionGestureObject??Sim.InteractionGestureTarget;
        string? error=id==null?"没有正在抓取的对象。":RecycleRejection(id);
        bool hover=_recycleBox.GetGlobalRect().HasPoint(point);
        _recycleLabel.Text=error!=null?(hover?"暂不能收纳":"回收箱 · 不可收纳"):
            $"拖入收纳 · {Sim.State.Creatures.Count(c=>c.Family.ColdStorage.Stored)}/12";
        _recycleBox.TooltipText=error??"个体进入回收箱；物品回仓，不退款";
        _recycleBox.Modulate=new Color(hover?(error==null?"b7e8cb":"edb8aa"):"ffffff");
    }
    private void PlayRecycleReceipt()
    {
        _recycleSuccessCount++;_recycleReceipt=1.1;
        _recycleBox.Visible=true;_recycleBox.Modulate=Colors.White;
        _recycleLabel.Text=$"已收纳 ✓ · {Sim.State.Creatures.Count(c=>c.Family.ColdStorage.Stored)}/12";
        _recycleTween?.Kill();_recycleIcon.PivotOffset=_recycleIcon.Size/2;
        _recycleIcon.Scale=Vector2.One;_recycleIcon.Rotation=0;
        _recycleTween=CreateTween();
        // Open/receive, compress, then settle. Only committed storage calls this.
        _recycleTween.TweenProperty(_recycleIcon,"rotation",-.20f,.12);
        _recycleTween.Parallel().TweenProperty(_recycleIcon,"scale",new Vector2(1.18f,.86f),.12);
        _recycleTween.TweenProperty(_recycleIcon,"scale",new Vector2(.88f,1.14f),.16);
        _recycleTween.Parallel().TweenProperty(_recycleIcon,"rotation",.10f,.16);
        _recycleTween.TweenProperty(_recycleIcon,"scale",Vector2.One,.25).SetTrans(Tween.TransitionType.Back);
        _recycleTween.Parallel().TweenProperty(_recycleIcon,"rotation",0f,.25);
    }
    private void UpdateRecycleReceipt(float dt)
    {
        _recycleReceipt=System.Math.Max(0,_recycleReceipt-dt);
        _recycleBox.Visible=!_modalOpen&&!Sim.State.Settings.Pause&&_windowFocused&&(_recycleReady||_recycleReceipt>0);
        if(!_recycleBox.Visible){_recycleBox.Modulate=Colors.White;}
    }
}
