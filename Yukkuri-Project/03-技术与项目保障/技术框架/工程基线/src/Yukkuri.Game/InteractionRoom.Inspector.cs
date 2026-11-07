using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private VBoxContainer _rightBody=null!,_objectPage=null!,_activityActions=null!;
    private Control _inspectorFooter=null!,_activityHeading=null!;
    private ScrollContainer _inspectorActionScroll=null!;
    private readonly List<(Control Source,VBoxContainer Actions)> _inspectorActionGroups=new();
    private Button _propertiesTab=null!,_eventsTab=null!,_fullscreenButton=null!;
    private Container _inspectorTabs=null!;
    private bool _refreshingInspectorTabs;
    private string? _lastInspectorObjectId;
    private Label? _recordTabText;
    private ulong _recordRefreshAt;
    private string? _focusSelectionId,_locatedCreatureId;
    private string _focusButtonIcon="";
    private Window.ModeEnum _previousWindowMode=Window.ModeEnum.Windowed;
    private Vector2I _previousWindowSize,_previousWindowPosition;

    private void BuildInspectorTabs(Control parent)
    {
        var tabs=new GridContainer{Columns=3,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        tabs.AddThemeConstantOverride("h_separation",8);tabs.AddThemeConstantOverride("v_separation",4);parent.AddChild(tabs);
        _inspectorTabs=tabs;
        _propertiesTab=PanelButton("属性",CloseActivityPanel);tabs.AddChild(_propertiesTab);
        _eventsTab=PanelButton("事件",ShowRoomRecords);tabs.AddChild(_eventsTab);
        _familyPanelButton=PanelButton("家庭",ShowFamilyDetails);tabs.AddChild(_familyPanelButton);
        foreach(var button in tabs.GetChildren().OfType<Button>())
        {button.CustomMinimumSize=new(0,32);button.AddThemeFontSizeOverride("font_size",14);}
        RefreshInspectorTabs();
    }
    private bool InspectingRecycleBox=>ActivityPanelVisible&&_activityTitle=="回收箱";
    private string? InspectorObjectId => InspectingRecycleBox?"recycle-box": _selectedShelterId??_selectedSupplyStationId??_selectedDetachedStemEventId??_selectedRoomObjectId??(_observedStemMotherId is { } mother?Sim.StemCareStatus(mother)?.EventId:Sim.Selected?.Id);
    private Creature? InspectorCreature => !InspectingRecycleBox&& _selectedShelterId==null&&_selectedSupplyStationId==null&&_selectedDetachedStemEventId==null&&_selectedRoomObjectId==null&&_observedStemMotherId==null?Sim.Selected:null;
    private bool InspectorHasFamily(Creature c)=>c.ParentId!=null||c.Family.MotherId!=null||c.Family.FatherId!=null||
        c.Family.PartnerIds.Count>0||c.Family.CaregiverIds.Count>0||Sim.State.KnownCreatures.Any(other=>other.Family.MotherId==c.Id||other.Family.FatherId==c.Id||other.ParentId==c.Id||other.Family.CaregiverIds.Contains(c.Id));
    private void RefreshInspectorTabs()
    {
        if(_propertiesTab==null||_refreshingInspectorTabs)return;
        _refreshingInspectorTabs=true;
        try
        {
            bool selected=InspectorObjectId!=null;
            bool events=selected&&RoomRecordsText().Length>0;
            bool family=InspectorCreature is { } c&&InspectorHasFamily(c);
            _propertiesTab.Visible=selected;_eventsTab.Visible=events;_familyPanelButton.Visible=family;_familyPanelButton.Disabled=!family;
            _inspectorTabs.Visible=(selected?1:0)+(events?1:0)+(family?1:0)>1;
            if(ActivityPanelVisible&&(_activityTitle=="事件记录"&&!events||_activityTitle=="家庭记录"&&!family||_activityTitle=="能力与分工"))CloseActivityPanel();
            SetQuickToolHighlight(_propertiesTab,!ActivityPanelVisible);
            SetQuickToolHighlight(_eventsTab,ActivityPanelVisible&&_activityTitle=="事件记录");
            SetQuickToolHighlight(_familyPanelButton,ActivityPanelVisible&&_activityTitle=="家庭记录");
        }
        finally{_refreshingInspectorTabs=false;}
    }
    private void DeselectInspector()
    {
        CancelGesture();ClearShelterSelection();ClearSupplyStationSelection();
        _selectedRoomObjectId=_selectedDetachedStemEventId=_observedStemMotherId=null;
        Sim.State.SelectedId=null;Sim.State.Mode=PlayMode.Management;_locatedCreatureId=null;
        if(ActivityPanelVisible&&_activityTitle is "事件记录" or "家庭记录")CloseActivityPanel();
        RefreshRoomUi();
    }
    private void FocusSelectedCreature()
    {
        if(Sim.Selected is not { } target||!HasVisibleFamilyBody(target)||_closeButton.Disabled)return;
        if(Sim.State.Mode==PlayMode.CloseUp){ToggleClose();return;}
        if(_locatedCreatureId==target.Id){ToggleClose();return;}
        CancelGesture();
        _yardFocus=new((float)target.X,0,(float)target.Z);
        _locatedCreatureId=target.Id;
        RefreshRoomUi();
    }
    private void MoveInspectorActions(Control source,Control destination)
    {
        var group=Column(destination,4);
        foreach(var child in source.GetChildren().ToArray())
            if(child is Godot.Button||child is InlineChoice)child.Reparent(group);
        _inspectorActionGroups.Add((source,group));
    }
    private void RefreshInspectorLayout()
    {
        if(_selectedRoomObjectId is { } prop&&!Sim.State.RoomObjects.Any(o=>o.Id==prop&&!o.Stored&&o.RegionId==Sim.State.ActiveRegionId))_selectedRoomObjectId=null;
        if(_lastInspectorObjectId!=InspectorObjectId)
        {
            _lastInspectorObjectId=InspectorObjectId;_recordRefreshAt=0;
            if(_recordTabText!=null&&GodotObject.IsInstanceValid(_recordTabText))_recordTabText.Text=_activityTitle=="事件记录"?RoomRecordsText():FamilyRecordsText();
        }
        RefreshInspectorTabs();
        bool show=InspectorObjectId!=null||ActivityPanelVisible||DebugPanelVisible;
        _rightPanel.Visible=show&&!DebugPanelVisible;_ui.SetMeta("right_sidebar_hidden",!show);GameUiLayout.Right(_rightPanel);
        foreach(var (source,actions) in _inspectorActionGroups)
            actions.Visible=source.Visible&&actions.GetChildren().OfType<Control>().Any(c=>c.Visible);
        var contents=(Control)_inspectorActionScroll.GetChild(0);
        float height=contents.GetCombinedMinimumSize().Y;
        _inspectorActionScroll.CustomMinimumSize=new(0,Mathf.Min(height,Mathf.Max(80,_ui.Size.Y*.24f)));
        _inspectorFooter.Visible=_objectActions.Visible&&_objectActions.GetChildCount()>0||
            _inspectorActionGroups.Any(group=>group.Actions.Visible);
        _closeButton.Visible=InspectorCreature!=null;
        if(InspectingRecycleBox){_selectionName.Text="回收箱";_inspectorTabs.Visible=false;}
        else if(InspectorObjectId!=null)_selectionName.Text=_who.Text;
        if(_closeButton.Disabled)_locatedCreatureId=null;
        bool close=Sim.State.Mode==PlayMode.CloseUp;
        string icon=!close&&_locatedCreatureId==Sim.Selected?.Id?"eye":"focus";
        if(icon!=_focusButtonIcon){UiActionIcons.Apply(_closeButton,icon);_focusButtonIcon=icon;}
        SetQuickToolHighlight(_closeButton,close);
        AutoRefreshActivity();
        if(_recordTabText==null||!GodotObject.IsInstanceValid(_recordTabText)||!ActivityPanelVisible)return;
        if(Time.GetTicksMsec()<_recordRefreshAt)return;
        _recordRefreshAt=Time.GetTicksMsec()+1000;
        if(_activityTitle=="事件记录")_recordTabText.Text=RoomRecordsText();
        if(_activityTitle=="家庭记录")_recordTabText.Text=FamilyRecordsText();
    }
    private string RoomRecordsText()
    {
        if(InspectorObjectId is not { } id)return "";
        var lines=new List<string>();
        if(InspectorCreature is { } c)lines.AddRange(c.PersonalMemories.Select(m=>m.Text));
        lines.AddRange(Sim.EventsFor(id));
        // Existing object-local fields carry proven identity. Global journal/history does not.
        if(_selectedShelterId is { } shelter&&Sim.State.Shelters.Find(o=>o.Id==shelter) is { } house&&house.LastEvent!="空屋，入口朝向小院前方。")lines.Add(house.LastEvent);
        if(_selectedSupplyStationId is { } station&&Sim.State.SupplyStations.Find(o=>o.Id==station) is { } supply&&supply.LastEvent!="空仓，先补充实际食水。")lines.Add(supply.LastEvent);
        if(_selectedDetachedStemEventId is { } stem&&Sim.State.DetachedStemBroods.Find(o=>o.EventId==stem) is { } brood)lines.Add(brood.LastEvent);
        return string.Join("\n\n",lines.Where(l=>!string.IsNullOrWhiteSpace(l)).Distinct().Reverse());
    }
    private string FamilyRecordsText()=>InspectorCreature is { } c?FamilySummary(c):"";
    private void ToggleFullscreen()
    {
        CancelGesture();
        var window=GetWindow();
        if(window.Mode is Window.ModeEnum.Fullscreen or Window.ModeEnum.ExclusiveFullscreen)
        {
            window.Mode=_previousWindowMode;
            if(_previousWindowMode==Window.ModeEnum.Windowed&&_previousWindowSize.X>0)
            {window.Size=_previousWindowSize;window.Position=_previousWindowPosition;}
        }
        else
        {
            _previousWindowMode=window.Mode;
            _previousWindowSize=window.Size;_previousWindowPosition=window.Position;
            window.Mode=Window.ModeEnum.Fullscreen;
        }
        RefreshRoomUi();
    }
}
