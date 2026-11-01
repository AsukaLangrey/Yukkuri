using Godot;
using System;

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
    private bool ActivityPanelVisible=>_activityPanel!=null&&_activityPanel.Visible;
    private bool ActivityContainsPoint(Vector2 point)=>ActivityPanelVisible&&_activityPanel.GetGlobalRect().HasPoint(point);

    private Button PanelButton(string value,Action action,bool active=false)
    {
        var button=Button(value,action,active);
        button.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        button.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        button.CustomMinimumSize=new(100,38);
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
        var control=_inlineHintControl;
        if(control!=null&&GodotObject.IsInstanceValid(control)&&control.IsVisibleInTree())
            return control.GetMeta("inline_hint").AsString();
        return _sceneHoverHint.Length>0?_sceneHoverHint:_message;
    }

    private void BuildActivityPanel()
    {
        _activityPanel=Panel(_ui,"f3f0e6");_activityPanel.Name="GameplaySidebar";
        _activityPanel.AnchorLeft=1;_activityPanel.AnchorRight=1;_activityPanel.AnchorBottom=1;
        _activityPanel.OffsetLeft=-454;_activityPanel.OffsetRight=-14;
        _activityPanel.OffsetTop=88;_activityPanel.OffsetBottom=-218;_activityPanel.Visible=false;
        var column=Column(_activityPanel,8);
        var heading=Row(column,5);
        _panelTitle=Text("",19);_panelTitle.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;heading.AddChild(_panelTitle);
        heading.AddChild(Button("刷新",()=>_refreshActivityPanel?.Invoke()));
        heading.AddChild(Button("收起",CloseActivityPanel));
        column.AddChild(Text("可同时查看小院 · 空格暂停 / 继续",12,"78816e"));
        _panelScroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical=Control.SizeFlags.ExpandFill,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        column.AddChild(_panelScroll);
        _panelContents=Column(_panelScroll,10);_panelContents.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
    }

    private void OpenActivityPanel(string title,Action refresh)
    {
        CancelGesture();
        if(_modalOpen)CloseModal();
        if(_debugPanel!=null)_debugPanel.Visible=false;
        _rightPanel.Visible=false;
        _activityPanel.Visible=true;_panelTitle.Text=title;_refreshActivityPanel=refresh;
        if(_activityTitle!=title)_panelScroll.ScrollVertical=0;
        _activityTitle=title;
        _activityPanel.OffsetLeft=-Math.Min(440,Math.Max(300,_ui.Size.X*.34f))-14;
        ClearChildren(_panelContents);
        // Opening or rebuilding gameplay UI must never change the player's pause choice.
    }

    private void CloseActivityPanel()
    {
        if(!ActivityPanelVisible)return;
        var focus=GetViewport().GuiGetFocusOwner();
        if(focus!=null&&_activityPanel.IsAncestorOf(focus))focus.ReleaseFocus();
        _activityPanel.Visible=false;_refreshActivityPanel=null;_activityTitle="";
        _rightPanel.Visible=!DebugPanelVisible;
        RefreshRoomUi();
    }
}
