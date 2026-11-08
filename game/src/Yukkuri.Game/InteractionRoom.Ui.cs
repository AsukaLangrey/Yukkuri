using Godot;
using System;
using System.Linq;
using Yukkuri.Simulation;
using Expression = Yukkuri.Simulation.Expression;

public partial class InteractionRoom
{
    private Control _ui=null!,_modalShade=null!;
    private PanelContainer _leftPanel=null!,_rightPanel=null!,_modalPanel=null!;
    private HBoxContainer _forceWidget=null!;
    private CanvasEventFeed _eventFeed=null!;
    private Control _canvasEffects=null!;
    private VBoxContainer _modalContents=null!;
    private VBoxContainer _leftContents=null!;
    private VBoxContainer _collapsedNavigation=null!;
    private Button _sidebarToggle=null!;
    private bool _leftCollapsed;
    private Action _layoutCanvasWidgets=null!;
    private Label _title=null!,_hint=null!,_feedback=null!,_speechLabel=null!,_who=null!,_condition=null!,_memory=null!,_clockLabel=null!,_forceLabel=null!;
    private Button _closeButton=null!,_pauseButton=null!;
    private bool _modalOpen,_pauseBeforeModal;
    private bool _directToolMode;
    private HSlider _forceSlider=null!;
    private readonly System.Collections.Generic.Dictionary<InteractionAction,float> _toolForces=new();
    private InteractionGroup _group=InteractionCatalog.Groups[0];

    private StyleBoxFlat RoomStyle(string color,int radius=12,int pad=10,string? border=null)
    {
        var s=new StyleBoxFlat{BgColor=new Color(color),CornerRadiusTopLeft=radius,CornerRadiusTopRight=radius,
            CornerRadiusBottomLeft=radius,CornerRadiusBottomRight=radius,ContentMarginLeft=pad,ContentMarginRight=pad,ContentMarginTop=pad,ContentMarginBottom=pad};
        if(border!=null){s.BorderColor=new Color(border);s.SetBorderWidthAll(1);}return s;
    }
    private Label Text(string value,int size=16,string color="405247")
    {
        var l=new Label{Text=value,AutowrapMode=TextServer.AutowrapMode.WordSmart,MouseFilter=Control.MouseFilterEnum.Ignore};
        l.AddThemeFontSizeOverride("font_size",size);l.AddThemeColorOverride("font_color",new Color(color));return l;
    }
    private Button Button(string value,Action action,bool active=false)
    {
        var b=new Button{Text=value,AutowrapMode=TextServer.AutowrapMode.WordSmart,FocusMode=Control.FocusModeEnum.None,CustomMinimumSize=new(0,32),MouseDefaultCursorShape=Control.CursorShape.PointingHand};
        b.AddThemeStyleboxOverride("normal",RoomStyle(active?"566e5d":"eae9d9",6,5));
        b.AddThemeStyleboxOverride("hover",RoomStyle(active?"647d6a":"dedfca",6,5));
        b.AddThemeStyleboxOverride("pressed",RoomStyle("c6cfb9",6,5));
        b.AddThemeColorOverride("font_color",new Color(active?"fff7df":"405247"));
        b.Pressed+=action;return b;
    }
    private void SetQuickToolHighlight(Button button,bool active)
    {
        button.AddThemeStyleboxOverride("normal",RoomStyle(active?"566e5d":"eae9d9",6,5));
        button.AddThemeStyleboxOverride("hover",RoomStyle(active?"647d6a":"dedfca",6,5));
        var textColor=new Color(active?"fff7df":"405247");
        button.AddThemeColorOverride("font_color",textColor);
        button.AddThemeColorOverride("font_hover_color",textColor);
        button.AddThemeColorOverride("font_pressed_color",new Color("405247"));
        UiActionIcons.Tint(button,active);
    }
    private PanelContainer Panel(Control parent,string color="f6f2e5")
    {var p=new PanelContainer();p.AddThemeStyleboxOverride("panel",RoomStyle(color));parent.AddChild(p);return p;}
    private VBoxContainer Column(Control parent,int gap=10)
    {var v=new VBoxContainer();v.AddThemeConstantOverride("separation",gap);parent.AddChild(v);return v;}
    private HBoxContainer Row(Control parent,int gap=8)
    {var r=new HBoxContainer();r.AddThemeConstantOverride("separation",gap);parent.AddChild(r);return r;}

    private Theme RoomTheme()
    {
        var theme=new Theme{DefaultFont=_font,DefaultFontSize=15};
        theme.SetColor("font_color","Label",new Color("405247"));
        foreach(var (state,color) in new[]{("normal","eae9d9"),("hover","dedfca"),("pressed","c6cfb9"),("disabled","eeecdf")})
            theme.SetStylebox(state,"Button",RoomStyle(color,6,5));
        foreach(string name in new[]{"font_color","font_hover_color","font_pressed_color","font_focus_color"})
            theme.SetColor(name,"Button",new Color("405247"));
        theme.SetColor("font_disabled_color","Button",new Color("919889"));
        theme.SetStylebox("normal","LineEdit",RoomStyle("fffbee",6,6,"ccd2bd"));
        theme.SetStylebox("read_only","LineEdit",RoomStyle("eeecdf",6,6,"d9dccb"));
        var focus=RoomStyle("00000000",6,6,"83987b");
        theme.SetStylebox("focus","LineEdit",focus);
        theme.SetStylebox("focus","Button",focus);
        foreach(string name in new[]{"font_color","font_selected_color","caret_color"})
            theme.SetColor(name,"LineEdit",new Color("405247"));
        theme.SetColor("font_uneditable_color","LineEdit",new Color("78816e"));
        theme.SetColor("selection_color","LineEdit",new Color("d3dfc4"));
        return theme;
    }

    private void BuildRoomUi()
    {
        GameUiLayout.Configure(GetWindow());
        var layer=new CanvasLayer();AddChild(layer);
        _ui=new Control{MouseFilter=Control.MouseFilterEnum.Ignore,Theme=RoomTheme()};
        layer.AddChild(_ui);_ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _ui.SetMeta("no_top_bar",true);

        _leftPanel=Panel(_ui);GameUiLayout.Left(_leftPanel);
        var left=Column(_leftPanel,6);
        var navigationHeader=Row(left,6);
        _sidebarToggle=Button("☰",ToggleLeftSidebar);_sidebarToggle.CustomMinimumSize=new(32,32);_sidebarToggle.AutowrapMode=TextServer.AutowrapMode.Off;_sidebarToggle.TooltipText="收起或展开导航";navigationHeader.AddChild(_sidebarToggle);
        var back=Button("‹ 返回",ReturnToCourtyard);back.CustomMinimumSize=new(58,32);back.AutowrapMode=TextServer.AutowrapMode.Off;back.Name="NavigationBack";navigationHeader.AddChild(back);
        _title=Text("",17);_title.AutowrapMode=TextServer.AutowrapMode.Off;
        _title.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;_title.ClipText=true;
        _title.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;navigationHeader.AddChild(_title);
        _collapsedNavigation=Column(left,6);_collapsedNavigation.Visible=false;
        _leftContents=Column(left,6);_leftContents.SizeFlagsVertical=Control.SizeFlags.ExpandFill;
        left=_leftContents;
        var toolScroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,SizeFlagsVertical=Control.SizeFlags.ExpandFill};left.AddChild(toolScroll);
        var tools=Column(toolScroll,6);tools.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;BuildToolbar(tools);
        BuildContactProgress(left);
        _hint=Text("",13,"78816e");left.AddChild(_hint);
        _feedback=Text("",13);left.AddChild(_feedback);
        BuildRecycleBox(_ui);

        _rightPanel=Panel(_ui);GameUiLayout.Right(_rightPanel);
        var right=Column(_rightPanel,12);
        BuildOilSelector(right);
        BuildInspectorTabs(right);
        _rightBody=Column(right,0);_rightBody.SizeFlagsVertical=Control.SizeFlags.ExpandFill;
        _objectPage=Column(_rightBody,6);_objectPage.SizeFlagsVertical=Control.SizeFlags.ExpandFill;
        var reportScroll=new ScrollContainer{SizeFlagsVertical=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};_objectPage.AddChild(reportScroll);
        var report=Column(reportScroll,6);report.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        _who=Text("",18);report.AddChild(_who);
        _speechLabel=Text("慢慢来……",14);report.AddChild(_speechLabel);
        _lifeOverview=Text("",12);report.AddChild(_lifeOverview);
        BuildVitalMeters(report);
        BuildDetachedStemPanel(report);
        BuildSupplyStationPanel(report);
        BuildShelterPanel(report);
        _objectDetails=Text("",13);report.AddChild(_objectDetails);
        _condition=Text("",13);report.AddChild(_condition);
        _memoryHeading=Text("刚才发生的事",12,"8b927f");report.AddChild(_memoryHeading);_memory=Text("",13);report.AddChild(_memory);
        var footer=Panel(_objectPage,"eae9d9");_inspectorFooter=footer;
        var actions=Column(footer,4);
        var actionScroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,CustomMinimumSize=new(0,0)};
        actions.AddChild(actionScroll);
        var contextual=Column(actionScroll,4);contextual.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        _objectActions=Column(contextual,4);
        MoveInspectorActions(_creatureVitalMeters,contextual);
        MoveInspectorActions(_detachedStemPanel,contextual);
        MoveInspectorActions(_supplyStationPanel,contextual);
        MoveInspectorActions(_shelterPanel,contextual);
        _inspectorActionScroll=actionScroll;
        BuildSidebarSystemControls(right);

        _forceWidget=Row(_ui,12);_forceWidget.MouseFilter=Control.MouseFilterEnum.Ignore;
        _forceLabel=Text("力度设定",15,"fff8e6");_forceLabel.AutowrapMode=TextServer.AutowrapMode.Off;
        _forceLabel.AddThemeColorOverride("font_outline_color",new Color("253b32"));_forceLabel.AddThemeConstantOverride("outline_size",5);_forceWidget.AddChild(_forceLabel);
        _forceSlider=new HSlider{MinValue=.1,MaxValue=1,Step=.05,Value=_force,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,CustomMinimumSize=new(220,30),FocusMode=Control.FocusModeEnum.None};
        _forceSlider.ValueChanged+=v=>{_force=(float)v;_toolForces[_action]=_force;RefreshRoomUi();};_forceWidget.AddChild(_forceSlider);
        _eventFeed=new CanvasEventFeed();_ui.AddChild(_eventFeed);_eventFeed.TargetRequested+=FocusRoomEvent;
        BuildCanvasClock();
        BuildResourceBar();
        _canvasEffects=new Control{ClipContents=true,MouseFilter=Control.MouseFilterEnum.Ignore};_ui.AddChild(_canvasEffects);
        _pointer=new RoomPointer{MouseFilter=Control.MouseFilterEnum.Ignore};_canvasEffects.AddChild(_pointer);
        _actionPreview=Text("",12,"fff8e6");_actionPreview.Size=new(260,0);_actionPreview.Visible=false;
        _actionPreview.AddThemeColorOverride("font_outline_color",new Color("253b32"));_actionPreview.AddThemeConstantOverride("outline_size",4);_canvasEffects.AddChild(_actionPreview);
        void LayoutWidgets()
        {
            GameUiLayout.CanvasWidget(_ui,_eventFeed,false,90);GameUiLayout.CanvasWidget(_ui,_forceWidget,true,36);

            LayoutRecycleBox();
            LayoutResourceBar();
            var bounds=GameUiLayout.Canvas(_ui);_canvasEffects.Position=bounds.Position;_canvasEffects.Size=bounds.Size;
            _pointer.Position=-bounds.Position;_pointer.Size=_ui.Size;
        }
        _layoutCanvasWidgets=LayoutWidgets;
        _ui.Resized+=LayoutWidgets;LayoutWidgets();
        BuildDebugPanel();
        BuildActivityPanel();
        _modalShade=new ColorRect{Color=new Color(0.10f,.16f,.12f,.64f),Visible=false};_ui.AddChild(_modalShade);_modalShade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _modalPanel=Panel(_modalShade);_modalPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);_modalPanel.OffsetLeft=-310;_modalPanel.OffsetRight=310;_modalPanel.OffsetTop=-290;_modalPanel.OffsetBottom=290;
        var modalScroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};_modalPanel.AddChild(modalScroll);
        _modalContents=Column(modalScroll,12);_modalContents.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        SelectToolbarCategory(0);
    }

    private static void ClearChildren(Node parent){foreach(var n in parent.GetChildren()){parent.RemoveChild(n);n.QueueFree();}}
    private void ReturnToCourtyard()
    {
        CancelGesture();CloseActivityPanel();
        if(Sim.State.Mode==PlayMode.CloseUp)ToggleClose();
        SelectToolbarCategory(0);RefreshRoomUi();
    }
    private void ToggleLeftSidebar()
    {
        CancelGesture();
        _leftCollapsed=!_leftCollapsed;
        _leftContents.Visible=!_leftCollapsed;
        _collapsedNavigation.Visible=_leftCollapsed;
        _title.Visible=!_leftCollapsed;
        _sidebarToggle.Text="☰";
        foreach(var sibling in _sidebarToggle.GetParent().GetChildren())if(sibling is Control control&&control.Name=="NavigationBack")control.Visible=!_leftCollapsed;
        _ui.SetMeta("left_sidebar_collapsed",_leftCollapsed);
        GameUiLayout.Left(_leftPanel,_leftCollapsed);
        _layoutCanvasWidgets();
    }
    // Kept for legacy automation callers; the live UI has only the new toolbar.
    private void RefreshToolBrowser()=>RebuildToolbarChoices();
    private void SetToolTab(bool rough,bool selectGroupAction=true) { if(selectGroupAction)SelectToolbarCategory(rough?1:0); }
    private void SelectGroup(InteractionGroup group)=>SelectQuickTool(group.Choices[0].Action);
    private void SelectQuickTool(InteractionAction action)
    {
        SelectAction(action);
    }
    private void SelectAction(InteractionAction action)
    {
        SelectActionOwner(action);
        if(action==InteractionAction.PlaceShelter){ArmShelterPlacement();return;}
        if(action is not (InteractionAction.OpenShelterDoor or InteractionAction.CloseShelterDoor or InteractionAction.CallFromShelter))ClearShelterSelection();
        if(action is InteractionAction.PlaceFoodStation or InteractionAction.PlaceWaterStation)
        {ArmSupplyStationPlacement(action==InteractionAction.PlaceFoodStation?SupplyStationKind.Food:SupplyStationKind.Water);return;}
        if(action is not (InteractionAction.RefillSupplyStation or InteractionAction.Lift))ClearSupplyStationSelection();
        CancelGesture();_action=action;_toolArmed=true;
        _force=_toolForces.TryGetValue(action,out float rememberedForce)?rememberedForce:.25f;_forceSlider.SetValueNoSignal(_force);
        _group=InteractionCatalog.Group(action);RebuildToolbarChoices();
        _title.Text=_group.Name+" · "+InteractionCatalog.Choice(_action).Label;
        _hint.Text=ToolHint(action);
        _message="";RefreshRoomUi();
        if(!_modalOpen&&SupplyStockMissing(action))ShowMissingToolSupplies(action);
    }
    private static string ToolHint(InteractionAction action)=>
        action is InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Drink?
            "点油：递到嘴边，等它接下一口；点地面：放下一份。右键收回，换工具也会停止递送。":InteractionCatalog.Choice(action).Hint;
    private void DisarmTool()=>SelectToolbarCategory(0);
    private void SelectOil(string id)
    {
        if(InspectingRecycleBox)CloseActivityPanel();
        if(!Sim.State.Creatures.Any(c=>c.Id==id&&c.RegionId==Sim.State.ActiveRegionId)){DeselectInspector();return;}
        ClearShelterSelection();
        ClearSupplyStationSelection();
        _selectedRoomObjectId=null;
        _observedStemMotherId=null;
        _selectedDetachedStemEventId=null;
        CancelGesture();Sim.State.SelectedId=id;
        if(_familyFirstId!=id)_familySecondId=null;
        _familyFirstId=id;
        LeaveUnavailableCloseup();
        _message=Sim.Selected!.Family.ColdStorage.Stored?"冷藏中，可从家庭面板取出。":"";
        RefreshRoomUi();
    }
    private void ToggleClose()
    {
        if(_selectedDetachedStemEventId!=null||_selectedSupplyStationId!=null||_selectedShelterId!=null)return;
        if(Sim.State.Mode!=PlayMode.CloseUp&&Sim.Selected is { } selected&&!HasVisibleFamilyBody(selected))
        {_message=Sim.IsShelteredHidden(selected)?"它在小屋里，属性仍可查看；需要出来时会沿门口走出。":selected.Family.Memorialized?"遗体已经安置，可以在家庭面板查看留下的记录。":"它正在冷藏中。请先取出，再靠近观察。";RefreshRoomUi();return;}
        CancelGesture();Sim.State.Mode=Sim.State.Mode==PlayMode.CloseUp?PlayMode.Management:PlayMode.CloseUp;
        if(Sim.State.Mode!=PlayMode.CloseUp)_locatedCreatureId=null;
        _message="";
        RefreshRoomUi();
    }
    private void TogglePause(){CancelGesture();Sim.State.Settings.Pause=!Sim.State.Settings.Pause;RefreshRoomUi();}
    private void ResetCloseView()
    {
        if(Sim.State.Mode!=PlayMode.CloseUp)
        {
            CancelGesture();_yardFocus=Vector3.Zero;_zoom=16*(float)CourtyardGround.Expansion;
            _message="";
            RefreshRoomUi();return;
        }
        CancelGesture();_closeYaw=0;_closePitch=InteractionCameraOrbit.DefaultPitch;_closeZoom=1;
        _message="";
        RefreshRoomUi();
    }
    private void RefreshRoomUi()
    {
        if(_who==null||Sim==null)return;
        NormalizeDetachedStemSelection();
        NormalizeSupplyStationSelection();
        NormalizeShelterSelection();
        RefreshOilSelector();
        RefreshDebugPanel();
        RefreshSupplyStock();
        RefreshYardHelpButton();
        var c=Sim.Selected;
        RefreshCalendarClock();LayoutCalendarStrip();
        _pauseButton.Text=Sim.State.Settings.Pause?"继续":"停一停";
        _fullscreenButton.Text=GetWindow().Mode is Window.ModeEnum.Fullscreen or Window.ModeEnum.ExclusiveFullscreen?"窗口":"全屏";
        _forceLabel.Text=$"力度设定 · {_force*100:0}%";
        _forceWidget.Visible=!_modalOpen&&_toolArmed&&InteractionCatalog.UsesForce(_action);
        RefreshRoomEvents();
        RefreshInlineFeedback();
        var heldSupply=_heldSupplyId==null?null:Sim.State.RoomObjects.Find(o=>o.Id==_heldSupplyId&&o.HeldByHand);
        _hint.Visible=_waterRescueToolArmed||_arrivingCreature!=null||heldSupply!=null;
        if(_waterRescueToolArmed)
        {
            _title.Text="救援用品";
            _hint.Text=$"点击落水的油救回岸上；右键收回。每次消耗1份，当前 {Sim.State.WaterRescueTools} 份；可在“用品补给”补充。";
        }
        else if(heldSupply!=null)
        {
            _title.Text=heldSupply.Portions<=0?"这一份已递出":heldSupply.Kind switch
            {RoomObjectKind.Water=>"拿着清水",RoomObjectKind.Sweet=>"拿着零食",_=>"拿着饭饭"};
            _hint.Text=heldSupply.Portions<=0?"这一份已用完，松手结束这次递送。":
                "按住递到嘴边；移到空地松开放下，右键收回。"+
                (heldSupply.Kind is RoomObjectKind.Meal or RoomObjectKind.Water?"也可对准培养瓶开口，给这一窝补充食水。":"");
        }
        else if(!_toolArmed&&c==null)
        {
            _title.Text="空手 · 观察小院";
            _hint.Text="点击油或离体茎查看；拿起地面食水后，可递到培养瓶开口。";
        }
        else if(!_toolArmed&&c!=null)
        {
            bool carried=c.Interaction.Carried;
            _title.Text=c.Family.Memorialized?"家人记录":c.Family.ColdStorage.Stored?"冷藏保存":carried?"托着 · 先看看它":"空手 · 先看看它";
            _hint.Text=c.Family.Memorialized?"家庭面板保留名字、亲缘与共同经历。选择小院里的另一只，可以继续相处。":
                c.Family.ColdStorage.Stored?"从回收箱管理或家庭照护面板取出，才能再次靠近；箱内生理时间暂停。":
                carried?"按住身体继续搬运，慢慢松手放下；快速甩动后松手会抛出。Shift＋滚轮调高度，也可在空地单击放稳。":"按住身体抓起拖动，慢慢松手放下；快速甩动后松手会抛出。空手拿食水、推球；双击近看。";
        }
        else _hint.Text=ToolHint(_action);
        if(c==null)
        {
            _who.Text="观察小院";_condition.Text=_memory.Text="";
            _speechLabel.Text="选择油或离体茎，查看当前状态。";
            RefreshDetachedStemUi();RefreshSupplyStationUi();RefreshShelterUi();RefreshRedesignProperties();RefreshInspectorLayout();RefreshContactProgress();return;
        }
        var i=c.Interaction;
        RefreshVitalMeters(c);
        _who.Text=c.Name+" · "+SpeciesName(c.Body.SpeciesId)+(" · "+GrowthRules.Name(c));
        string intake=i.NaturalIntakePending?i.EatingKind==IntakeKind.Water?"准备喝这一口":"准备取这一口":
            i.EatingKind==IntakeKind.Water?"慢慢喝着这一口":i.GroundIntake&&i.GroundIntakeKind==RoomObjectKind.Leaves?"慢慢嚼着叶子":"慢慢嚼着这一口";
        string activity=i.EatingRemaining>0?intake:i.Resting?"慢慢休息":c.Activity;
        if(!WorldSimulation.CanSelfMove(c)&&(c.Condition is PhysicalCondition.Active or PhysicalCondition.Recovering))
            activity=activity.Length==0?"暂时无法自己挪动":activity+" · 暂时无法自己挪动";
        string state=c.Family.Memorialized?"已经安置，家人与经历仍保留":c.Condition==PhysicalCondition.Dead?
            (c.Family.ColdStorage.Stored?"遗体仍在冷藏中":"不再回应了"):c.Family.ColdStorage.Stored?"回收箱中 · 生理时间暂停":c.Condition==PhysicalCondition.Critical?"身体状态很差，急需护理":
            i.Physiology.Active?i.Physiology.Excreting==ExcretionKind.Urine?"正在撒尿":"正在排便":
            i.Falling?"正在下落":i.Carried?"正在被托着":i.Restraint switch{RestraintKind.Box=>"留在关着的箱子里",RestraintKind.Frame=>"被固定具限制着",RestraintKind.Hanging=>"留在悬挂装置上",RestraintKind.Wrap=>"被软布包裹着",RestraintKind.Hand=>"被手按住",_=>i.Supine?"身体翻了过来":activity};
        if(c.Condition is PhysicalCondition.Active or PhysicalCondition.Recovering&&!i.Falling&&
            (i.Carried||i.Restraint!=RestraintKind.None||i.Supine)&&(i.EatingRemaining>0||i.Resting))state+=" · "+activity;
        string needs=string.Join("\n",CreatureNeedsObservation.Needs(c));
        if(i.Digestion.Symptomatic)needs+=(needs.Length>0?"\n":"")+(i.Digestion.CareRemaining>0?"肚子不适已经照护，仍需吃喝和休息。":"肚子不舒服，留意吃喝和休息。");
        if(i.PendingConversation.HasValue)needs+=(needs.Length>0?"\n":"")+(i.EatingRemaining>0?"等这一口咽下后再回应，不必重复催它。":i.Resting?"正在歇着，等它慢慢回应。":"已听见你的话，等它慢慢回应。");
        foreach(var clue in InteractionSurfaceObservation.Describe(i))needs+=(needs.Length>0?"\n":"")+clue+"。";
        string mood=c.Pain>25||c.Expression==Expression.Hurt?"身体还在疼，需要照护。":
            c.ReactionRemaining>0&&c.Expression==Expression.Calling?"正在请求帮助，先听听它需要什么。":
            c.ReactionRemaining>0&&i.ReactionRefused?"这次不愿意继续，先给它一点空间。":
            c.Fear>60?"它还有些害怕，先留一点距离。":i.Emotion.Alarm>=12||i.Water.RecoverySeconds>0?
            "它还记得刚才的惊吓，需要持续安稳地相处。":i.Resting?"先让它安静歇一会儿。":!i.HasAccessory?
            (i.Accessory.BorrowedForCare?"等你整理好饰品，再帮它戴回去。":"还惦记着离开头顶的饰品。"):
            i.Accessory.Integrity<90?"仍在意饰品上的破损。":c.Resentment>40?"它还有些不满。":"";
        string accessory=i.HasAccessory?(i.Accessory.Integrity<70?"饰品有破损，仍戴在头上。":""):
            i.Accessory.Integrity<=0?"原来的饰品已经损坏。":i.Accessory.Place switch{AccessoryPlace.Stored=>"饰品被收起来了。",AccessoryPlace.Hung=>"饰品挂在高处。",_=>"饰品离开了头顶。"};
        _condition.Text=string.Join("\n",new[]{c.Condition==PhysicalCondition.Dead?"":mood,
            c.Condition==PhysicalCondition.Dead?"":Sim.NightHousingStatus(c),
            c.Condition==PhysicalCondition.Dead?"":needs,accessory,
            c.Condition!=PhysicalCondition.Dead&&i.FootIntegrity<70?(i.FootTreated?"伤处已护理，恢复中。":"移动部位受伤，需要检查。"):""}.Where(s=>s.Length>0));
        if(c.Condition!=PhysicalCondition.Dead&&MouthFunction.Impairment(c)>20)_condition.Text+="\n\n"+MouthFunction.Describe(c);
        var selfCare=i.Injuries.SelfCare;
        if(c.Condition!=PhysicalCondition.Dead)
        {
            if(selfCare.Active)_condition.Text+="\n\n正在自行护伤，还没有完成。需要安静和时间，也可能失败。";
            else if(selfCare.LastEvent.Length>0)_condition.Text+="\n\n最近一次自行护伤："+selfCare.LastEvent;
        }
        string family=c.Condition==PhysicalCondition.Dead?"":FamilyRules.Describe(c);
        if(family.Length>0)_condition.Text+="\n\n"+family;
        string aftermath=FamilyRules.DescribeAftermath(c);
        if(aftermath.Length>0)_condition.Text+="\n\n"+aftermath;
        string risk=c.Condition==PhysicalCondition.Dead?"":FamilyRules.DescribeRisk(c);
        if(risk.Length>0)_condition.Text+="\n\n"+risk;
        if(c.Family.ColdStorage.Stored)_condition.Text=c.Condition==PhysicalCondition.Dead?"遗体冷藏保存中，经历与关系记录仍保留。":"冷藏期间生理时间暂停，当前数值保留；取出后再继续照护。";
        _condition.Text=_condition.Text.Replace("\n\n","\n");
        _memory.Text=string.Join("\n",c.PersonalMemories.TakeLast(3).Reverse().Select(m=>m.Text));
        string speech=c.Condition==PhysicalCondition.Critical?c.Speech:MouthFunction.FormatSpeech(c,c.Speech);
        _speechLabel.Text=c.Condition!=PhysicalCondition.Dead&&c.ReactionRemaining>0&&speech.Length>0?c.Name+"：「"+speech+"」":c.Name+" · "+state;
        RefreshDetachedStemUi();
        RefreshSupplyStationUi();
        RefreshShelterUi();
        RefreshRedesignProperties();
        _memory.Visible=_memoryHeading.Visible=false;
        _who.Visible=_selectedRoomObjectId!=null||_selectedSupplyStationId!=null||_selectedShelterId!=null||_selectedDetachedStemEventId!=null||_observedStemMotherId!=null;
        _condition.Visible=_condition.Visible&&_condition.Text.Length>0;
        RefreshInspectorLayout();
        RefreshContactProgress();
    }

    private enum SystemDialogKind { Save,Load,Settings,Menu }
    private void OpenSystemModal(SystemDialogKind kind,string title)
    {
        CloseActivityPanel();
        CancelGesture();if(!_modalOpen)_pauseBeforeModal=Sim.State.Settings.Pause;
        _modalOpen=true;Sim.State.Settings.Pause=true;_modalShade.Visible=true;ClearChildren(_modalContents);
        _modalContents.AddChild(Text(title,24));
    }
    private void CloseModal(){if(!_modalOpen)return;_modalOpen=false;_modalShade.Visible=false;Sim.State.Settings.Pause=_pauseBeforeModal;RefreshRoomUi();}
    private void ShowSystemMessage(SystemDialogKind kind,string message){OpenSystemModal(kind,"存读档未完成");_modalContents.AddChild(Text(message));_modalContents.AddChild(Button("返回设置",ShowSettings));}
    private void ShowHelp()
    {
        OpenActivityPanel("小院操作",ShowHelp);
        foreach(var line in new[]{
            "观察只选中对象。空手分为抓取、抚摸、收拾、戳碰、按压。右键停止使用工具并回到观察。",
            "抓取按住拖动，松手放下；快速甩动后松手抛出。抓取时画布左下出现回收图标，拖入后个体进入12格回收箱，物品入仓不退款。",
            "抚摸停在头顶或脸侧即可接触，按住轻拖可贴地挪动。戳碰轻点部位，空地按住后挥向身体按速度击打，长按部位拖动可拉拽。",
            "按压从身体下半部向上推可推翻；右侧属性栏的按住与解除按压按钮控制持续按压。切换工具不解除固定装置。",
            "饭饭、零食、清水点油手喂，点地放下。零食停在油身边可逗食。所有清水使用同一份有限库存。",
            "供给器点击查看补充频率，按时从仓库取食水，缺货停止补充。商店购买实际花钱，不自动采购。",
            "玩具包括软球、小宝物、包裹、伸展。包裹后按住缓慢左右轻摇，可从右侧属性栏解开。",
            "交流可招呼、训诫、唱歌、教学、指令、相处。教学目前示范自己清扫；相处依次指定两只油，抓取会打断。",
            "养护可擦拭、检查、整理饰品、护理面粉、救护橙汁、冷藏复苏、埋葬。取下的饰品拖回原主身体即可归还。",
            "繁育入口查看个体许可、使用成熟个体喷剂和照护油茎。",
            "正常速度10分钟为一天，每季7天；春秋夜晚8小时，夏夜7小时，冬夜9小时。时间与倍速显示在画布左上，可切换1/2/3/5/10倍；Esc打开系统菜单，F5保存、F9读取，空格暂停。",
            "中键拖动画面，滚轮缩放；Shift＋滚轮调支点高度。空格暂停，Tab近看。每分钟自动保存，读档不结算离线时间。"})_panelContents.AddChild(Text(line,14));
        _panelContents.AddChild(PanelButton("收起帮助",CloseActivityPanel));
    }
    private void ShowSaveSlots(bool loading)
    {
        OpenSystemModal(loading?SystemDialogKind.Load:SystemDialogKind.Save,loading?"读取游戏":"保存游戏");
        _modalContents.AddChild(Text(loading?"选择要继续的进度；读取会替换当前小院状态。":"选择手动槽位保存当前进度。已有槽位会保留上一份备份。",14));
        for(int n=loading?0:1;n<=3;n++)
        {
            int slot=n;string file=_saves.SlotPath(slot);bool exists=System.IO.File.Exists(file);
            string name=slot==0?"自动存档":"手动槽位 "+slot;
            string stamp=exists?System.IO.File.GetLastWriteTime(file).ToString("yyyy-MM-dd HH:mm"):"空槽位";
            var button=Button($"{(loading?"读取":"保存到")} {name} · {stamp}",()=>
            {
                if(loading)LoadRoom(slot);
                else{_slot=slot;if(SaveRoom(slot))CloseModal();else ShowSystemMessage(SystemDialogKind.Save,_message);}
            });
            button.Disabled=loading&&!exists;button.CustomMinimumSize=new(0,48);_modalContents.AddChild(button);
        }
        _modalContents.AddChild(Button("返回系统菜单",ShowSystemMenu));
        _modalContents.AddChild(Button("返回小院 · Esc",CloseModal));
    }
    private void ShowSettings()
    {
        OpenSystemModal(SystemDialogKind.Settings,"游戏设置");
        _modalContents.AddChild(Button("保存游戏 · F5",()=>ShowSaveSlots(false)));
        _modalContents.AddChild(Button("读取游戏 · F9",()=>ShowSaveSlots(true)));
        _modalContents.AddChild(Button(_muted?"声音：关":"声音：开",()=>{SetRoomMuted(!_muted);ShowSettings();}));
        _modalContents.AddChild(Button(GetWindow().Mode is Window.ModeEnum.Fullscreen or Window.ModeEnum.ExclusiveFullscreen?"切换窗口 · F11":"切换全屏 · F11",()=>{ToggleFullscreen();ShowSettings();}));
        _modalContents.AddChild(Button(Sim.State.Settings.LethalInjuries?"致命后果：开":"致命后果：关",()=>{Sim.State.Settings.LethalInjuries=!Sim.State.Settings.LethalInjuries;ShowSettings();}));
        _modalContents.AddChild(Button(Sim.State.AutonomousBreeding?"全局自主繁育：开":"全局自主繁育：关",()=>{Sim.State.AutonomousBreeding=!Sim.State.AutonomousBreeding;ShowSettings();}));
        _modalContents.AddChild(Button("重新开一间小院",()=>
        {
            OpenSystemModal(SystemDialogKind.Settings,"重新开始这次相处？");_modalContents.AddChild(Text("当前状态先保存到自动存档，手动槽位保留。新小院的三只油将从初始状态开始。"));
            _modalContents.AddChild(Button("保存并重新开始",()=>
            {
                if(!SaveRoom(0,false)){ShowSystemMessage(SystemDialogKind.Save,_message);return;}
                Sim=WorldSimulation.CreateCourtyardEconomy();ResetRoomUiSession();_recovered=false;_pauseBeforeModal=false;BuildActors();CloseModal();
            }));
            _modalContents.AddChild(Button("继续原来的相处",CloseModal));
        }));
        _modalContents.AddChild(Button("关闭面板",CloseModal));
    }
    private void ShowMedicineSupplies(string? result=null,InteractionAction equipAction=InteractionAction.Care)=>ShowShop(result,"养护用品");
    private void ShowRescueSupplies(string? result=null)=>ShowShop(result,"养护用品");
}
