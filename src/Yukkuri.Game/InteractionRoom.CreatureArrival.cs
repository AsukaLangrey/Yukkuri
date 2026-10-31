using Godot;
using System.Collections.Generic;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private CreatureArrival? _arrivingCreature;
    private Node3D? _arrivalPreview;
    private readonly List<MeshInstance3D> _arrivalLines=new();

    private void ShowCreatureArrival()
    {
        OpenModal("投放新油");
        _modalContents.AddChild(Text($"每只 {WorldSimulation.NewCreaturePrice} 元 · 余额 {Sim.State.Money} 元",20));
        _modalContents.AddChild(Text("选择后点击场景空地。成功放下才扣款，右键取消。新油从陌生开始相处。"));
        _modalContents.AddChild(Text("油种"));
        var species=new OptionButton();species.AddItem("灵梦");species.AddItem("魔理沙");_modalContents.AddChild(species);
        _modalContents.AddChild(Text("成长阶段"));
        var age=new OptionButton();age.AddItem("成体");age.AddItem("幼体 · 需要保暖和照护");_modalContents.AddChild(age);
        _modalContents.AddChild(Text("繁育角色"));
        var role=new OptionButton();role.AddItem("母方");role.AddItem("父方");_modalContents.AddChild(role);
        var name=new LineEdit{PlaceholderText="名字（可留空）",MaxLength=24};_modalContents.AddChild(name);
        var place=Button("选择落点",()=>
        {
            var profile=new CreatureArrival(species.Selected==0?"reimu":"marisa",age.Selected==1,
                role.Selected==0?ReproductionRole.Maternal:ReproductionRole.Paternal,name.Text);
            CloseModal();CancelGesture();ClearShelterSelection();ClearSupplyStationSelection();
            _selectedDetachedStemEventId=_selectedRoomObjectId=_observedStemMotherId=null;
            Sim.State.Mode=PlayMode.Management;_roomMode=RoomMode.Observe;_toolbarCategory=0;
            _action=InteractionAction.Inspect;_group=InteractionCatalog.Group(_action);_directToolMode=false;
            _arrivingCreature=profile;_toolArmed=true;RebuildToolbarChoices();
            _message="点击干燥空地投放一只新油，右键取消；成功放下才扣款。";RefreshRoomUi();
        });
        place.Disabled=Sim.State.Money<WorldSimulation.NewCreaturePrice;_modalContents.AddChild(place);
        _modalContents.AddChild(Button("返回小院",CloseModal));
    }
    private void CancelCreatureArrival()
    {_arrivingCreature=null;if(_arrivalPreview!=null)_arrivalPreview.Visible=false;}

    private void UpdateCreatureArrivalPreview()
    {
        var pointer=GetViewport().GetMousePosition();
        if(_arrivingCreature is not { } profile||!_windowFocused||_modalOpen||_panning||_orbiting||
            Sim.State.Settings.Pause||Sim.State.Mode==PlayMode.CloseUp||!IsScenePoint(pointer)||!TryGroundPoint(pointer,0,out var point))
        {if(_arrivalPreview!=null)_arrivalPreview.Visible=false;return;}
        if(_arrivalPreview==null)
        {
            _arrivalPreview=new Node3D();AddChild(_arrivalPreview);
            foreach(float sign in new[]{-1f,1f})
            {
                _arrivalLines.Add(SceneArt.Box(_arrivalPreview,new(sign,0,0),new(.02f,.008f,2),"899e90"));
                _arrivalLines.Add(SceneArt.Box(_arrivalPreview,new(0,0,sign),new(2,.008f,.02f),"899e90"));
            }
            foreach(var line in _arrivalLines)line.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
        }
        float radius=(float)InteractionFixtureGeometry.StandingFootprint*(profile.Young?.73f:1);
        _arrivalPreview.Scale=new(radius,1,radius);_arrivalPreview.Position=point+Vector3.Up*.02f;_arrivalPreview.Visible=true;
        bool valid=ContactAt(pointer)==null&&ObjectAt(pointer)==null&&Sim.NewCreaturePlacementReason(profile,point.X,point.Z)==null;
        foreach(var line in _arrivalLines)line.MaterialOverride=SceneArt.Mat(valid?"899e90":"b7806c");
    }
    private bool PlaceNewCreatureAt(Vector2 pointer)
    {
        if(_arrivingCreature is not { } profile)return false;
        if(ContactAt(pointer)!=null||ObjectAt(pointer)!=null)
        {_message="请点击干燥空地，避开身体、物品与建筑。";return true;}
        if(!TryGroundPoint(pointer,0,out var point)){_message="这个视角看不到地面落点。";return true;}
        var reply=Sim.PlaceNewCreature(profile,point.X,point.Z);
        if(reply.Success)
        {CancelCreatureArrival();_toolArmed=false;SynchronizeFamilyActors();SelectOil(Sim.State.SelectedId!);}
        AcceptFeedback(reply);RefreshRoomUi();return true;
    }
}
