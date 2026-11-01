using Godot;
using System.Collections.Generic;
using System.Linq;
using Yukkuri.Simulation;

public partial class Main
{
    private PanelContainer _objectSidebar=null!;
    private CanvasEventFeed _canvasEvents=null!;
    private HBoxContainer _forceParameters=null!;
    private Label _forceSetting=null!;
    private readonly CanvasNoticeSource _eventSource=new();
    private HBoxContainer BuildMainHeader()
    {
        var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",PanelStyle("faf5e9"));_uiRoot.AddChild(panel);GameUiLayout.TopBar(panel);
        var row=new HBoxContainer();row.AddThemeConstantOverride("separation",8);panel.AddChild(row);
        _top=new Label{Text="油库里乐园",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,ClipText=true,
            TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis,MouseFilter=Control.MouseFilterEnum.Ignore};
        _top.AddThemeFontSizeOverride("font_size",15);row.AddChild(_top);return row;
    }
    private VBoxContainer CreateSidebar(bool left)
    {
        GameUiLayout.Configure(GetWindow());
        var panel=new PanelContainer();var style=PanelStyle("faf5e9");
        style.ContentMarginLeft=10;style.ContentMarginRight=10;style.ContentMarginTop=10;style.ContentMarginBottom=10;
        panel.AddThemeStyleboxOverride("panel",style);_uiRoot.AddChild(panel);
        if(left)GameUiLayout.Left(panel);else {GameUiLayout.Right(panel);_objectSidebar=panel;}
        var scroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};panel.AddChild(scroll);
        var contents=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};contents.AddThemeConstantOverride("separation",4);scroll.AddChild(contents);
        return contents;
    }
    private void BuildCanvasEvents()
    {
        _canvasEvents=new CanvasEventFeed();_uiRoot.AddChild(_canvasEvents);
        _canvasEvents.TargetRequested+=id=>
        {
            var target=Sim.State.Creatures.FirstOrDefault(c=>c.Id==id&&c.RegionId==Sim.State.ActiveRegionId);
            if(target==null){Toast("对象已不在当前场景，请查看记录。");return;}
            CancelGesture();ReleaseGrabSafely();_mapDragging=false;Sim.Select(id);
            _mapPan=new((float)target.X,0,(float)target.Z);UpdateUi();
        };
        void LayoutWidgets()
        {
            GameUiLayout.CanvasWidget(_uiRoot,_canvasEvents,false,90);
            if(_forceParameters!=null)GameUiLayout.CanvasWidget(_uiRoot,_forceParameters,true,36);
        }
        _uiRoot.Resized+=LayoutWidgets;LayoutWidgets();
    }
    private bool MainScenePoint(Vector2 point)=>GameUiLayout.Canvas(_uiRoot).HasPoint(point)&&
        (_pressure==null||!GameUiLayout.Hit(_pressure,point))&&(_canvasEvents==null||!_canvasEvents.BlocksPoint(point));
    private void UpdateSharedPanels()
    {
        if(_objectSidebar==null||_canvasEvents==null)return;
        _objectSidebar.Visible=!new[]{_welcome,_diaryPanel,_helpPanel}.Any(p=>p!=null&&p.Visible);
        var notices=new List<CanvasNotice>();
        var target=Sim.State.Creatures.FirstOrDefault(c=>c.Id==Sim.State.HelpTargetId&&c.Condition!=PhysicalCondition.Dead);
        if(target!=null)notices.Add(new("help:"+target.Id,target.Name+"正在求助，请查看它的状态。",target.Condition==PhysicalCondition.Critical?3:2,target.Id));
        if(Sim.State.MilestoneScenario&&Sim.State.Threat.Phase is ThreatPhase.Approaching or ThreatPhase.Intruding)
            notices.Add(new("threat",Sim.State.Threat.Phase==ThreatPhase.Approaching?"林缘有动静，请留意入口。":"乐园内出现威胁，请查看求助与防护情况。",2));
        _canvasEvents.UpdateNotices(notices);
        foreach(var notice in _eventSource.Read(Sim.State))_canvasEvents.Publish(notice);
        if(_forceParameters!=null)
        {
            _forceParameters.Visible=Sim.State.Mode==PlayMode.CloseUp&&!_grabTool&&!MilestoneModalOpen;
            _forceSetting.Text=$"力度设定 {_pressure.Value:P0}";
        }
    }
    private PanelContainer CreateGameplayDock()
    {
        var panel=new PanelContainer{Visible=false};GameUiLayout.Right(panel);
        panel.AddThemeStyleboxOverride("panel",PanelStyle("faf5e9"));_uiRoot.AddChild(panel);
        return panel;
    }
    private void ShowGameplayDock(PanelContainer panel)
    {
        if(_settingsPanel!=null&&_settingsPanel.Visible)CloseSettings();
        foreach(var other in new[]{_welcome,_diaryPanel,_helpPanel})
            if(other!=null)other.Visible=other==panel;
        panel.Visible=true;
        CancelGesture();ReleaseGrabSafely();_mapDragging=false;_force=0;
        UpdateUi();
    }
    private void ReleaseGrabSafely()
    {
        if(_held!=null&&GodotObject.IsInstanceValid(_held)){_held.LinearVelocity=Vector3.Zero;_held.AngularVelocity=Vector3.Zero;}
        _held=null;
    }
    private bool CloseGameplayDock()
    {
        foreach(var panel in new[]{_helpPanel,_diaryPanel,_welcome})
            if(panel!=null&&panel.Visible){panel.Visible=false;UpdateUi();return true;}
        return false;
    }
    private void HideGameplayDocks()
    {
        foreach(var panel in new[]{_welcome,_diaryPanel,_helpPanel})if(panel!=null)panel.Visible=false;
    }
}
