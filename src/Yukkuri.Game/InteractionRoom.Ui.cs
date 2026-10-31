using Godot;
using System;
using System.Linq;
using Yukkuri.Simulation;
using Expression = Yukkuri.Simulation.Expression;

public partial class InteractionRoom
{
    private Control _ui=null!,_modalShade=null!;
    private PanelContainer _leftPanel=null!,_rightPanel=null!,_bottomPanel=null!,_modalPanel=null!;
    private VBoxContainer _groupList=null!,_modalContents=null!;
    private HFlowContainer _choiceList=null!;
    private Label _title=null!,_hint=null!,_feedback=null!,_speechLabel=null!,_who=null!,_condition=null!,_memory=null!,_clockLabel=null!,_hoverLabel=null!,_forceLabel=null!;
    private Button _closeButton=null!,_pauseButton=null!,_resetViewButton=null!;
    private bool _modalOpen,_pauseBeforeModal;
    private bool _directToolMode;
    private HSlider _forceSlider=null!;
    private readonly System.Collections.Generic.Dictionary<InteractionAction,float> _toolForces=new();
    private InteractionGroup _group=InteractionCatalog.Groups[0];

    private StyleBoxFlat RoomStyle(string color,int radius=12,int pad=14,string? border=null)
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
        var b=new Button{Text=value,FocusMode=Control.FocusModeEnum.None,CustomMinimumSize=new(0,38),MouseDefaultCursorShape=Control.CursorShape.PointingHand};
        b.AddThemeStyleboxOverride("normal",RoomStyle(active?"566e5d":"eae9d9",8,9));
        b.AddThemeStyleboxOverride("hover",RoomStyle(active?"647d6a":"dedfca",8,9));
        b.AddThemeStyleboxOverride("pressed",RoomStyle("c6cfb9",8,9));
        b.AddThemeColorOverride("font_color",new Color(active?"fff7df":"405247"));
        b.Pressed+=action;return b;
    }
    private void SetQuickToolHighlight(Button button,bool active)
    {
        button.AddThemeStyleboxOverride("normal",RoomStyle(active?"566e5d":"eae9d9",8,9));
        button.AddThemeStyleboxOverride("hover",RoomStyle(active?"647d6a":"dedfca",8,9));
        var textColor=new Color(active?"fff7df":"405247");
        button.AddThemeColorOverride("font_color",textColor);
        button.AddThemeColorOverride("font_hover_color",textColor);
        button.AddThemeColorOverride("font_pressed_color",new Color("405247"));
    }
    private PanelContainer Panel(Control parent,string color="f6f2e5")
    {var p=new PanelContainer();p.AddThemeStyleboxOverride("panel",RoomStyle(color));parent.AddChild(p);return p;}
    private VBoxContainer Column(Control parent,int gap=10)
    {var v=new VBoxContainer();v.AddThemeConstantOverride("separation",gap);parent.AddChild(v);return v;}
    private HBoxContainer Row(Control parent,int gap=8)
    {var r=new HBoxContainer();r.AddThemeConstantOverride("separation",gap);parent.AddChild(r);return r;}

    private void BuildRoomUi()
    {
        var layer=new CanvasLayer();AddChild(layer);
        _ui=new Control{MouseFilter=Control.MouseFilterEnum.Ignore,Theme=new Theme{DefaultFont=_font,DefaultFontSize=15}};
        layer.AddChild(_ui);_ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var top=Panel(_ui);top.AnchorRight=1;top.OffsetLeft=14;top.OffsetTop=12;top.OffsetRight=-14;top.OffsetBottom=76;
        var header=Row(top,16);var identity=Column(header,0);identity.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        identity.AddChild(Text("慢慢来，相处小院",23));identity.AddChild(Text("人油互动 · 第一批试玩",12,"7c8876"));
        _clockLabel=Text("",13);_clockLabel.CustomMinimumSize=new(190,0);header.AddChild(_clockLabel);
        BuildYardHelpButton(header);
        _pauseButton=Button("停一停",()=>TogglePause());header.AddChild(_pauseButton);
        header.AddChild(Button("调试 · F3",ToggleDebugPanel));
        header.AddChild(Button("怎么玩",ShowHelp));header.AddChild(Button("存档与设置",ShowSettings));

        _leftPanel=Panel(_ui);_leftPanel.OffsetLeft=14;_leftPanel.OffsetTop=88;_leftPanel.OffsetRight=218;_leftPanel.AnchorBottom=1;_leftPanel.OffsetBottom=-218;
        var left=Column(_leftPanel,9);BuildToolbar(left);

        _rightPanel=Panel(_ui);_rightPanel.AnchorLeft=1;_rightPanel.AnchorRight=1;_rightPanel.AnchorBottom=1;
        _rightPanel.OffsetLeft=-274;_rightPanel.OffsetRight=-14;_rightPanel.OffsetTop=88;_rightPanel.OffsetBottom=-218;
        var right=Column(_rightPanel,10);right.AddChild(Text("正在相处",12,"8b927f"));
        BuildOilSelector(right);
        _who=Text("",21);right.AddChild(_who);
        var viewControls=Row(right,5);
        _closeButton=Button("靠近看看 · 双击",ToggleClose);_closeButton.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;viewControls.AddChild(_closeButton);
        _resetViewButton=Button("正面",ResetCloseView);viewControls.AddChild(_resetViewButton);
        _familyPanelButton=Button("详细家庭记录",()=>{_familyFirstId=Sim.Selected?.Id;ShowFamilyDetails();});right.AddChild(_familyPanelButton);
        var reportScroll=new ScrollContainer{SizeFlagsVertical=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};right.AddChild(reportScroll);
        var report=Column(reportScroll,14);report.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        BuildDetachedStemPanel(report);
        BuildSupplyStationPanel(report);
        _objectDetails=Text("",14);report.AddChild(_objectDetails);
        _objectActions=Column(_ui,4);_objectActions.CustomMinimumSize=new(176,0);
        BuildVitalMeters(report);
        _condition=Text("",14);report.AddChild(_condition);
        _memoryHeading=Text("刚才发生的事",12,"8b927f");report.AddChild(_memoryHeading);_memory=Text("",13);report.AddChild(_memory);

        _bottomPanel=Panel(_ui);_bottomPanel.AnchorTop=1;_bottomPanel.AnchorBottom=1;_bottomPanel.AnchorRight=1;
        _bottomPanel.OffsetLeft=14;_bottomPanel.OffsetRight=-14;_bottomPanel.OffsetTop=-206;_bottomPanel.OffsetBottom=-12;
        var bottom=Column(_bottomPanel,7);var titleRow=Row(bottom,16);
        _title=Text("",19);_title.AutowrapMode=TextServer.AutowrapMode.Off;_title.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;titleRow.AddChild(_title);
        _forceLabel=Text("",13);_forceLabel.AutowrapMode=TextServer.AutowrapMode.Off;_forceLabel.CustomMinimumSize=new(90,24);titleRow.AddChild(_forceLabel);
        _forceSlider=new HSlider{MinValue=.1,MaxValue=1,Step=.05,Value=_force,CustomMinimumSize=new(150,24),FocusMode=Control.FocusModeEnum.None};
        _forceSlider.ValueChanged+=v=>{_force=(float)v;_toolForces[_action]=_force;RefreshRoomUi();};titleRow.AddChild(_forceSlider);
        BuildContactProgress(titleRow);
        _choiceList=new HFlowContainer();_choiceList.AddThemeConstantOverride("h_separation",7);bottom.AddChild(_choiceList);
        _hint=Text("",13,"78816e");bottom.AddChild(_hint);
        _feedback=Text("",15);bottom.AddChild(_feedback);

        var speechPanel=Panel(_ui,"f6f2e5e8");speechPanel.AnchorLeft=.28f;speechPanel.AnchorRight=.70f;
        speechPanel.OffsetTop=100;speechPanel.OffsetBottom=164;speechPanel.MouseFilter=Control.MouseFilterEnum.Ignore;
        speechPanel.AddThemeStyleboxOverride("panel",RoomStyle("f6f2e5e8",12,10));
        _speechLabel=Text("慢慢来……",18);_speechLabel.HorizontalAlignment=HorizontalAlignment.Center;speechPanel.AddChild(_speechLabel);
        _hoverLabel=Text("",14);_hoverLabel.AutowrapMode=TextServer.AutowrapMode.Off;_hoverLabel.AddThemeStyleboxOverride("normal",RoomStyle("f6f2e5e8",6,6));_ui.AddChild(_hoverLabel);
        _hoverLabel.Visible=false;
        _pointer=new RoomPointer{MouseFilter=Control.MouseFilterEnum.Ignore};_ui.AddChild(_pointer);_pointer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        BuildDebugPanel();
        _modalShade=new ColorRect{Color=new Color(0.10f,.16f,.12f,.64f),Visible=false};_ui.AddChild(_modalShade);_modalShade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _modalPanel=Panel(_modalShade);_modalPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);_modalPanel.OffsetLeft=-310;_modalPanel.OffsetRight=310;_modalPanel.OffsetTop=-290;_modalPanel.OffsetBottom=290;
        var modalScroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};_modalPanel.AddChild(modalScroll);
        _modalContents=Column(modalScroll,12);_modalContents.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        BuildRecycleBox();
        SelectToolbarCategory(0);
    }

    private static void ClearChildren(Node parent){foreach(var n in parent.GetChildren()){parent.RemoveChild(n);n.QueueFree();}}
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
        if(action is InteractionAction.PlaceFoodStation or InteractionAction.PlaceWaterStation)
        {ArmSupplyStationPlacement(action==InteractionAction.PlaceFoodStation?SupplyStationKind.Food:SupplyStationKind.Water);return;}
        if(action is not (InteractionAction.RefillSupplyStation or InteractionAction.Lift))ClearSupplyStationSelection();
        CancelGesture();_action=action;_toolArmed=true;ClearChildren(_choiceList);
        _force=_toolForces.TryGetValue(action,out float rememberedForce)?rememberedForce:.25f;_forceSlider.SetValueNoSignal(_force);
        _group=InteractionCatalog.Group(action);RebuildToolbarChoices();
        _title.Text=_group.Name+" · "+InteractionCatalog.Choice(_action).Label;
        _hint.Text=ToolHint(action);
        _message=_group.Description;RefreshRoomUi();
        if(!_modalOpen&&SupplyStockMissing(action))ShowMissingToolSupplies(action);
    }
    private static string ToolHint(InteractionAction action)=>
        action is InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Drink?
            "点油：递到嘴边，等它接下一口；点地面：放下一份。右键收回，换工具也会停止递送。":InteractionCatalog.Choice(action).Hint;
    private void DisarmTool()=>SelectToolbarCategory(0);
    private void SelectOil(string id)
    {
        ClearSupplyStationSelection();
        _selectedRoomObjectId=null;
        _observedStemMotherId=null;
        _selectedDetachedStemEventId=null;
        CancelGesture();Sim.State.SelectedId=id;_familyFirstId=id;
        bool leftClose=LeaveUnavailableCloseup();
        _message=Sim.Selected!.Family.Memorialized?"正在查看"+Sim.Selected.Name+"留下的家庭记录。":
            Sim.Selected.Family.ColdStorage.Stored?"它仍在冷藏中，可从家庭与照护面板取出。":"正在观察"+Sim.Selected.Name+"。";
        if(leftClose)_message+=" 已回到小院视角，原来的暂停设置保持不变。";
        RefreshRoomUi();
    }
    private void ToggleClose()
    {
        if(_selectedDetachedStemEventId!=null||_selectedSupplyStationId!=null)return;
        if(Sim.State.Mode!=PlayMode.CloseUp&&Sim.Selected is { } selected&&!HasVisibleFamilyBody(selected))
        {_message=selected.Family.Memorialized?"遗体已经安置，可以在家庭面板查看留下的记录。":"它正在冷藏中。请先从家庭与照护面板取出，再靠近观察。";RefreshRoomUi();return;}
        CancelGesture();Sim.State.Mode=Sim.State.Mode==PlayMode.CloseUp?PlayMode.Management:PlayMode.CloseUp;
        _message=Sim.State.Mode==PlayMode.CloseUp?"近看时自然时间暂停。左键按住环境或中键拖动转视角，左键按住小油进行互动；滚轮拉近拉远。":"回到小院，继续观察它自己会做什么。";
        RefreshRoomUi();
    }
    private void TogglePause(){CancelGesture();Sim.State.Settings.Pause=!Sim.State.Settings.Pause;RefreshRoomUi();}
    private void ResetCloseView()
    {
        if(Sim.State.Mode!=PlayMode.CloseUp)
        {
            CancelGesture();_yardFocus=Vector3.Zero;_zoom=16;
            _message="回到小院中央。按住中键拖动画面，滚轮拉近拉远。";
            RefreshRoomUi();return;
        }
        CancelGesture();_closeYaw=0;_closePitch=InteractionCameraOrbit.DefaultPitch;_closeZoom=1;
        _message=Sim.Selected?.Interaction.Carried==true?"回到正面观察，手里仍然稳稳托着它。":"回到正面观察，已经收手；可以重新对准想接触的位置。";
        RefreshRoomUi();
    }
    private void RefreshRoomUi()
    {
        if(_who==null||Sim==null)return;
        NormalizeDetachedStemSelection();
        NormalizeSupplyStationSelection();
        RefreshOilSelector();
        RefreshDebugPanel();
        RefreshSupplyStock();
        RefreshYardHelpButton();
        _choiceList.Visible=_toolbarCategory!=0;
        var c=Sim.Selected;
        _clockLabel.Text=Sim.State.Settings.Pause?"小院暂停中":Sim.State.Mode==PlayMode.CloseUp?"近看 · 自然时间暂停":"小院时间  "+TimeSpan.FromSeconds(Sim.State.WorldSeconds).ToString(@"mm\:ss");
        _pauseButton.Text=Sim.State.Settings.Pause?"继续":"停一停";_closeButton.Text=Sim.State.Mode==PlayMode.CloseUp?"回到小院 · Tab":"靠近看看 · 双击";
        _resetViewButton.Text=Sim.State.Mode==PlayMode.CloseUp?"正面":"居中";
        float shownForce=_mouseHeld?_appliedForce:_force;
        _forceLabel.Text="接触 · "+(shownForce<.35?"轻":shownForce<.62?"稍重":"用力");
        _forceSlider.Visible=_toolArmed&&InteractionCatalog.UsesForce(_action);
        _forceLabel.Visible=_forceSlider.Visible||_mouseHeld&&_action==InteractionAction.Touch;
        _feedback.Text=_message;
        var heldSupply=_heldSupplyId==null?null:Sim.State.RoomObjects.Find(o=>o.Id==_heldSupplyId&&o.HeldByHand);
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
                c.Family.ColdStorage.Stored?"从家庭与照护面板取出，才能再次靠近；冷藏只会有限延缓。":
                carried?"按住身体继续搬运，慢慢松手放下；快速甩动后松手会抛出。Shift＋滚轮调高度，也可在空地单击放稳。":"按住身体抓起拖动，慢慢松手放下；快速甩动后松手会抛出。空手拿食水、推球；双击近看。";
        }
        else _hint.Text=ToolHint(_action);
        if(c==null)
        {
            _who.Text="观察小院";_condition.Text=_memory.Text="";
            _speechLabel.Text="选择油或离体茎，查看当前状态。";
            RefreshDetachedStemUi();RefreshSupplyStationUi();RefreshRedesignProperties();RefreshContactProgress();return;
        }
        var i=c.Interaction;
        RefreshVitalMeters(c);
        _who.Text=c.Name+" · "+SpeciesName(c.Body.SpeciesId)+(c.IsYoung?"幼体":"");
        string intake=i.NaturalIntakePending?i.EatingKind==IntakeKind.Water?"准备喝这一口":"准备取这一口":
            i.EatingKind==IntakeKind.Water?"慢慢喝着这一口":i.GroundIntake&&i.GroundIntakeKind==RoomObjectKind.Leaves?"慢慢嚼着叶子":"慢慢嚼着这一口";
        string activity=i.EatingRemaining>0?intake:i.Resting?"慢慢休息":c.Activity;
        if(!WorldSimulation.CanSelfMove(c)&&(c.Condition is PhysicalCondition.Active or PhysicalCondition.Recovering))
            activity=activity.Length==0?"暂时无法自己挪动":activity+" · 暂时无法自己挪动";
        string state=c.Family.Memorialized?"已经安置，家人与经历仍保留":c.Condition==PhysicalCondition.Dead?
            (c.Family.ColdStorage.Stored?"遗体仍在冷藏中":"不再回应了"):c.Family.ColdStorage.Stored?"冷藏中，需及时取出照护":c.Condition==PhysicalCondition.Critical?"身体状态很差，急需护理":
            i.Physiology.Active?i.Physiology.Excreting==ExcretionKind.Urine?"正在撒尿":"正在排便":
            i.Falling?"正在下落":i.Carried?"正在被托着":i.Restraint switch{RestraintKind.Box=>"留在关着的箱子里",RestraintKind.Frame=>"被固定具限制着",RestraintKind.Hanging=>"留在悬挂装置上",RestraintKind.Wrap=>"被软布包裹着",RestraintKind.Hand=>"被手按住",_=>i.Supine?"身体翻了过来":activity};
        if(c.Condition is PhysicalCondition.Active or PhysicalCondition.Recovering&&!i.Falling&&
            (i.Carried||i.Restraint!=RestraintKind.None||i.Supine)&&(i.EatingRemaining>0||i.Resting))state+=" · "+activity;
        string needs=c.Fullness<22?"肚子饿了，想吃一点。":c.Fullness>90?"肚子圆圆的，暂时不想再吃。":"";
        if(c.Energy<30)needs+=(needs.Length>0?"\n":"")+"有些疲倦，需要休息。";
        if(i.Hydration<25)needs+=(needs.Length>0?"\n":"")+"口渴了，需要喝一点水。";
        if(i.Digestion.Symptomatic)needs+=(needs.Length>0?"\n":"")+(i.Digestion.CareRemaining>0?"肚子不适已经照护，仍需吃喝和休息。":"肚子不舒服，留意吃喝和休息。");
        if(i.PendingConversation.HasValue)needs+=(needs.Length>0?"\n":"")+(i.EatingRemaining>0?"等这一口咽下后再回应，不必重复催它。":i.Resting?"正在歇着，等它慢慢回应。":"已听见你的话，等它慢慢回应。");
        foreach(var clue in InteractionSurfaceObservation.Describe(i))needs+=(needs.Length>0?"\n":"")+clue+"。";
        string mood=c.Pain>25||c.Expression==Expression.Hurt?"身体还在疼，需要照护。":
            c.ReactionRemaining>0&&c.Expression==Expression.Calling?"正在请求帮助，先听听它需要什么。":
            c.ReactionRemaining>0&&i.ReactionRefused?"这次不愿意继续，先给它一点空间。":
            c.Fear>60?"它还有些害怕，先留一点距离。":i.Emotion.Alarm>=12||i.Water.RecoverySeconds>0?
            "它还记得刚才的惊吓，需要持续安稳地相处。":i.Resting?"先让它安静歇一会儿。":!i.HasAccessory?
            (i.Accessory.BorrowedForCare?"等你整理好饰品，再帮它戴回去。":"还惦记着离开头顶的饰品。"):
            i.Accessory.Integrity<90?"仍在意饰品上的破损。":c.Resentment>40?"它还有些不满。":c.Trust>65?"它对你比较放心。":"它正在慢慢熟悉你。";
        string accessory=i.HasAccessory?(i.Accessory.Integrity<70?"饰品有破损，仍戴在头上。":"饰品好好地戴着。"):
            i.Accessory.Integrity<=0?"原来的饰品已经损坏。":i.Accessory.Place switch{AccessoryPlace.Stored=>"饰品被收起来了。",AccessoryPlace.Hung=>"饰品挂在高处。",_=>"饰品离开了头顶。"};
        _condition.Text=c.Condition==PhysicalCondition.Dead?state+"\n\n"+accessory:state+"\n\n"+mood+(needs.Length>0?"\n"+needs:"")+"\n\n"+accessory+
            (i.FootIntegrity<70?"\n"+(i.FootTreated?"伤处已经护理，仍需慢慢养好。":"移动部位不舒服，需要看看。") :"");
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
        _memory.Text=c.PersonalMemories.Count==0?"还没有一起经历很多事。":string.Join("\n\n",c.PersonalMemories.TakeLast(3).Reverse().Select(m=>m.Text));
        string speech=c.Condition==PhysicalCondition.Critical?c.Speech:MouthFunction.FormatSpeech(c,c.Speech);
        _speechLabel.Text=c.Condition!=PhysicalCondition.Dead&&c.ReactionRemaining>0&&speech.Length>0?c.Name+"：「"+speech+"」":c.Name+" · "+state;
        RefreshDetachedStemUi();
        RefreshSupplyStationUi();
        RefreshRedesignProperties();
        RefreshContactProgress();
    }

    private void OpenModal(string title)
    {
        CancelGesture();if(!_modalOpen)_pauseBeforeModal=Sim.State.Settings.Pause;
        _modalOpen=true;Sim.State.Settings.Pause=true;_modalShade.Visible=true;ClearChildren(_modalContents);
        _modalContents.AddChild(Text(title,24));
    }
    private void CloseModal(){_modalOpen=false;_modalShade.Visible=false;Sim.State.Settings.Pause=_pauseBeforeModal;RefreshRoomUi();}
    private void ShowModalMessage(string message){OpenModal("没有完成这次操作");_modalContents.AddChild(Text(message));_modalContents.AddChild(Button("知道了",CloseModal));}
    private void ShowHelp()
    {
        OpenModal("小院操作");
        foreach(var line in new[]{
            "观察只选中对象。空手分为抓取、抚摸、收拾、戳碰、按压。右键停止使用工具并回到观察。",
            "抓取按住拖动，松手放下；快速甩动后松手抛出。抓取时右下浮出收容箱，物品入仓不退款。",
            "抚摸停在头顶或脸侧即可接触，按住轻拖可贴地挪动。戳碰轻点部位，空地按住后挥向身体按速度击打，长按部位拖动可拉拽。",
            "按压从身体下半部向上推可推翻；身旁按住与解除按压按钮控制持续按压。切换工具不解除固定装置。",
            "饭饭、零食、清水点油手喂，点地放下。零食停在油身边可逗食。所有清水使用同一份有限库存。",
            "供给器点击查看补充频率，按时从仓库取食水，缺货停止补充。商店购买实际花钱，不自动采购。",
            "玩具包括软球、小宝物、包裹、伸展。包裹后按住缓慢左右轻摇，身旁可解开。",
            "交流可招呼、训诫、唱歌、教学、指令、相处。教学目前示范自己清扫；相处依次指定两只油，抓取会打断。",
            "养护可擦拭、检查、整理饰品、护理面粉、救护橙汁、冷藏复苏、埋葬。取下的饰品拖回原主身体即可归还。",
            "繁育入口查看个体许可、使用成熟个体喷剂和照护油茎。全局自主繁育开关沿用旧设置，不等于个体行为要求。",
            "中键拖动画面，滚轮缩放；Shift＋滚轮调支点高度。空格暂停，Tab近看。每分钟自动保存，读档不结算离线时间。"})_modalContents.AddChild(Text(line,14));
        _modalContents.AddChild(Button("返回",CloseModal));
    }
    private void ShowSettings()
    {
        OpenModal("存档与设置");
        _modalContents.AddChild(Text("相处小院使用独立存档，保存各自的身体、饰品和经历。",14));
        var slots=Row(_modalContents);for(int n=1;n<=3;n++){int slot=n;slots.AddChild(Button("槽位 "+n,()=>{_slot=slot;ShowSettings();},n==_slot));}
        var actions=Row(_modalContents);actions.AddChild(Button("保存到槽位 "+_slot,()=>{if(SaveRoom(_slot))CloseModal();else ShowModalMessage(_message);}));actions.AddChild(Button("读取槽位 "+_slot,()=>LoadRoom(_slot)));
        _modalContents.AddChild(Button("读取最近的自动存档",()=>LoadRoom(0)));
        _modalContents.AddChild(Button(_muted?"声音：关":"声音：开",()=>{SetRoomMuted(!_muted);ShowSettings();}));
        _modalContents.AddChild(Button(Sim.State.Settings.LethalInjuries?"致命后果：开":"致命后果：关",()=>{Sim.State.Settings.LethalInjuries=!Sim.State.Settings.LethalInjuries;ShowSettings();}));
        _modalContents.AddChild(Button(Sim.State.AutonomousBreeding?"全局自主繁育：开":"全局自主繁育：关",()=>{Sim.State.AutonomousBreeding=!Sim.State.AutonomousBreeding;ShowSettings();}));
        _modalContents.AddChild(Button("重新开一间小院",()=>
        {
            OpenModal("重新开始这次相处？");_modalContents.AddChild(Text("当前状态先保存到自动存档，手动槽位保留。新小院的三只油将从初始状态开始。"));
            _modalContents.AddChild(Button("保存并重新开始",()=>
            {
                if(!SaveRoom(0,false)){ShowModalMessage(_message);return;}
                Sim=WorldSimulation.CreateInteractionPlaytest();_recovered=false;_pauseBeforeModal=false;BuildActors();CloseModal();
            }));
            _modalContents.AddChild(Button("继续原来的相处",CloseModal));
        }));
        _modalContents.AddChild(Button("关闭面板",CloseModal));
    }
    private void ShowMedicineSupplies(string? result=null,InteractionAction equipAction=InteractionAction.Care)=>ShowShop(result,"养护用品");
    private void ShowRescueSupplies(string? result=null)=>ShowShop(result,"养护用品");
}
