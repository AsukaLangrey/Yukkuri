using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    // Render checks exercise the real scene and controls. They are not native mouse-play evidence.
    private void StartRoomVerificationIfRequested(string[] args)
    {
        int contactFlag=Array.IndexOf(args,"--contact-check-output");
        if(contactFlag>=0&&contactFlag+1<args.Length){_=VerifyContactGeometry(args[contactFlag+1]);return;}
        int flag=Array.IndexOf(args,"--room-render-output");
        if(flag>=0&&flag+1<args.Length)_=VerifyRoomRender(Path.GetFullPath(args[flag+1]));
    }
    private async Task RenderFrames(int count=5)
    {for(int n=0;n<count;n++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task VerifyRoomRender(string directory)
    {
        var results=new List<object>();
        try
        {
            Directory.CreateDirectory(directory);_autoClock=-10000;_muted=true;
            await RenderFrames(20);
            async Task Capture(string name)
            {
                await RenderFrames(10);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var picture=GetViewport().GetTexture().GetImage();
                var error=picture.SavePng(Path.Combine(directory,name+".png"));
                if(error!=Error.Ok)throw new IOException("截图失败："+error);
                Rect2 viewport=_ui.GetGlobalRect();
                bool sidebarsFit=viewport.Encloses(_leftPanel.GetGlobalRect())&&viewport.Encloses(_rightPanel.GetGlobalRect());
                bool forceFits=GameUiLayout.Canvas(_ui).Encloses(_forceWidget.GetGlobalRect());
                bool forceSingleLine=_forceLabel.Size.Y<40;
                if(_leftPanel.Position.Y>GameUiLayout.Margin+1||_rightPanel.Position.Y>GameUiLayout.Margin+1||_clockLabel.Size.Y>28||_recycleLabel.Size.Y>28)
                    throw new InvalidOperationException("顶栏或收容箱文字换行撑高");
                if(!sidebarsFit||!forceFits||!forceSingleLine)throw new InvalidOperationException("侧栏或画布参数组件超出布局范围");
                results.Add(new{name,sidebarsFit,forceFits,forceSingleLine,viewport=new{viewport.Size.X,viewport.Size.Y},
                    feedback=new{x=_feedback.GlobalPosition.X,y=_feedback.GlobalPosition.Y,width=_feedback.Size.X,height=_feedback.Size.Y},
                    action=_action.ToString(),mode=Sim.State.Mode.ToString()});
            }
            Sim.State.Settings.Pause=true;await Capture("01-yard");
            var originalAction=_action;var originalMode=_roomMode;float originalForce=_force;
            _categoryButtons[2].EmitSignal(BaseButton.SignalName.Pressed);
            await RenderFrames(3);
            if(_action!=originalAction||_roomMode!=originalMode||_force!=originalForce||
                !_categoryChoices[2].Visible||_categoryChoices.Where((c,i)=>i!=2).Any(c=>c.Visible))
                throw new InvalidOperationException("一级导航必须只展开该分类，不能切换工具或力度");
            await Capture("navigation-food-expanded");
            _categoryButtons[2].EmitSignal(BaseButton.SignalName.Pressed);
            RefreshRoomUi();await RenderFrames(5);
            if(_title.Text!="观察"||!_categoryChoices[2].Visible)
                throw new InvalidOperationException("重复选择一级分类改变了工具或隐藏了二级列");
            if(_categoryChoices[2].GlobalPosition.X<=_categoryButtons[2].GetGlobalRect().End.X)
                throw new InvalidOperationException("二级操作必须位于一级分类右侧");
            _categoryButtons[1].EmitSignal(BaseButton.SignalName.Pressed);
            _navigationActions.Single(e=>e.Mode==RoomMode.Pet).Button.EmitSignal(BaseButton.SignalName.Pressed);
            if(_roomMode!=RoomMode.Pet||_action!=InteractionAction.Stroke)
                throw new InvalidOperationException("二级导航未切换到选中的操作");
            foreach(bool paused in new[]{false,true})
            {
                Sim.State.Settings.Pause=paused;
                var expandedCanvas=GameUiLayout.Canvas(_ui);
                _sidebarToggle.EmitSignal(BaseButton.SignalName.Pressed);await RenderFrames(5);
                var collapsedCanvas=GameUiLayout.Canvas(_ui);
                if(!_leftCollapsed||_leftContents.IsVisibleInTree()||!_collapsedNavigation.IsVisibleInTree()||
                    _categoryIconButtons.Any(b=>!b.IsVisibleInTree()||!_leftPanel.GetGlobalRect().Encloses(b.GetGlobalRect()))||
                    Sim.State.Settings.Pause!=paused||
                    _roomMode!=RoomMode.Pet||_toolbarCategory!=1||
                    Math.Abs(collapsedCanvas.Size.X-expandedCanvas.Size.X-(GameUiLayout.LeftWidth-GameUiLayout.CollapsedLeftWidth))>.1||
                    !IsScenePoint(new Vector2(expandedCanvas.Position.X-30,expandedCanvas.GetCenter().Y))||
                    IsScenePoint(_sidebarToggle.GetGlobalRect().GetCenter())||
                    _leftPanel.Size.X>GameUiLayout.CollapsedLeftWidth+1)
                    throw new InvalidOperationException("收起导航未释放画布、拦截了场景或改变了选择/暂停状态");
                await Capture("navigation-collapsed-"+paused);
                _sidebarToggle.EmitSignal(BaseButton.SignalName.Pressed);await RenderFrames(5);
                if(_leftCollapsed||!_categoryChoices[1].IsVisibleInTree()||Sim.State.Settings.Pause!=paused||
                    IsScenePoint(new Vector2(expandedCanvas.Position.X-30,expandedCanvas.GetCenter().Y)))
                    throw new InvalidOperationException("展开导航未恢复分类或输入边界");
            }
            await Capture("navigation-restored");
            _sidebarToggle.EmitSignal(BaseButton.SignalName.Pressed);await RenderFrames(3);
            _categoryIconButtons[2].EmitSignal(BaseButton.SignalName.Pressed);await RenderFrames(5);
            if(_leftCollapsed||!_categoryChoices[2].IsVisibleInTree()||_roomMode!=RoomMode.Pet)
                throw new InvalidOperationException("图标栏未展开所选分类或误切换了工具");
            await Capture("navigation-icon-opened-food");
            _categoryButtons[Toolbar.Length].EmitSignal(BaseButton.SignalName.Pressed);
            await RenderFrames(3);await Capture("navigation-arrivals");
            if(!_categoryChoices[Toolbar.Length].IsVisibleInTree()||_roomMode!=RoomMode.Pet)
                throw new InvalidOperationException("新油一级导航未显示或改变了工具");
            _categoryButtons[Toolbar.Length+1].EmitSignal(BaseButton.SignalName.Pressed);
            await RenderFrames(3);await Capture("navigation-shop-categories");
            var shopChoices=(VBoxContainer)_categoryChoices[Toolbar.Length+1].GetChild(0);
            ((Button)shopChoices.GetChild(0)).EmitSignal(BaseButton.SignalName.Pressed);
            await RenderFrames(3);
            if(!ActivityPanelVisible||_activityTitle!="商店"||_roomMode!=RoomMode.Pet)
                throw new InvalidOperationException("商店分类没有打开商店或误切换工具");
            CloseActivityPanel();
            SelectToolbarCategory(0);
            Sim.State.Settings.Pause=false;ToggleClose();await RenderFrames(90);await Capture("02-close");
            SelectGroup(InteractionCatalog.Groups.Single(g=>g.Id=="P06"));await Capture("03-feeding-controls");
            SetToolTab(true);SelectGroup(InteractionCatalog.Groups.Single(g=>g.Id=="P15"));await Capture("04-force-controls");
            ShowHelp();await Capture("05-help");CloseActivityPanel();
            ShowShop();await Capture("06-shop-controls");CloseActivityPanel();
            // Gameplay panels must preserve both running and explicitly paused states.
            foreach(bool paused in new[]{false,true})
            {
                Sim.State.Settings.Pause=paused;
                foreach(var page in new (string name,Action open)[]{
                    ("help",ShowHelp),("shop",()=>ShowShop()),("arrival",ShowCreatureArrival),
                    ("family",ShowFamily),("commands",ShowCommandChoices),("teaching",ShowTeachingChoices),
                    ("stem",ShowStemChoices),("breeding",ShowBreedingStatus),("container",ShowContainer),
                    ("shelters",()=>ShowShelterSupplies()),("details",ShowFamilyDetails)})
                {
                    page.open();await RenderFrames(2);
                    bool valid=ActivityPanelVisible&&!_modalOpen&&!_modalShade.Visible&&Sim.State.Settings.Pause==paused&&
                        IsScenePoint(GameUiLayout.Canvas(_ui).GetCenter())&&!IsScenePoint(_activityPanel.GetGlobalRect().GetCenter());
                    if(!valid)throw new InvalidOperationException("玩法侧栏阻断场景或改变暂停状态："+page.name);
                    CloseActivityPanel();
                    if(Sim.State.Settings.Pause!=paused)throw new InvalidOperationException("关闭侧栏改变暂停状态："+page.name);
                    results.Add(new{name="sidebar-"+page.name,paused,sceneInputAvailable=true});
                }
                ShowSettings();
                if(!_modalOpen||!Sim.State.Settings.Pause)throw new InvalidOperationException("系统设置未保持模态暂停");
                CloseModal();
                if(Sim.State.Settings.Pause!=paused)throw new InvalidOperationException("系统设置未恢复原暂停状态");
            }
            var canvas=GameUiLayout.Canvas(_ui);
            if(!IsScenePoint(new Vector2(canvas.Position.X+20,canvas.End.Y-20)))
                throw new InvalidOperationException("旧底部区域仍有不可操作的空白带");
            if(_forceWidget.Visible&&(IsScenePoint(_forceSlider.GetGlobalRect().GetCenter())||!IsScenePoint(_forceLabel.GetGlobalRect().GetCenter())))
                throw new InvalidOperationException("力度控件与透明区域的输入边界不正确");
            ShowCreatureArrival();await RenderFrames(2);
            var draft=_panelContents.GetChildren().OfType<LineEdit>().Single();draft.Text="保留输入";
            ShowHelp();ShowCreatureArrival();await RenderFrames(2);
            if(_panelContents.GetChildren().OfType<LineEdit>().Single().Text!="保留输入")
                throw new InvalidOperationException("切换右侧页面丢失了未提交输入");
            CloseActivityPanel();
            Sim.State.Settings.Pause=false;
            if(OS.GetCmdlineUserArgs().Contains("--art-gallery"))
            {
                // Deliberately authored pose gallery, not evidence of player input or simulation outcomes.
                foreach(var c in Sim.State.Creatures)
                {
                    Sim.State.SelectedId=c.Id;Sim.State.Mode=PlayMode.CloseUp;
                    c.Interaction.Dirt=0;c.Interaction.SurfaceDirt.Clear();c.Interaction.SideDirt.Clear();
                    c.Interaction.Wetness=0;c.Interaction.SurfaceWetness.Clear();c.Interaction.SideWetness.Clear();
                    c.Expression=Yukkuri.Simulation.Expression.Neutral;
                    await RenderFrames(80);await Capture("art-"+c.Name+"-idle");
                    c.Interaction.Carried=true;c.Interaction.Height=.8;
                    await RenderFrames(80);await Capture("art-"+c.Name+"-carried");
                    c.Interaction.Carried=false;c.Interaction.Height=0;
                    c.Interaction.Accessory.Place=AccessoryPlace.Hand;c.Interaction.Accessory.X=c.X+1;
                    c.Interaction.Accessory.Z=c.Z;c.Interaction.Accessory.Height=.7;
                    c.Interaction.Accessory.Dirt=30;
                    await RenderFrames(40);await Capture("art-"+c.Name+"-accessory-dirty");
                    c.Interaction.Accessory.Dirt=12;await Capture("art-"+c.Name+"-accessory-partly-clean");
                    c.Interaction.Accessory.Dirt=3;await Capture("art-"+c.Name+"-accessory-nearly-clean");
                    c.Interaction.Accessory.Dirt=0;c.Interaction.Accessory.Integrity=40;
                    await Capture("art-"+c.Name+"-accessory-torn");
                    c.Interaction.Accessory.Integrity=100;c.Interaction.Accessory.RepairProgress=36;
                    await Capture("art-"+c.Name+"-accessory-repaired");
                    c.Interaction.Accessory.Place=AccessoryPlace.Ground;c.Interaction.Accessory.Height=0;
                    await Capture("art-"+c.Name+"-accessory-ground");
                    c.Interaction.Accessory.Place=AccessoryPlace.Worn;c.Interaction.Accessory.RepairProgress=0;
                    foreach(var fixture in new[]{RestraintKind.Wrap,RestraintKind.Box,RestraintKind.Frame,RestraintKind.Hanging})
                    {
                        c.Interaction.Restraint=fixture;c.Interaction.Height=fixture==RestraintKind.Hanging?.7:0;
                        await RenderFrames(30);await Capture("art-"+c.Name+"-"+fixture);
                    }
                    c.Interaction.Restraint=RestraintKind.None;c.Interaction.Height=0;c.Interaction.Supine=true;
                    await RenderFrames(30);await Capture("art-"+c.Name+"-supine");
                    c.Interaction.Dirt=40;c.Interaction.SurfaceDirt[BodyPart.Bottom]=60;
                    await Capture("art-"+c.Name+"-underside-dirt");
                    c.Interaction.Dirt=0;c.Interaction.SurfaceDirt.Clear();c.Interaction.SideDirt.Clear();c.Interaction.FootIntegrity=35;
                    await Capture("art-"+c.Name+"-underside-injury");
                    c.Interaction.FootIntegrity=100;c.Interaction.Supine=false;
                    foreach(string pose in new[]{"pressure","roll","nuzzle","stretch"})
                    {
                        c.Interaction.ReactionPose=pose;c.Interaction.ReactionPoseRemaining=1000;_force=.9f;
                        await RenderFrames(22);await Capture("art-"+c.Name+"-"+pose);
                    }
                    c.Interaction.ReactionPose="idle";c.Interaction.ReactionPoseRemaining=0;
                }
            }
            SelectToolbarCategory(0);await RenderFrames(5);
            if(_categoryChoices[0].IsVisibleInTree())throw new InvalidOperationException("同名单项仍显示重复二级");
            if(_navigationDivider.Visible||_navigationOperations.Visible||
                Math.Abs(_leftPanel.Size.X-GameUiLayout.SingleLeftWidth)>1)
                throw new InvalidOperationException("无二级导航时未收窄或仍显示分栏线");
            if(_recycleBox.Visible)throw new InvalidOperationException("空闲时不应显示回收图标");
            foreach(bool paused in new[]{false,true})
            {
                Sim.State.Settings.Pause=paused;
                _eventsTab.EmitSignal(BaseButton.SignalName.Pressed);await RenderFrames(5);
                if(_activityTitle!="事件记录"||!_eventsTab.IsVisibleInTree()||Sim.State.Settings.Pause!=paused)
                    throw new InvalidOperationException("事件页签不可用或改变了暂停");
                await Capture("inspector-events-"+paused);
                _familyPanelButton.EmitSignal(BaseButton.SignalName.Pressed);await RenderFrames(5);
                if(_activityTitle!="家庭记录"||!_familyPanelButton.IsVisibleInTree()||Sim.State.Settings.Pause!=paused)
                    throw new InvalidOperationException("家庭页签不可用或改变了暂停");
                await Capture("inspector-family-"+paused);
                _propertiesTab.EmitSignal(BaseButton.SignalName.Pressed);await RenderFrames(5);
                if(ActivityPanelVisible||!_objectPage.IsVisibleInTree()||Sim.State.Settings.Pause!=paused)
                    throw new InvalidOperationException("属性页签未恢复或改变了暂停");
            }
            var window=GetWindow();var originalSize=window.Size;var originalPosition=window.Position;
            if(_inspectorTabs.GetChildren().OfType<Button>().Any(b=>b.Text.Contains("分工")))throw new InvalidOperationException("手动分工页签仍可见");
            await Capture("inspector-no-manual-duty");
            _propertiesTab.EmitSignal(BaseButton.SignalName.Pressed);await RenderFrames(5);
            if(ActivityPanelVisible)throw new InvalidOperationException("属性页签未恢复属性页");
            Sim.State.Mode=PlayMode.Management;
            _locatedCreatureId=null;
            _closeButton.EmitSignal(BaseButton.SignalName.Pressed);await RenderFrames(10);
            if(Sim.State.Mode!=PlayMode.Management||_locatedCreatureId!=Sim.Selected!.Id||
                _yardFocus.DistanceTo(new Vector3((float)Sim.Selected.X,0,(float)Sim.Selected.Z))>.01f)
                throw new InvalidOperationException("第一次点击未定位或直接进入近看");
            var firstSelected=Sim.Selected.Id;
            SelectOil(Sim.State.Creatures.First(c=>c.Id!=firstSelected).Id);
            _closeButton.EmitSignal(BaseButton.SignalName.Pressed);await RenderFrames(5);
            if(Sim.State.Mode!=PlayMode.Management||_locatedCreatureId!=Sim.Selected!.Id)
                throw new InvalidOperationException("切换对象后未从定位开始");
            _closeButton.EmitSignal(BaseButton.SignalName.Pressed);await RenderFrames(10);
            if(Sim.State.Mode!=PlayMode.CloseUp)throw new InvalidOperationException("第二次点击未进入近看");
            await Capture("inspector-two-step-focus");
            _closeButton.EmitSignal(BaseButton.SignalName.Pressed);await RenderFrames(5);
            if(Sim.State.Mode==PlayMode.CloseUp||_locatedCreatureId!=null)
                throw new InvalidOperationException("近看按钮未退出或未重置定位");
            SelectOil(firstSelected);
            ShowCreatureArrival();await RenderFrames(3);
            _panelContents.GetChildren().OfType<LineEdit>().Single().Text="自动刷新保留";
            _nextActivityRefresh=0;AutoRefreshActivity();await RenderFrames(3);
            if(_panelContents.GetChildren().OfType<LineEdit>().Single().Text!="自动刷新保留")
                throw new InvalidOperationException("自动刷新丢失名字草稿");
            CloseActivityPanel();
            results.Add(new{name="inspector-two-step-focus-and-autorefresh",passed=true});
            foreach(var size in new[]{new Vector2I(1600,900),new Vector2I(1440,1000)})
            {
                window.Size=size;await RenderFrames(25);RefreshInspectorLayout();await RenderFrames(5);
                if(Math.Abs(_ui.Size.X/_ui.Size.Y-(float)window.Size.X/window.Size.Y)>.01f)
                    throw new InvalidOperationException("窗口宽高变化后仍有固定比例留边");
                if(_inspectorFooter.Visible&&(!_rightPanel.GetGlobalRect().Encloses(_inspectorFooter.GetGlobalRect())||
                    Math.Abs(_inspectorFooter.GetGlobalRect().End.Y-_rightBody.GetGlobalRect().End.Y)>2))
                    throw new InvalidOperationException("右栏操作区未固定在底部");
                var resourceRect=_resourceBar.GetGlobalRect();var scene=GameUiLayout.Canvas(_ui);
                if(!scene.Encloses(resourceRect)||Math.Abs(resourceRect.End.X-(scene.End.X-12))>2||
                    !IsScenePoint(resourceRect.GetCenter())||_resourceValues[0].Text!=Sim.State.Money.ToString())
                    throw new InvalidOperationException("资源栏未靠右、超出画布、拦截输入或数值未更新");
                await Capture("responsive-"+size.X+"x"+size.Y);
            }
            ToggleFullscreen();await RenderFrames(40);
            if(window.Mode!=Window.ModeEnum.Fullscreen||window.Size!=DisplayServer.ScreenGetSize(window.CurrentScreen))
                throw new InvalidOperationException("全屏未覆盖当前屏幕");
            if(Math.Abs(_ui.Size.X/_ui.Size.Y-(float)window.Size.X/window.Size.Y)>.01f)
                throw new InvalidOperationException("全屏画布存在比例留边");
            await Capture("responsive-fullscreen");
            ToggleFullscreen();await RenderFrames(20);
            if(window.Mode!=Window.ModeEnum.Windowed)throw new InvalidOperationException("无法退出全屏");
            window.Size=originalSize;window.Position=originalPosition;await RenderFrames(15);
            await Capture("inspector-final");
            results.Add(new{name="responsive-fullscreen-roundtrip",passed=true});
            var frameTimes=new List<double>();ulong previous=Time.GetTicksUsec();
            for(int n=0;n<180;n++)
            {
                await RenderFrames(1);ulong current=Time.GetTicksUsec();
                frameTimes.Add((current-previous)/1000.0);previous=current;
            }
            frameTimes.Sort();
            var performance=new{scope="180 process-frame intervals in this verification window; not a general hardware benchmark",samples=frameTimes.Count,
                medianMs=frameTimes[90],p95Ms=frameTimes[171],maxMs=frameTimes[^1],
                drawCalls=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
                renderedObjects=Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame),
                primitives=Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)};
            File.WriteAllText(Path.Combine(directory,"render-checks.json"),JsonSerializer.Serialize(new{kind="engine-render-not-native-play",at=DateTimeOffset.UtcNow,results,performance},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("INTERACTION_RENDER_COMPLETE: "+directory);GetTree().Quit();
        }
        catch(Exception ex){GD.PushError(ex.ToString());File.WriteAllText(Path.Combine(directory,"error.txt"),ex.ToString());GetTree().Quit(2);}
    }
}
