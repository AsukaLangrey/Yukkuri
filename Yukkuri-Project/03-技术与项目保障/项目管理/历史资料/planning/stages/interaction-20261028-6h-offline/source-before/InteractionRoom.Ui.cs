using Godot;
using System;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private Control _ui=null!,_modalShade=null!;
    private PanelContainer _leftPanel=null!,_rightPanel=null!,_bottomPanel=null!,_modalPanel=null!;
    private VBoxContainer _groupList=null!,_modalContents=null!;
    private HFlowContainer _choiceList=null!;
    private Label _title=null!,_hint=null!,_feedback=null!,_speechLabel=null!,_who=null!,_condition=null!,_memory=null!,_clockLabel=null!,_hoverLabel=null!,_forceLabel=null!;
    private Button _closeButton=null!,_pauseButton=null!,_gentleTab=null!,_roughTab=null!;
    private bool _roughTabSelected,_modalOpen,_pauseBeforeModal;
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
        _pauseButton=Button("停一停",()=>TogglePause());header.AddChild(_pauseButton);
        header.AddChild(Button("怎么玩",ShowHelp));header.AddChild(Button("存档与设置",ShowSettings));

        _leftPanel=Panel(_ui);_leftPanel.OffsetLeft=14;_leftPanel.OffsetTop=88;_leftPanel.OffsetRight=218;_leftPanel.AnchorBottom=1;_leftPanel.OffsetBottom=-218;
        var left=Column(_leftPanel,9);var tabs=Row(left,5);
        _gentleTab=Button("爱油",()=>SetToolTab(false),true);_gentleTab.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;tabs.AddChild(_gentleTab);
        _roughTab=Button("虐油",()=>SetToolTab(true));_roughTab.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;tabs.AddChild(_roughTab);
        var scroll=new ScrollContainer{SizeFlagsVertical=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};left.AddChild(scroll);
        _groupList=Column(scroll,6);_groupList.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        left.AddChild(Text("选工具后，在小油身上使用。\n右键取消，Esc 收手。",12,"7c8876"));

        _rightPanel=Panel(_ui);_rightPanel.AnchorLeft=1;_rightPanel.AnchorRight=1;_rightPanel.AnchorBottom=1;
        _rightPanel.OffsetLeft=-274;_rightPanel.OffsetRight=-14;_rightPanel.OffsetTop=88;_rightPanel.OffsetBottom=-218;
        var right=Column(_rightPanel,10);right.AddChild(Text("正在相处",12,"8b927f"));
        var selectors=Row(right,4);
        foreach(var c in Sim.State.Creatures){var id=c.Id;var b=Button(c.Name,()=>SelectOil(id));b.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;selectors.AddChild(b);}
        _who=Text("",21);right.AddChild(_who);
        _closeButton=Button("靠近看看 · 双击",ToggleClose);right.AddChild(_closeButton);
        var reportScroll=new ScrollContainer{SizeFlagsVertical=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};right.AddChild(reportScroll);
        var report=Column(reportScroll,14);report.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        _condition=Text("",14);report.AddChild(_condition);
        report.AddChild(Text("刚才发生的事",12,"8b927f"));_memory=Text("",13);report.AddChild(_memory);

        _bottomPanel=Panel(_ui);_bottomPanel.AnchorTop=1;_bottomPanel.AnchorBottom=1;_bottomPanel.AnchorRight=1;
        _bottomPanel.OffsetLeft=14;_bottomPanel.OffsetRight=-14;_bottomPanel.OffsetTop=-206;_bottomPanel.OffsetBottom=-12;
        var bottom=Column(_bottomPanel,7);var titleRow=Row(bottom,16);
        _title=Text("",19);_title.AutowrapMode=TextServer.AutowrapMode.Off;_title.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;titleRow.AddChild(_title);
        _forceLabel=Text("",13);_forceLabel.AutowrapMode=TextServer.AutowrapMode.Off;_forceLabel.CustomMinimumSize=new(90,24);titleRow.AddChild(_forceLabel);
        var forceSlider=new HSlider{MinValue=.1,MaxValue=1,Step=.05,Value=_force,CustomMinimumSize=new(150,24),FocusMode=Control.FocusModeEnum.None};
        forceSlider.ValueChanged+=v=>{_force=(float)v;RefreshRoomUi();};titleRow.AddChild(forceSlider);
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
        _modalShade=new ColorRect{Color=new Color(0.10f,.16f,.12f,.64f),Visible=false};_ui.AddChild(_modalShade);_modalShade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _modalPanel=Panel(_modalShade);_modalPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);_modalPanel.OffsetLeft=-310;_modalPanel.OffsetRight=310;_modalPanel.OffsetTop=-290;_modalPanel.OffsetBottom=290;
        var modalScroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};_modalPanel.AddChild(modalScroll);
        _modalContents=Column(modalScroll,12);_modalContents.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        SetToolTab(false);
    }

    private static void ClearChildren(Node parent){foreach(var n in parent.GetChildren()){parent.RemoveChild(n);n.QueueFree();}}
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
        CancelGesture();_group=group;SelectAction(group.Choices[0].Action);
        foreach(var child in _groupList.GetChildren())if(child is Button b)
        {bool active=b.Text==group.Name;b.AddThemeStyleboxOverride("normal",RoomStyle(active?"566e5d":"eae9d9",8,9));b.AddThemeColorOverride("font_color",new Color(active?"fff7df":"405247"));}
    }
    private void SelectAction(InteractionAction action)
    {
        CancelGesture();_action=action;_toolArmed=true;ClearChildren(_choiceList);
        foreach(var option in _group.Choices){var a=option.Action;_choiceList.AddChild(Button(option.Label,()=>SelectAction(a),a==action));}
        _title.Text=_group.Name;_hint.Text=InteractionCatalog.Choice(_action).Hint;
        _message=_group.Description;RefreshRoomUi();
    }
    private void DisarmTool()
    {
        CancelGesture();_toolArmed=false;
        _title.Text=Sim?.Selected?.Interaction.Carried==true?"托着 · 先看看它":"空手 · 先看看它";
        _hint.Text=Sim?.Selected?.Interaction.Carried==true?"托举支撑仍在。选择“放稳”，看好落点再放下。":"点击选择小油，双击近看。选择一个动作后再开始接触。";
        _message=Sim?.Selected?.Interaction.Carried==true?"仍在稳稳托着它。选择“放稳”，再点一个落点。":"已经收手。可以先观察它，或者选择下一件想做的事。";
        foreach(var child in _choiceList.GetChildren())if(child is Button b)
        {b.AddThemeStyleboxOverride("normal",RoomStyle("eae9d9",8,9));b.AddThemeColorOverride("font_color",new Color("405247"));}
        RefreshRoomUi();
    }
    private void SelectOil(string id)
    {CancelGesture();Sim.State.SelectedId=id;_message="正在看着"+Sim.Selected!.Name+"，把工具移到想接触的位置。";RefreshRoomUi();}
    private void ToggleClose()
    {
        CancelGesture();Sim.State.Mode=Sim.State.Mode==PlayMode.CloseUp?PlayMode.Management:PlayMode.CloseUp;
        _message=Sim.State.Mode==PlayMode.CloseUp?"近看时，小院的自然时间暂停。你正在做的动作仍会继续。":"回到小院，继续观察它自己会做什么。";
        RefreshRoomUi();
    }
    private void TogglePause(){CancelGesture();Sim.State.Settings.Pause=!Sim.State.Settings.Pause;RefreshRoomUi();}
    private void RefreshRoomUi()
    {
        if(_who==null||Sim==null)return;
        var c=Sim.Selected;if(c==null)return;var i=c.Interaction;
        _who.Text=c.Name+(c.IsYoung?" · 小小油":c.Body.SpeciesId=="marisa"?" · 魔理沙":" · 灵梦");
        string state=c.Condition==PhysicalCondition.Dead?"不再回应了":c.Condition==PhysicalCondition.Critical?"身体状态很差，急需护理":
            i.Carried?"正在被托着":i.Restraint switch{RestraintKind.Box=>"留在关着的箱子里",RestraintKind.Frame=>"被固定具限制着",RestraintKind.Hanging=>"留在悬挂装置上",RestraintKind.Wrap=>"被软布包裹着",RestraintKind.Hand=>"被手按住",_=>i.Supine?"身体翻了过来":WorldSimulation.CanSelfMove(c)?c.Activity:"暂时无法自己挪动"};
        string needs=c.Fullness<22?"肚子瘪瘪的，常常看着食物。":c.Fullness>90?"肚子圆圆的，暂时不想再吃。":c.Energy<30?"动作慢了，有些疲倦。":"";
        string mood=c.Fear>60?"看到手靠近，会先缩一下。":!i.HasAccessory?
            (i.Accessory.BorrowedForCare?"视线跟着借给你整理的饰品，等着戴回去。":"皱着眉，一直留意自己的饰品。"):
            i.Accessory.Integrity<90?"仍在意饰品上的破损。":c.Resentment>40?"嘴上不服气，还在留意你的动作。":c.Trust>65?"会主动抬脸看向你。":"它正在慢慢熟悉你。";
        string accessory=i.HasAccessory?(i.Accessory.Integrity<70?"饰品有破损，仍戴在头上。":"饰品好好地戴着。"):
            i.Accessory.Integrity<=0?"原来的饰品已经损坏。":i.Accessory.Place switch{AccessoryPlace.Stored=>"饰品被收起来了。",AccessoryPlace.Hung=>"饰品挂在高处。",_=>"饰品离开了头顶。"};
        _condition.Text=state+"\n\n"+mood+(needs.Length>0?"\n"+needs:"")+"\n\n"+accessory+
            (i.FootIntegrity<70?"\n"+(i.FootTreated?"伤处已经护理，仍需慢慢养好。":"移动部位不舒服，需要看看。") :"");
        _memory.Text=c.PersonalMemories.Count==0?"还没有一起经历很多事。":string.Join("\n\n",c.PersonalMemories.TakeLast(3).Reverse().Select(m=>m.Text));
        _clockLabel.Text=Sim.State.Settings.Pause?"小院暂停中":Sim.State.Mode==PlayMode.CloseUp?"近看 · 自然时间暂停":"小院时间  "+TimeSpan.FromSeconds(Sim.State.WorldSeconds).ToString(@"mm\:ss");
        _pauseButton.Text=Sim.State.Settings.Pause?"继续":"停一停";_closeButton.Text=Sim.State.Mode==PlayMode.CloseUp?"回到小院 · Tab":"靠近看看 · 双击";
        _forceLabel.Text="力度 · "+(_force<.35?"轻":_force<.7?"中":"重");
        _feedback.Text=_message;
        _speechLabel.Text=c.ReactionRemaining>0&&c.Speech.Length>0?c.Name+"：「"+c.Speech+"」":c.Name+" · "+(i.Carried?"稳稳托着":state.Length>0?state:"正安静地看着四周");
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
        _modalContents.AddChild(Text("1  选中一只油，再在左侧选爱油或虐油工具。选择动作后，移动到身体对应位置，光圈会提示碰到了哪里。"));
        _modalContents.AddChild(Text("2  点按：招呼、夸奖、翻身等。按住拖动：摸摸、蹭脸、擦擦。喂食要递到嘴边，等它接下并咽完。"));
        _modalContents.AddChild(Text("3  双击小油或按 Tab 近看；近看暂停自然时间，避免别的油在画外出事。回到小院才能看取食、跟随、休息与伤势恢复。"));
        _modalContents.AddChild(Text("4  托起后可拖动搬运，Shift＋滚轮调高度。松开鼠标仍托着；选“放稳”后点地面。右键或 Esc 收手，不会误触跌落。"));
        _modalContents.AddChild(Text("5  WASD 移动你在院子里的位置，用于实际跟随练习。滚轮调整远近。空格暂停。按 Del 收起指向的地面物品。"));
        _modalContents.AddChild(Text("6  固定、箱子、悬挂要用各自的解除动作。解除工具不会抹掉伤势；脚脚受伤后，先把食物放到能吃到的地方，再护理和休息。"));
        _modalContents.AddChild(Text("饭饭和护理用品在这个试玩场景中不限量。每分钟自动保存；读档不计算离线时间。",13,"78816e"));
        _modalContents.AddChild(Button("回到小院",CloseModal));
    }
    private void ShowSettings()
    {
        OpenModal("存档与设置");
        _modalContents.AddChild(Text("相处小院使用独立存档，保存各自的身体、饰品和经历。",14));
        var slots=Row(_modalContents);for(int n=1;n<=3;n++){int slot=n;slots.AddChild(Button("槽位 "+n,()=>{_slot=slot;ShowSettings();},n==_slot));}
        var actions=Row(_modalContents);actions.AddChild(Button("保存到槽位 "+_slot,()=>{SaveRoom(_slot);CloseModal();}));actions.AddChild(Button("读取槽位 "+_slot,()=>LoadRoom(_slot)));
        _modalContents.AddChild(Button("读取最近的自动存档",()=>LoadRoom(0)));
        _modalContents.AddChild(Button(_muted?"声音：关":"声音：开",()=>{_muted=!_muted;if(_muted)_sound.Stop();ShowSettings();}));
        _modalContents.AddChild(Button(Sim.State.Settings.LethalInjuries?"致命后果：开":"致命后果：关",()=>{Sim.State.Settings.LethalInjuries=!Sim.State.Settings.LethalInjuries;ShowSettings();}));
        _modalContents.AddChild(Button("重新开一间小院",()=>
        {
            OpenModal("重新开始这次相处？");_modalContents.AddChild(Text("当前状态先保存到自动存档，手动槽位保留。新小院的三只油将从初始状态开始。"));
            _modalContents.AddChild(Button("保存并重新开始",()=>{SaveRoom(0,false);Sim=WorldSimulation.CreateInteractionPlaytest();_recovered=false;_pauseBeforeModal=false;BuildActors();_poses.Clear();CloseModal();}));
            _modalContents.AddChild(Button("继续原来的相处",CloseModal));
        }));
        _modalContents.AddChild(Button("回到小院",CloseModal));
    }
}
