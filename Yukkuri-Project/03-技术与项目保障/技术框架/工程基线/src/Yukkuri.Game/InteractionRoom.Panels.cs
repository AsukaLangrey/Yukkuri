using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private PanelContainer _activityPanel=null!;
    private VBoxContainer _panelContents=null!;
    private Label _panelTitle=null!;
    private ScrollContainer _panelScroll=null!;
    private Action? _refreshActivityPanel;
    private string _activityTitle="";
    private Control? _inlineHintControl;
    private string _sceneHoverHint="";
    private readonly Dictionary<string,int> _pageScroll=new();
    private readonly Dictionary<string,Dictionary<string,Variant>> _pageInputs=new();
    private int _pageGeneration;
    private bool _autoRefreshingActivity;
    private ulong _nextActivityRefresh;
    private readonly CanvasNoticeSource _eventSource=new();
    private bool ActivityPanelVisible=>_activityPanel!=null&&_activityPanel.Visible;
    private bool ActivityContainsPoint(Vector2 point)=>ActivityPanelVisible&&_activityPanel.GetGlobalRect().HasPoint(point);

    private Button PanelButton(string value,Action action,bool active=false)
    {
        var button=Button(value,action,active);
        button.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        button.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        button.CustomMinimumSize=new(100,32);
        return button;
    }

    private void SetInlineHint(Control control,string text)
    {
        if(!control.HasMeta("inline_hint"))
        {
            control.MouseEntered+=()=>{_inlineHintControl=control;RefreshInlineFeedback();};
            control.MouseExited+=()=>{if(_inlineHintControl==control){_inlineHintControl=null;RefreshInlineFeedback();}};
        }
        control.SetMeta("inline_hint",text);
    }

    private string _lastInlineMessage="";
    private ulong _inlineMessageUntil;
    private void RefreshInlineFeedback()
    {
        _feedback.Text=CurrentInlineFeedback();
        _feedback.Visible=_feedback.Text.Length>0;
    }
    private string CurrentInlineFeedback()
    {
        ulong now=Time.GetTicksMsec();
        if(_message!=_lastInlineMessage){_lastInlineMessage=_message;_inlineMessageUntil=now+5000;}
        if(now<_actionResultUntil&&_message.Length>0)return _message;
        var control=_inlineHintControl;
        if(control!=null&&GodotObject.IsInstanceValid(control)&&control.IsVisibleInTree())
            return control.GetMeta("inline_hint").AsString();
        if(_sceneHoverHint.Length>0&&(_actionPreview==null||!_actionPreview.Visible))return _sceneHoverHint;
        return now<_inlineMessageUntil?_message:"";
    }

    private void BuildActivityPanel()
    {
        _activityPanel=Panel(_rightBody,"f3f0e6");_activityPanel.Name="GameplaySidebar";
        _activityPanel.AddThemeStyleboxOverride("panel",RoomStyle("f3f0e6",6,0));
        _activityPanel.SizeFlagsVertical=Control.SizeFlags.ExpandFill;_activityPanel.Visible=false;
        var column=Column(_activityPanel,6);
        var heading=Row(column,5);
        _activityHeading=heading;
        _panelTitle=Text("",19);_panelTitle.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;heading.AddChild(_panelTitle);
        _panelScroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical=Control.SizeFlags.ExpandFill,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        column.AddChild(_panelScroll);
        _panelContents=Column(_panelScroll,6);_panelContents.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        _activityActions=Column(column,4);
    }

    private void OpenActivityPanel(string title,Action refresh)
    {
        bool returning=_activityTitle!=title||!ActivityPanelVisible;
        if(returning)RememberActivityPage();
        if(!_autoRefreshingActivity)
        {
            bool rescueTool=_waterRescueToolArmed;CancelGesture();_waterRescueToolArmed=rescueTool;
            if(_modalOpen)CloseModal();
        }
        if(_debugPanel!=null)_debugPanel.Visible=false;
        _ui.SetMeta("right_sidebar_hidden",false);GameUiLayout.Right(_rightPanel);
        _rightPanel.Visible=true;_objectPage.Visible=false;
        _activityPanel.Visible=true;_panelTitle.Text=title;_refreshActivityPanel=refresh;
        if(returning)_panelScroll.ScrollVertical=0;
        _activityTitle=title;
        _activityHeading.Visible=title is not ("事件记录" or "家庭记录" or "能力与分工" or "回收箱");
        ClearChildren(_activityActions);_recordTabText=null;
        RefreshInspectorTabs();
        ClearChildren(_panelContents);
        int generation=++_pageGeneration;
        if(returning||_autoRefreshingActivity)Callable.From(()=>
        {
            if(generation!=_pageGeneration||!ActivityPanelVisible)return;
            if(_pageInputs.TryGetValue(title,out var inputs))
            {
                int index=0;foreach(var input in PageInputs(_panelContents))
                {
                    string key=input.GetType().Name+":"+index++;
                    if(!inputs.TryGetValue(key,out var value))continue;
                    if(input is SpinBox number)number.Value=value.AsDouble();
                    else if(input is LineEdit edit)edit.Text=value.AsString();
                    else if(input is InlineChoice choice&&!choice.HasMeta("identity_selection"))choice.Select(value.AsInt32());
                }
            }
            if(_pageScroll.TryGetValue(title,out var scroll))_panelScroll.ScrollVertical=scroll;
        }).CallDeferred();
        // Opening or rebuilding gameplay UI must never change the player's pause choice.
    }

    private void AutoRefreshActivity()
    {
        if(!ActivityPanelVisible||_autoRefreshingActivity||_modalOpen||
            _activityTitle is "事件记录" or "家庭记录" or "能力与分工"||
            Time.GetTicksMsec()<_nextActivityRefresh)return;
        if(Input.IsMouseButtonPressed(MouseButton.Left)||Input.IsMouseButtonPressed(MouseButton.Right))return;
        var focus=GetViewport().GuiGetFocusOwner();
        if(focus is LineEdit or TextEdit&&_activityPanel.IsAncestorOf(focus))return;
        _nextActivityRefresh=Time.GetTicksMsec()+1000;
        RememberActivityPage();
        _autoRefreshingActivity=true;
        try{_refreshActivityPanel?.Invoke();}
        finally{_autoRefreshingActivity=false;}
    }

    private void CloseActivityPanel()
    {
        if(!ActivityPanelVisible)return;
        RememberActivityPage();++_pageGeneration;
        var focus=GetViewport().GuiGetFocusOwner();
        if(focus!=null&&_activityPanel.IsAncestorOf(focus))focus.ReleaseFocus();
        _activityPanel.Visible=false;_refreshActivityPanel=null;_activityTitle="";
        _rightPanel.Visible=!DebugPanelVisible;
        _objectPage.Visible=true;RefreshInspectorTabs();
        RefreshRoomUi();
    }

    private void RememberActivityPage()
    {
        if(!ActivityPanelVisible||_activityTitle.Length==0)return;
        _pageScroll[_activityTitle]=_panelScroll.ScrollVertical;
        var values=new Dictionary<string,Variant>();int index=0;
        foreach(var input in PageInputs(_panelContents))
        {
            string key=input.GetType().Name+":"+index++;
            if(input is SpinBox number)values[key]=number.Value;
            else if(input is LineEdit edit)values[key]=edit.Text;
            else if(input is InlineChoice choice)values[key]=choice.Selected;
        }
        _pageInputs[_activityTitle]=values;
    }
    private static IEnumerable<Control> PageInputs(Node node)
    {
        if(node is SpinBox or LineEdit or InlineChoice){yield return (Control)node;yield break;}
        foreach(var child in node.GetChildren())foreach(var input in PageInputs(child))yield return input;
    }
    private void ResetRoomUiSession()
    {
        _dutyRecipientDrafts.Clear();_developmentCreatureId=null;_actionResultUntil=0;
        _focusSelectionId=null;_locatedCreatureId=null;
        _pageInputs.Clear();_pageScroll.Clear();_shopQuantities.Clear();++_pageGeneration;_eventFeed.ResetSession();_sceneHoverHint="";_inlineHintControl=null;
    }
    private void RefreshRoomEvents()
    {
        _eventFeed.UpdateNotices(Sim.InteractionHelpRequests().Select(help=>new CanvasNotice(
            help.Target.Id+":"+help.Kind,help.Target.Name+"："+help.Description,
            help.Kind is InteractionHelpKind.Critical or InteractionHelpKind.Water?3:help.Kind==InteractionHelpKind.Threat?2:1,help.Target.Id)));
        foreach(var notice in _eventSource.Read(Sim.State))_eventFeed.Publish(notice);
    }
    private void FocusRoomEvent(string id)
    {
        if(_modalOpen)return;
        var target=Sim.State.Creatures.FirstOrDefault(c=>c.Id==id&&c.RegionId==Sim.State.ActiveRegionId);
        if(target==null){_message="对象已不在当前场景，可到记录页查看经过。";return;}
        SelectOil(id);Sim.State.Mode=PlayMode.Management;_yardFocus=new((float)target.X,0,(float)target.Z);
        _message="已定位"+target.Name+"，当前页面与暂停选择保持不变。";RefreshRoomUi();
    }
    private void ShowRoomRecords()
    {
        OpenActivityPanel("事件记录",ShowRoomRecords);
        _recordTabText=Text(RoomRecordsText(),14);_panelContents.AddChild(_recordTabText);
    }
}
