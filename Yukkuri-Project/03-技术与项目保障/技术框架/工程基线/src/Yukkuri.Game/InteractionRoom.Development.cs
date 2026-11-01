using Godot;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private Label _lifeOverview=null!,_actionPreview=null!;
    private Label? _developmentSummary,_developmentHistory,_developmentDuty;
    private string? _developmentCreatureId;
    private ulong _actionResultUntil;
    private readonly System.Collections.Generic.Dictionary<string,string> _dutyRecipientDrafts=new();

    private void ShowDevelopment(string? id=null)
    {
        id??=Sim.Selected?.Id;_developmentCreatureId=id;
        OpenActivityPanel("能力与分工",()=>ShowDevelopment(id));
        var c=Sim.State.Creatures.Find(other=>other.Id==id);
        if(c==null){_panelContents.AddChild(Text("先选择一只油，再查看能力和安排。"));return;}
        _panelContents.AddChild(Text(c.Name+" · 能力与分工",18));
        _panelContents.AddChild(PanelButton("查看当前选中的油",()=>ShowDevelopment(Sim.Selected?.Id)));
        _developmentDuty=Text("",13);_panelContents.AddChild(_developmentDuty);
        var options=Row(_panelContents,5);
        options.AddChild(PanelButton("自由生活",()=>{_message=Sim.SetLifeDuty(c.Id,LifeDuty.None);ShowDevelopment(c.Id);RefreshRoomUi();}));
        options.AddChild(PanelButton("清洁附近",()=>{_message=Sim.SetLifeDuty(c.Id,LifeDuty.Cleaning);ShowDevelopment(c.Id);RefreshRoomUi();}));
        var children=Sim.State.Creatures.Where(other=>other!=c&&other.IsYoung&&other.RegionId==c.RegionId&&other.Condition!=PhysicalCondition.Dead).ToArray();
        if(!c.IsYoung&&children.Length>0)
        {
            var choice=new InlineChoice();foreach(var child in children)choice.AddItem(child.Name);
            string? draft=_dutyRecipientDrafts.TryGetValue(c.Id,out var remembered)?remembered:c.Interaction.Development.RecipientId;
            int selected=System.Array.FindIndex(children,child=>child.Id==draft);choice.Select(System.Math.Max(0,selected));
            choice.ItemSelected+=index=>_dutyRecipientDrafts[c.Id]=children[(int)index].Id;
            _panelContents.AddChild(choice);
            _panelContents.AddChild(PanelButton("安排照看这只幼体",()=>
            {
                if(choice.Selected<0||choice.Selected>=children.Length)return;
                _message=Sim.SetLifeDuty(c.Id,LifeDuty.Care,children[choice.Selected].Id);ShowDevelopment(c.Id);RefreshRoomUi();
            }));
        }
        _panelContents.AddChild(Text("分工是持续安排，会服从身体需要、意愿和实际通路；一次只安排一项。取消分工仍保留自然的生活与家庭照护。",12,"78816e"));
        _panelContents.AddChild(Text(_message,13));
        _developmentSummary=Text("",13);_panelContents.AddChild(_developmentSummary);
        _panelContents.AddChild(Text("最近能力经历",15));
        _developmentHistory=Text("",12);_panelContents.AddChild(_developmentHistory);
        RefreshDevelopmentUi();
    }
    private void RefreshDevelopmentUi()
    {
        if(_lifeOverview!=null)
        {
            bool creature=Sim.Selected!=null&&_selectedRoomObjectId==null&&_selectedSupplyStationId==null&&_selectedShelterId==null&&_selectedDetachedStemEventId==null&&_observedStemMotherId==null;
            _lifeOverview.Visible=creature;
            if(creature)
            {
                var state=Sim.ExplainLifeActivity(Sim.Selected!.Id);
                _lifeOverview.Text=$"正在：{state.Action}\n目标：{state.Goal}\n{state.Reason}";
            }
        }
        if(!ActivityPanelVisible||_activityTitle!="能力与分工")return;
        var c=Sim.State.Creatures.Find(other=>other.Id==_developmentCreatureId);
        if(c==null)return;
        if(_developmentSummary!=null&&GodotObject.IsInstanceValid(_developmentSummary))_developmentSummary.Text=Sim.AbilitySummary(c.Id);
        if(_developmentDuty!=null&&GodotObject.IsInstanceValid(_developmentDuty))_developmentDuty.Text=Sim.DutySummary(c.Id);
        if(_developmentHistory!=null&&GodotObject.IsInstanceValid(_developmentHistory))
            _developmentHistory.Text=c.Interaction.Development.Recent.Count==0?"尚无新记录。":string.Join("\n",c.Interaction.Development.Recent.AsEnumerable().Reverse().Select(e=>$"{e.At:0}秒 · {LifeDevelopmentRules.SkillName(e.Skill)}：{e.Description}"));
    }
    private string BodyOperationPreview(Contact contact)
    {
        if(!_toolArmed)return "观察：点击查看对象，双击靠近。";
        var c=Sim.State.Creatures.Find(other=>other.Id==contact.Id);
        if(c==null)return "对象已离开，请重新对准。";
        var action=_roomMode switch
        {
            RoomMode.Wrap=>c.Interaction.Restraint==RestraintKind.Wrap?InteractionAction.Rock:InteractionAction.Wrap,
            RoomMode.Frame=>c.Interaction.Restraint==RestraintKind.Frame?InteractionAction.Release:InteractionAction.Pin,
            RoomMode.Box=>c.Interaction.Restraint==RestraintKind.Box?InteractionAction.OpenBox:InteractionAction.Box,
            RoomMode.Hanging=>c.Interaction.Restraint==RestraintKind.Hanging?InteractionAction.AdjustHanging:InteractionAction.Hang,
            RoomMode.Pressure=>contact.Part==BodyPart.Side?InteractionAction.Squeeze:contact.Part==BodyPart.Bottom?InteractionAction.Roll:InteractionAction.Press,
            _=>_action
        };
        bool accessible=!contact.Blocked||contact.Fixture switch
        {RestraintKind.Box=>action==InteractionAction.OpenBox,RestraintKind.Frame=>action==InteractionAction.Release,
            RestraintKind.Wrap=>action is InteractionAction.Unwrap or InteractionAction.Rock,RestraintKind.Hanging=>action is InteractionAction.Lower or InteractionAction.AdjustHanging,_=>false};
        if(!accessible)return ContactName(contact)+"挡住了接触，请先处理限制。";
        if(_roomMode==RoomMode.Teaching)return Sim.CleaningLessonReason(contact.Id)??"可示范清洁：会实际清理身旁一处脏污。";
        if(_roomMode is RoomMode.Cold or RoomMode.Bury or RoomMode.Breeding or RoomMode.Induce or RoomMode.Suppress or RoomMode.Juice or RoomMode.Company)
            return "当前工具："+(Toolbar.SelectMany(t=>t.Entries).FirstOrDefault(e=>e.Mode==_roomMode)?.Name??"互动")+"；点击后按对象状态处理。";
        return Sim.PreviewBodyInteraction(contact.Id,new(action,contact.Part,Force:_force,Side:contact.Side));
    }
    private void RefreshOperationPreview(bool scene,Contact? contact,Vector2 point)
    {
        bool visible=scene&&!_mouseHeld&&!_waterRescueToolArmed&&contact is {TargetKind:InteractionTargetKind.Body};
        _actionPreview.Visible=visible;
        if(!visible)return;
        string hint=BodyOperationPreview(contact!);
        _actionPreview.Text=ContactName(contact!)+" · "+hint;
        _sceneHoverHint=_actionPreview.Text;
        var local=point-_canvasEffects.Position+new Vector2(18,18);
        _actionPreview.Position=new(Mathf.Clamp(local.X,4,Mathf.Max(4,_canvasEffects.Size.X-264)),Mathf.Clamp(local.Y,4,Mathf.Max(4,_canvasEffects.Size.Y-_actionPreview.Size.Y-8)));
    }
}
