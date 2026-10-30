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
    private VBoxContainer _quickTools=null!;
    private HFlowContainer _choiceList=null!;
    private Label _title=null!,_hint=null!,_feedback=null!,_speechLabel=null!,_who=null!,_condition=null!,_memory=null!,_clockLabel=null!,_hoverLabel=null!,_forceLabel=null!;
    private Button _closeButton=null!,_pauseButton=null!,_gentleTab=null!,_roughTab=null!,_resetViewButton=null!;
    private bool _roughTabSelected,_modalOpen,_pauseBeforeModal;
    private bool _browseTools,_directToolMode;
    private HSlider _forceSlider=null!;
    private readonly System.Collections.Generic.Dictionary<InteractionAction,float> _toolForces=new();
    private readonly System.Collections.Generic.Dictionary<InteractionAction,Button> _directToolButtons=new();
    private InteractionAction? _highlightedDirectTool;
    private bool _directHighlightInitialized;
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
        var left=Column(_leftPanel,9);left.AddChild(Text("手边物品",16));
        left.AddChild(Button("空手 · 直接接触",DisarmTool));var tabs=Row(left,5);
        _gentleTab=Button("爱油",()=>SetToolTab(false),true);_gentleTab.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;tabs.AddChild(_gentleTab);
        _roughTab=Button("虐油",()=>SetToolTab(true));_roughTab.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;tabs.AddChild(_roughTab);
        var scroll=new ScrollContainer{SizeFlagsVertical=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};left.AddChild(scroll);
        var toolLists=Column(scroll,6);toolLists.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        _quickTools=Column(toolLists,6);
        foreach(var entry in new[]{("手掌 · 拖拽搬动",InteractionAction.Lift),("抚摸 · 轻轻划过",InteractionAction.Stroke),
            ("拳头 · 点击击打",InteractionAction.Punch),("火机 · 按住灼烧",InteractionAction.Burn),
            ("零食 · 点油喂 / 点地放",InteractionAction.FeedSweet),("饭饭 · 点油喂 / 点地放",InteractionAction.FeedMeal),
            ("清水 · 点油喂 / 点地放",InteractionAction.Drink)})
        {var action=entry.Item2;var button=Button(entry.Item1,()=>SelectQuickTool(action));_directToolButtons[action]=button;_quickTools.AddChild(button);}
        _quickTools.AddChild(Button("救援用品 · 点油救起",ArmWaterRescueTool));
        _quickTools.AddChild(Button("购买救援用品",()=>ShowRescueSupplies()));
        foreach(var entry in new[]{("说话","P01"),("毛巾","P07"),("护理用品","P11"),("包裹","P05"),("球与小宝物","P09")})
        {string id=entry.Item2;_quickTools.AddChild(Button(entry.Item1,()=>SelectGroup(InteractionCatalog.Groups.Single(g=>g.Id==id))));}
        _groupList=Column(toolLists,6);_groupList.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        left.AddChild(Button("展开 / 收起全部工具",()=>{_browseTools=!_browseTools;RefreshToolBrowser();}));
        left.AddChild(Text("右键切回空手。\n按住抓起，松手放下；甩手抛出。\n空手拿食水、推球；双击近看。\n小院中键拖动画面，滚轮缩放。",12,"7c8876"));

        _rightPanel=Panel(_ui);_rightPanel.AnchorLeft=1;_rightPanel.AnchorRight=1;_rightPanel.AnchorBottom=1;
        _rightPanel.OffsetLeft=-274;_rightPanel.OffsetRight=-14;_rightPanel.OffsetTop=88;_rightPanel.OffsetBottom=-218;
        var right=Column(_rightPanel,10);right.AddChild(Text("正在相处",12,"8b927f"));
        BuildOilSelector(right);
        _who=Text("",21);right.AddChild(_who);
        var viewControls=Row(right,5);
        _closeButton=Button("靠近看看 · 双击",ToggleClose);_closeButton.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;viewControls.AddChild(_closeButton);
        _resetViewButton=Button("正面",ResetCloseView);viewControls.AddChild(_resetViewButton);
        right.AddChild(Button("同伴、家庭与照护",()=>{_familyFirstId=Sim.Selected?.Id;ShowFamily();}));
        var reportScroll=new ScrollContainer{SizeFlagsVertical=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};right.AddChild(reportScroll);
        var report=Column(reportScroll,14);report.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        BuildVitalMeters(report);
        _condition=Text("",14);report.AddChild(_condition);
        report.AddChild(Text("刚才发生的事",12,"8b927f"));_memory=Text("",13);report.AddChild(_memory);

        _bottomPanel=Panel(_ui);_bottomPanel.AnchorTop=1;_bottomPanel.AnchorBottom=1;_bottomPanel.AnchorRight=1;
        _bottomPanel.OffsetLeft=14;_bottomPanel.OffsetRight=-14;_bottomPanel.OffsetTop=-206;_bottomPanel.OffsetBottom=-12;
        var bottom=Column(_bottomPanel,7);var titleRow=Row(bottom,16);
        _title=Text("",19);_title.AutowrapMode=TextServer.AutowrapMode.Off;_title.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;titleRow.AddChild(_title);
        _forceLabel=Text("",13);_forceLabel.AutowrapMode=TextServer.AutowrapMode.Off;_forceLabel.CustomMinimumSize=new(90,24);titleRow.AddChild(_forceLabel);
        _forceSlider=new HSlider{MinValue=.1,MaxValue=1,Step=.05,Value=_force,CustomMinimumSize=new(150,24),FocusMode=Control.FocusModeEnum.None};
        _forceSlider.ValueChanged+=v=>{_force=(float)v;_toolForces[_action]=_force;RefreshRoomUi();};titleRow.AddChild(_forceSlider);
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
        SetToolTab(false);
        RefreshToolBrowser();
    }

    private static void ClearChildren(Node parent){foreach(var n in parent.GetChildren()){parent.RemoveChild(n);n.QueueFree();}}
    private void RefreshToolBrowser()
    {
        _quickTools.Visible=!_browseTools;_groupList.Visible=_browseTools;
        _gentleTab.Visible=_roughTab.Visible=_browseTools;
    }
    private void SetToolTab(bool rough)
    {
        CancelGesture();_roughTabSelected=rough;ClearChildren(_groupList);
        foreach(var pair in new[]{(_gentleTab,!rough),(_roughTab,rough)})
        {
            pair.Item1.AddThemeStyleboxOverride("normal",RoomStyle(pair.Item2?"566e5d":"eae9d9",8,9));
            pair.Item1.AddThemeColorOverride("font_color",new Color(pair.Item2?"fff7df":"405247"));
        }
        foreach(var g in InteractionCatalog.Groups.Where(g=>g.Rough==rough))
        {
            var group=g;var b=Button(group.Name,()=>SelectGroup(group),group.Id==_group.Id);
            b.Alignment=HorizontalAlignment.Left;_groupList.AddChild(b);
        }
        if(_group.Rough!=rough)SelectGroup(InteractionCatalog.Groups.First(g=>g.Rough==rough));else SelectGroup(_group);
    }
    private void SelectGroup(InteractionGroup group)
    {
        CancelGesture();_directToolMode=false;_group=group;SelectAction(group.Choices[0].Action);
        foreach(var child in _groupList.GetChildren())if(child is Button b)
        {bool active=b.Text==group.Name;b.AddThemeStyleboxOverride("normal",RoomStyle(active?"566e5d":"eae9d9",8,9));b.AddThemeColorOverride("font_color",new Color(active?"fff7df":"405247"));}
    }
    private void SelectQuickTool(InteractionAction action)
    {
        var group=InteractionCatalog.Groups.First(g=>g.Choices.Any(choice=>choice.Action==action));
        _group=group;SetToolTab(group.Rough);SelectAction(action);_directToolMode=true;RefreshRoomUi();
    }
    private void SelectAction(InteractionAction action)
    {
        CancelGesture();_action=action;_toolArmed=true;ClearChildren(_choiceList);
        _force=_toolForces.TryGetValue(action,out float rememberedForce)?rememberedForce:.25f;_forceSlider.SetValueNoSignal(_force);
        foreach(var option in _group.Choices){var a=option.Action;_choiceList.AddChild(Button(option.Label,()=>SelectAction(a),a==action));}
        _title.Text=_group.Name+" · "+InteractionCatalog.Choice(_action).Label;
        _hint.Text=action is InteractionAction.FeedMeal or InteractionAction.FeedSweet or InteractionAction.Drink?
            "点油：递到嘴边，等它接下一口；点地面：放下一份。右键收回，换工具也会停止递送。":InteractionCatalog.Choice(_action).Hint;
        _message=_group.Description;RefreshRoomUi();
    }
    private void DisarmTool()
    {
        CancelGesture();_toolArmed=false;
        _message=Sim?.Selected?.Interaction.Carried==true?"仍在稳稳托着它。在空地上点一下放稳。":"手已经收回。可以观察它自己生活，也可以直接轻轻接触。";
        foreach(var child in _choiceList.GetChildren())if(child is Button b)
        {b.AddThemeStyleboxOverride("normal",RoomStyle("eae9d9",8,9));b.AddThemeColorOverride("font_color",new Color("405247"));}
        RefreshRoomUi();
    }
    private void SelectOil(string id)
    {
        CancelGesture();Sim.State.SelectedId=id;_familyFirstId=id;
        bool leftClose=LeaveUnavailableCloseup();
        _message=Sim.Selected!.Family.Memorialized?"正在查看"+Sim.Selected.Name+"留下的家庭记录。":
            Sim.Selected.Family.ColdStorage.Stored?"它仍在冷藏中，可从家庭与照护面板取出。":"正在观察"+Sim.Selected.Name+"。";
        if(leftClose)_message+=" 已回到小院视角，原来的暂停设置保持不变。";
        RefreshRoomUi();
    }
    private void ToggleClose()
    {
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
        RefreshOilSelector();
        RefreshDebugPanel();
        RefreshYardHelpButton();
        _choiceList.Visible=_toolArmed&&!_directToolMode&&!_waterRescueToolArmed;
        InteractionAction? highlighted=_toolArmed&&!_waterRescueToolArmed?_action:null;
        if(!_directHighlightInitialized||highlighted!=_highlightedDirectTool)
        {
            _directHighlightInitialized=true;_highlightedDirectTool=highlighted;
            foreach(var pair in _directToolButtons)
            {
                bool active=highlighted==pair.Key;
                pair.Value.AddThemeStyleboxOverride("normal",RoomStyle(active?"566e5d":"eae9d9",8,9));
                pair.Value.AddThemeColorOverride("font_color",new Color(active?"fff7df":"405247"));
            }
        }
        var c=Sim.Selected;if(c==null)return;var i=c.Interaction;
        RefreshVitalMeters(c);
        if(_waterRescueToolArmed)
        {
            _title.Text="救援用品";
            _hint.Text=$"点击落水的油救回岸上；右键收回。每次消耗1份，当前 {Sim.State.WaterRescueTools} 份；可在“购买救援用品”补充。";
        }
        else if(!_toolArmed)
        {
            _title.Text=c.Family.Memorialized?"家人记录":c.Family.ColdStorage.Stored?"冷藏保存":i.Carried?"托着 · 先看看它":"空手 · 先看看它";
            _hint.Text=c.Family.Memorialized?"家庭面板保留名字、亲缘与共同经历。选择小院里的另一只，可以继续相处。":
                c.Family.ColdStorage.Stored?"从家庭与照护面板取出，才能再次靠近；冷藏只会有限延缓。":
                i.Carried?"按住身体继续搬运，慢慢松手放下；快速甩动后松手会抛出。Shift＋滚轮调高度，也可在空地单击放稳。":"按住身体抓起拖动，慢慢松手放下；快速甩动后松手会抛出。空手拿食水、推球；双击近看。";
        }
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
        _clockLabel.Text=Sim.State.Settings.Pause?"小院暂停中":Sim.State.Mode==PlayMode.CloseUp?"近看 · 自然时间暂停":"小院时间  "+TimeSpan.FromSeconds(Sim.State.WorldSeconds).ToString(@"mm\:ss");
        _pauseButton.Text=Sim.State.Settings.Pause?"继续":"停一停";_closeButton.Text=Sim.State.Mode==PlayMode.CloseUp?"回到小院 · Tab":"靠近看看 · 双击";
        _resetViewButton.Text=Sim.State.Mode==PlayMode.CloseUp?"正面":"居中";
        float shownForce=_mouseHeld?_appliedForce:_force;
        _forceLabel.Text="接触 · "+(shownForce<.35?"轻":shownForce<.62?"稍重":"用力");
        _forceSlider.Visible=_toolArmed&&InteractionCatalog.UsesForce(_action);
        _forceLabel.Visible=_forceSlider.Visible||_mouseHeld&&_action==InteractionAction.Touch;
        _feedback.Text=_message;
        string speech=c.Condition==PhysicalCondition.Critical?c.Speech:MouthFunction.FormatSpeech(c,c.Speech);
        _speechLabel.Text=c.Condition!=PhysicalCondition.Dead&&c.ReactionRemaining>0&&speech.Length>0?c.Name+"：「"+speech+"」":c.Name+" · "+state;
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
        OpenModal("先慢慢熟悉它");
        _modalContents.AddChild(Text("1  右键或点“空手”收回工具。空手按住身体抓起，拖动搬运；慢慢松手放下，快速甩动后松手会沿甩动方向抛出，速度越快力度越大。停住后再松手不会沿用之前的甩动速度。"));
        _modalContents.AddChild(Text("2  左侧直接选择手掌、抚摸、拳头、火机或食水。手掌按住身体拖动搬运，松开放下；落点被占用或鼠标离开操作区时继续托着，在空地再点一下放稳。抚摸按住轻轻划过；拳头连续点击；火机按住实际接触部位，松手就停止。按住托举时 Shift＋滚轮调高度。"));
        _modalContents.AddChild(Text("3  零食、饭饭或清水工具：点油是递到嘴边手喂，松开鼠标也会递完这一份；点地面是放下一份。它需要时间接食，也可能拒绝，嘴边不能被障碍挡住。换工具、右键、暂停或离开窗口会收回递送。空手也能拿起地上的食水：递到嘴边等它接一口，移到空地松开放下；右键把剩余食水放回原处。"));
        _modalContents.AddChild(Text("4  小院里按住中键拖动画面，滚轮缩放，“居中”回到中央。空手双击小油，或按 Tab 近看；使用工具时双击仍是操作工具。近看暂停自然时间，回到小院才继续自主找食水、跟随、休息和恢复。近看时用中键转视角，空手也可左键按住环境拖动；装备工具时左键按工具规则交互。滚轮调远近，“正面”恢复视角。转镜头时托举仍保持支撑；要放下小油或放置物品，在空地单击后松开，不要拖动。"));
        _modalContents.AddChild(Text("5  空手按住软球，拖动后松开推球。跟随练习时移动鼠标指路，不用操作人类角色。空格暂停；Del 收起指向的可收纳物品，树叶、河水和软垫不能收起。"));
        _modalContents.AddChild(Text("6  固定具、箱子和悬挂装置要用各自的解除动作；切回空手不会解除装置。脚脚受伤时，把食水放到能吃到的地方，再护理和休息。左右脸可以分别擦洗和护理。"));
        _modalContents.AddChild(Text("7  油可以落入水中并挣扎，长时间泡水会恶化。选择“救援用品”后点击落水的油，带回有空位的岸边；每次成功救助用掉1份。左侧“购买救援用品”可以补充，测试阶段不扣金钱。救起后仍要擦干、检查伤势。"));
        _modalContents.AddChild(Text("招呼、夸奖、安慰不必连点。嘴里有食物时会先咽下再回应，歇着时也可能慢一点；先看它的表情和回应。",13,"78816e"));
        _modalContents.AddChild(Text("嘴边受伤会让接食和咽下更慢；严重时说话含混，暂时唱不清小调或完整讲述。它仍能听懂，也能用声音和动作表达需要。",13,"78816e"));
        _modalContents.AddChild(Text("工具的基础力度分别记住，实际接触速度也会影响作用。更多言语、照护和装置在“展开 / 收起全部工具”里。",13,"78816e"));
        _modalContents.AddChild(Text("不用一直逗它：它会自己找食水、休息和活动。树叶和河水也能自己取用，但吃喝后可能不舒服。把干净食水放下并收手，给它一点时间。",13,"78816e"));
        _modalContents.AddChild(Text("饭饭和护理用品在这个试玩场景中不限量。每分钟自动保存；读档不计算离线时间。",13,"78816e"));
        _modalContents.AddChild(Text("右侧“同伴、家庭与照护”可以邀请两只油相处、安排孕育和幼体照护。邀请不保证接受，需要双方成熟、愿意且身体适合。出生后宝宝会出现在小院和个体名单中。冷藏只会有限地十倍延缓，取出后仍需照护。",13,"78816e"));
        _modalContents.AddChild(Text("右侧的馒馒来是当前心情，信任来自相处经历；生命和恐惧能帮助你判断身体与情绪。吃到好东西可以开心，但不会马上忘掉刚才的伤害。持续安全、吃喝与休息才有助于慢慢恢复；重伤需要及时护理。",13,"78816e"));
        _modalContents.AddChild(Text("同院出现真实危机时，顶部会显示求助。点击可找到需要帮助的油，暂停设置保持原样。落水时拿救援用品，再点油救起；正在受袭时，可从施害者或受害者的观察面板制止。救助不会直接消除伤势、饥饿或关系问题。",13,"78816e"));
        _modalContents.AddChild(Text("口渴度越高越需要水。吃喝后会逐渐产生尿意和便意，排泄物留在原地；鼠标指向后按 Del 清理。按 F3 或点顶部“调试”可查看完整状态、筛选参数和切换个体；冻结读数只固定显示，不冻结小院时间。",13,"78816e"));
        _modalContents.AddChild(Button("关闭面板",CloseModal));
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
    private void ShowRescueSupplies(string? result=null)
    {
        OpenModal("救援用品");
        int price=WaterEnvironment.RescueToolPrice;
        _modalContents.AddChild(Text($"水域救援用品 · 每份 {price} 元",20));
        _modalContents.AddChild(Text("点击落水的油，将它带到可用岸边。每次成功救助消耗1份；岸边无处落脚或目标不适用时不会消耗。救回后仍需擦干和照护。"));
        _modalContents.AddChild(Text(Sim.State.UnlimitedResources?"测试阶段：无限金钱，购买不扣钱；使用仍消耗真实库存。":$"现有金钱：{Sim.State.Money} 元"));
        _modalContents.AddChild(Text($"救援用品库存：{Sim.State.WaterRescueTools} / 99 份"));
        if(!string.IsNullOrEmpty(result))_modalContents.AddChild(Text(result,14));
        var buy=Button("购买1份",()=>
        {
            bool purchased=Sim.BuyWaterRescueTool();
            ShowRescueSupplies(purchased?"已入库1份救援用品。":Sim.State.WaterRescueTools>=99?"库存已满，先使用现有用品。":
                !Sim.State.UnlimitedResources&&Sim.State.Money<price?"金钱不足，尚未购买。":"当前不能购买，库存与金钱未改变。");
        });
        buy.Disabled=Sim.State.WaterRescueTools>=99||!Sim.State.UnlimitedResources&&Sim.State.Money<price;
        _modalContents.AddChild(buy);
        var equip=Button("拿起救援用品",()=>{CloseModal();ArmWaterRescueTool();});
        equip.Disabled=Sim.State.WaterRescueTools<=0;_modalContents.AddChild(equip);
        _modalContents.AddChild(Button("返回小院",CloseModal));
    }
}
