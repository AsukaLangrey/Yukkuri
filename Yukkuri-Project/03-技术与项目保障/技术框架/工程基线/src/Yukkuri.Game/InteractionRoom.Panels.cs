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
            control.MouseEntered+=()=>{_inlineHintControl=control;_feedback.Text=CurrentInlineFeedback();};
            control.MouseExited+=()=>{if(_inlineHintControl==control){_inlineHintControl=null;_feedback.Text=CurrentInlineFeedback();}};
        }
        control.SetMeta("inline_hint",text);
    }

    private string CurrentInlineFeedback()
    {
        if(Time.GetTicksMsec()<_actionResultUntil)return _message;
        var control=_inlineHintControl;
        if(control!=null&&GodotObject.IsInstanceValid(control)&&control.IsVisibleInTree())
            return control.GetMeta("inline_hint").AsString();
        return _sceneHoverHint.Length>0?_sceneHoverHint:_message;
    }

    private void BuildActivityPanel()
    {
        _activityPanel=Panel(_ui,"f3f0e6");_activityPanel.Name="GameplaySidebar";
        GameUiLayout.Right(_activityPanel);_activityPanel.Visible=false;
        var column=Column(_activityPanel,6);
        var heading=Row(column,5);
        _panelTitle=Text("",19);_panelTitle.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;heading.AddChild(_panelTitle);
        heading.AddChild(Button("刷新",()=>_refreshActivityPanel?.Invoke()));
        heading.AddChild(Button("返回对象",CloseActivityPanel));
        column.AddChild(Text("可同时查看小院 · 空格暂停 / 继续",12,"78816e"));
        _panelScroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical=Control.SizeFlags.ExpandFill,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        column.AddChild(_panelScroll);
        _panelContents=Column(_panelScroll,6);_panelContents.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
    }

    private void OpenActivityPanel(string title,Action refresh)
    {
        bool returning=_activityTitle!=title||!ActivityPanelVisible;
        if(returning)RememberActivityPage();
        bool rescueTool=_waterRescueToolArmed;CancelGesture();_waterRescueToolArmed=rescueTool;
        if(_modalOpen)CloseModal();
        if(_debugPanel!=null)_debugPanel.Visible=false;
        _rightPanel.Visible=false;
        _activityPanel.Visible=true;_panelTitle.Text=title;_refreshActivityPanel=refresh;
        if(returning)_panelScroll.ScrollVertical=0;
        _activityTitle=title;
        ClearChildren(_panelContents);
        int generation=++_pageGeneration;
        if(returning)Callable.From(()=>
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
                }
            }
            if(_pageScroll.TryGetValue(title,out var scroll))_panelScroll.ScrollVertical=scroll;
        }).CallDeferred();
        // Opening or rebuilding gameplay UI must never change the player's pause choice.
    }

    private void CloseActivityPanel()
    {
        if(!ActivityPanelVisible)return;
        RememberActivityPage();++_pageGeneration;
        var focus=GetViewport().GuiGetFocusOwner();
        if(focus!=null&&_activityPanel.IsAncestorOf(focus))focus.ReleaseFocus();
        _activityPanel.Visible=false;_refreshActivityPanel=null;_activityTitle="";
        _rightPanel.Visible=!DebugPanelVisible;
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
        }
        _pageInputs[_activityTitle]=values;
    }
    private static IEnumerable<Control> PageInputs(Node node)
    {
        if(node is SpinBox or LineEdit){yield return (Control)node;yield break;}
        foreach(var child in node.GetChildren())foreach(var input in PageInputs(child))yield return input;
    }
    private void ResetRoomUiSession()
    {
        // IDs can be reused by a loaded or restarted world; no pending invitation crosses sessions.
        _companyFirst=null;
        _dutyRecipientDrafts.Clear();_developmentCreatureId=null;_actionResultUntil=0;
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
        OpenActivityPanel("事件与完整记录",ShowRoomRecords);
        _panelContents.AddChild(Text("重点事件 · 本次会话",18));
        foreach(var line in _eventFeed.History.Reverse())_panelContents.AddChild(Text(line,14));
        _panelContents.AddChild(Text("世界记录",18));
        foreach(var line in Sim.State.Journal.AsEnumerable().Reverse())_panelContents.AddChild(Text(line,14));
    }
}
