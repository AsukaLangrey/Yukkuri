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
        id??=InspectorCreature?.Id;
        OpenActivityPanel("能力记录",()=>ShowDevelopment());
        if(id!=null)_panelContents.AddChild(Text(Sim.AbilitySummary(id),13));
    }
    private void RefreshDevelopmentUi()
    {
        if(_lifeOverview!=null)
        {
            bool creature=Sim.Selected!=null&&_selectedRoomObjectId==null&&_selectedSupplyStationId==null&&_selectedShelterId==null&&_selectedDetachedStemEventId==null&&_observedStemMotherId==null;
            var selected=Sim.Selected;
            var state=creature&&selected!=null?Sim.ExplainLifeActivity(selected.Id):null;
            _lifeOverview.Visible=state!=null&&state.Goal!="按自己的需要生活";
            if(_lifeOverview.Visible&&state!=null)
            {
                _lifeOverview.Text=$"{state.Goal}\n{state.Reason}";
            }
        }
        if(!ActivityPanelVisible||_activityTitle!="能力与分工")return;
        if(_developmentCreatureId!=Sim.Selected?.Id){ShowDevelopment();return;}
        var c=Sim.State.Creatures.Find(other=>other.Id==_developmentCreatureId);
        if(c==null)return;
        if(_developmentSummary!=null&&GodotObject.IsInstanceValid(_developmentSummary))_developmentSummary.Text=Sim.AbilitySummary(c.Id);
        if(_developmentDuty!=null&&GodotObject.IsInstanceValid(_developmentDuty))_developmentDuty.Text=Sim.DutySummary(c.Id);
        if(_developmentHistory!=null&&GodotObject.IsInstanceValid(_developmentHistory))
            _developmentHistory.Text=c.Interaction.Development.Recent.Count==0?"尚无新记录。":string.Join("\n",c.Interaction.Development.Recent.AsEnumerable().Reverse().Select(e=>$"{e.At:0}秒 · {LifeDevelopmentRules.SkillName(e.Skill)}：{e.Description}"));
    }
    private string BodyOperationPreview(Contact contact)
    {
        if(!_toolArmed)return "";
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
        if(!accessible)return "接触受阻";
        if(_roomMode==RoomMode.Teaching)return Sim.CleaningLessonReason(contact.Id)??"示范清洁";
        if(_roomMode is RoomMode.Cold or RoomMode.Bury or RoomMode.Breeding or RoomMode.Induce or RoomMode.Suppress or RoomMode.Juice or RoomMode.Company)
            return Toolbar.SelectMany(t=>t.Entries).FirstOrDefault(e=>e.Mode==_roomMode)?.Name??"互动";
        return Sim.PreviewBodyInteraction(contact.Id,new(action,contact.Part,Force:_force,Side:contact.Side));
    }
    private void RefreshOperationPreview(bool scene,Contact? contact,Vector2 point)
    {
        bool visible=scene&&!_mouseHeld&&!_waterRescueToolArmed&&contact is {TargetKind:InteractionTargetKind.Body};
        _actionPreview.Visible=visible;
        if(!visible)return;
        string hint=BodyOperationPreview(contact!);
        if(hint.Length==0){_actionPreview.Visible=false;return;}
        _actionPreview.Text=ContactName(contact!)+" · "+hint;
        _sceneHoverHint=_actionPreview.Text;
        var local=point-_canvasEffects.Position+new Vector2(18,18);
        _actionPreview.Position=new(Mathf.Clamp(local.X,4,Mathf.Max(4,_canvasEffects.Size.X-264)),Mathf.Clamp(local.Y,4,Mathf.Max(4,_canvasEffects.Size.Y-_actionPreview.Size.Y-8)));
    }
}
