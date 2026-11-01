using Godot;
using System;
using System.Linq;
using Yukkuri.Simulation;
using Expression = Yukkuri.Simulation.Expression;

public partial class Main
{
    private Label _objectives=null!,_relationship=null!,_reaction=null!,_toolHint=null!,_contactReadout=null!,_threatInfo=null!;
    private HSlider _pressure=null!;
    private LinkButton _leaveCloseButton=null!;
    private OptionButton _toolMaterial=null!;
    private OptionButton _saveSlotOption=null!;
    private PanelContainer _settingsPanel=null!;
    private ContactAction _contactAction=ContactAction.Stroke;
    private ContactTool _contactTool=ContactTool.Hand;
    private bool _grabTool,_gestureActive,_milestoneStarted,_menuPreviousPause;
    private ContactArea _contactArea=ContactArea.Forehead;
    private double _gestureDuration;
    private string? _gestureTarget;
    private Vector2 _gestureOrigin,_gesturePoint;
    private float _gestureForce=.25f;
    private string _visibleActorKey="";
    private double _actorSyncTimer;
    private Node3D? _handVisual,_employee;
    private ConfirmationDialog _newGameConfirm=null!;
    private Control _toolPanel=null!;
    private ColorRect _modalShield=null!;
    private PanelContainer _diaryPanel=null!;
    private Label _diaryText=null!;
    private ScrollContainer _profileScroll=null!;
    private string? _profileCreatureId;
    private readonly System.Collections.Generic.List<(CheckButton button,Func<bool> read)> _settingToggles=new();
    private HSlider _musicSetting=null!,_effectsSetting=null!;
    private bool MilestoneModalOpen=>Sim.State.MilestoneScenario&&(_welcome.Visible||_settingsPanel.Visible||_diaryPanel.Visible||_helpPanel.Visible);
    // Explicit pause suspends interaction without placing a modal shield over Continue.
    private bool CloseInteractionSuspended=>Sim.State.Settings.Pause||MilestoneModalOpen;
    private void ToggleMilestonePause()
    {
        Sim.State.Settings.Pause=!Sim.State.Settings.Pause;
        if(Sim.State.Settings.Pause)SuspendCloseInteraction();
        UpdateUi();
    }
    private void SuspendCloseInteraction()
    {
        CancelGesture();_held=null;_foodInHand=false;_mapDragging=false;_force=0;
        foreach(var actor in Actors.Values)actor.Freeze=true;
    }
    private VBoxContainer ScrollPanel(Vector2 position,Vector2 size,string color)
    {
        var panel=new PanelContainer{Position=position,Size=size,CustomMinimumSize=size};panel.AddThemeStyleboxOverride("panel",PanelStyle(color));_uiRoot.AddChild(panel);
        var scroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,SizeFlagsVertical=Control.SizeFlags.ExpandFill};panel.AddChild(scroll);
        var box=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};box.AddThemeConstantOverride("separation",6);scroll.AddChild(box);return box;
    }

    private void BuildMilestoneUi()
    {
        var canvas=new CanvasLayer();AddChild(canvas);
        _uiRoot=new Control{MouseFilter=Control.MouseFilterEnum.Ignore};_uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);canvas.AddChild(_uiRoot);
        var theme=new Theme{DefaultFont=_font,DefaultFontSize=16};
        foreach(var (state,color) in new[]{("normal","e3e7d8"),("hover","d1ddc1"),("pressed","b8cfa6"),("disabled","e8e6dc")})
        {
            var style=PanelStyle(color,8);style.ContentMarginTop=6;style.ContentMarginBottom=6;style.ContentMarginLeft=10;style.ContentMarginRight=10;style.ShadowSize=0;
            theme.SetStylebox(state,"Button",style);
        }
        theme.SetColor("font_color","Button",new Color("344d40"));theme.SetColor("font_hover_color","Button",new Color("263d31"));
        theme.SetColor("font_pressed_color","Button",new Color("263d31"));theme.SetColor("font_disabled_color","Button",new Color("a3aa9a"));
        theme.SetColor("font_color","CheckButton",new Color("42584f"));theme.SetColor("font_color","Label",new Color("42584f"));
        _uiRoot.Theme=theme;
        var top=Panel(_uiRoot,new(22,18),new(1396,64),"f7f2e6f5");
        _top=Text(top,"油库里乐园",21);_top.AutowrapMode=TextServer.AutowrapMode.Off;
        var left=ScrollPanel(new(22,96),new(236,650),"f7f2e6f0");
        Text(left,"小园初晴",25);Text(left,"一座小小的乐园，从相遇开始",12,"8b937d");
        _modeLabel=Text(left,"",14,"648561");
        _threatInfo=Text(left,"",14,"9b7650");_threatInfo.CustomMinimumSize=new(0,44);
        Text(left,"今天可以做的事",14,"89917c");
        _objectives=Text(left,"",15);_objectives.CustomMinimumSize=new(0,142);
        Action(left,"home","乐园 · 回家照护",()=>Travel("home"));
        Action(left,"wild","林缘 · 观察与相遇",()=>Travel("wild"));
        Action(left,"human","Tab  主角视角",HumanMode);
        Action(left,"help","查看求助 · 前去帮助",()=>{if(Sim.FocusHelp()){RebuildRegion();Toast("已定位求助的油库里。可以保护它、制止入侵者，或先护理伤势。");}else Toast("目前没有未处理的求助。");});
        Text(left,"附近的油库里",13,"89917c");
        var scroll=new ScrollContainer{CustomMinimumSize=new(0,110),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};left.AddChild(scroll);
        _residents=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(_residents);

        var right=Panel(_uiRoot,new(1110,96),new(308,650),"f7f2e6f0");
        Text(right,"慢慢认识它",13,"89917c");_selectedName=Text(right,"",25);_detail=Text(right,"",13);
        Action(right,"diary","经历与观察记录",OpenDiary);
        _fullness=Meter(right,"饱腹","aabf81");_energy=Meter(right,"精力","d2b274");_health=Meter(right,"身体","82ad9d");
        var profileScroll=new ScrollContainer{CustomMinimumSize=new(0,240),SizeFlagsVertical=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};right.AddChild(profileScroll);
        _profileScroll=profileScroll;
        right=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};right.AddThemeConstantOverride("separation",6);profileScroll.AddChild(right);
        _relationship=Text(right,"",14,"718568");_relationship.CustomMinimumSize=new(0,36);
        _reaction=Text(right,"",16,"a5765b");_reaction.CustomMinimumSize=new(0,56);
        _observation=Text(right,"",13);_observation.CustomMinimumSize=new(0,52);
        Action(right,"observe","O  观察与记录",()=>{Sim.Observe();Sim.State.Progress.ResearchObservations++;Toast("已记录眼前的行为。判断需要更多经历来验证。");});
        Action(right,"feed","F  喂食 · 1份",MilestoneFeed);
        Action(right,"protect","保护它 · 制止威胁",()=>ShowResult(Sim.ProtectSelected()));
        Action(right,"rescue","邀请它回乐园",()=>{var result=Sim.InviteToSanctuary();ShowResult(result);if(result.Success)RebuildRegion();});
        Action(right,"admit","结束观察 · 正式接纳",()=>{if(Sim.AdmitSelected()){Sound("select");Toast("它在这里有自己的位置了。");}});
        Action(right,"close","E  靠近与互动",CloseUp);

        var eventBox=Panel(_uiRoot,new(278,538),new(812,114),"344c42e5");
        Text(eventBox,"乐园里的小事",13,"c9d5b6");_events=Text(eventBox,"",14,"f6edda");_events.CustomMinimumSize=new(0,48);
        var tools=Panel(_uiRoot,new(278,666),new(812,176),"f7f2e6f5");_toolPanel=tools.GetParent<Control>();
        var heading=new HBoxContainer();heading.AddThemeConstantOverride("separation",12);tools.AddChild(heading);
        _toolHint=new Label{Text="选择一种互动，靠近后按住目标。",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};heading.AddChild(_toolHint);
        _contactReadout=new Label{Text="力度 25%"};_contactReadout.AddThemeColorOverride("font_color",new Color("a3784d"));heading.AddChild(_contactReadout);
        var leaveClose=new LinkButton{Text="返回场景 · E",FocusMode=Control.FocusModeEnum.None,Visible=false};
        leaveClose.AddThemeFontSizeOverride("font_size",14);leaveClose.AddThemeColorOverride("font_color",new Color("42584f"));
        leaveClose.Pressed+=ExitCloseUp;heading.AddChild(leaveClose);
        _leaveCloseButton=leaveClose;
        var grid=new GridContainer{Columns=4};grid.AddThemeConstantOverride("h_separation",6);grid.AddThemeConstantOverride("v_separation",5);tools.AddChild(grid);
        var toolNames=new[]{("stroke","抚摸",ContactAction.Stroke),("hug","抱抱",ContactAction.Hug),("nuzzle","蹭蹭",ContactAction.Nuzzle),("train","一起练习",ContactAction.Train),
            ("grab","拿起 / 放下",ContactAction.Stroke),("warn","警告",ContactAction.Warn),("discipline","惩戒",ContactAction.Discipline),("treat","护理 · 1包",ContactAction.Treat)};
        foreach(var (id,label,action) in toolNames)
        {
            var b=new Button{Text=label,ToggleMode=true,CustomMinimumSize=new(187,32),FocusMode=Control.FocusModeEnum.None};
            b.Pressed+=()=>ChooseContact(action,id=="grab");grid.AddChild(b);Buttons[id]=b;
        }
        var forceRow=new HBoxContainer();forceRow.AddThemeConstantOverride("separation",10);tools.AddChild(forceRow);
        forceRow.AddChild(new Label{Text="轻柔"});_pressure=new HSlider{MinValue=.05,MaxValue=1,Step=.05,Value=.25,CustomMinimumSize=new(220,26),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,FocusMode=Control.FocusModeEnum.None};forceRow.AddChild(_pressure);
        forceRow.AddChild(new Label{Text="用力"});_toolMaterial=new OptionButton{CustomMinimumSize=new(150,28)};
        _toolMaterial.AddItem("手掌");_toolMaterial.AddItem("软手套");_toolMaterial.AddItem("训练教具");
        _toolMaterial.ItemSelected+=index=>_contactTool=(ContactTool)index;forceRow.AddChild(_toolMaterial);
        _pressure.ValueChanged+=value=>_gestureForce=(float)value;

        var footer=Panel(_uiRoot,new(22,858),new(1396,34),"f7f2e6f0");
        var footerStyle=(StyleBoxFlat)footer.GetParent<PanelContainer>().GetThemeStylebox("panel");footerStyle.ContentMarginTop=4;footerStyle.ContentMarginBottom=4;
        _hint=Text(footer,"",13);_hint.AutowrapMode=TextServer.AutowrapMode.Off;
        var controls=Panel(_uiRoot,new(22,758),new(236,84),"f7f2e6f0");
        var controlsRow=new HBoxContainer();controls.AddChild(controlsRow);
        AddCompact(controlsRow,"pause","暂停",ToggleMilestonePause);
        AddCompact(controlsRow,"settings","设置",OpenSettings);
        AddCompact(controlsRow,"guide","菜单",OpenMilestoneMenu);
        AddCompact(controlsRow,"manual","帮助",OpenHelp);
        var saveRow=new HBoxContainer();controls.AddChild(saveRow);
        AddCompact(saveRow,"save","F5 保存",()=>Save(_slot));
        AddCompact(saveRow,"load","F9 读取",()=>{if(Load(_slot))CloseMilestoneMenu();});
        var slot=new OptionButton{CustomMinimumSize=new(52,28)};for(int i=1;i<=3;i++)slot.AddItem(i.ToString());slot.ItemSelected+=i=>_slot=(int)i+1;saveRow.AddChild(slot);_saveSlotOption=slot;
        var supplies=Panel(_uiRoot,new(1110,758),new(308,84),"f7f2e6f0");
        var supplyRow=new HBoxContainer();supplies.AddChild(supplyRow);
        AddCompact(supplyRow,"supplies","食物 +20 · 10",()=>Toast(Sim.BuyFood()?"食物已入库。":"资金不足。"));
        AddCompact(supplyRow,"medicine","护理 +3 · 15",()=>Toast(Sim.BuyMedicine()?"护理包已入库。":"资金不足。"));
        var supplies2=new HBoxContainer();supplies.AddChild(supplies2);
        AddCompact(supplies2,"refill","补充食盆",()=>{Toast(Sim.Refill()?"已补充食物。":"没有食盆或食物不足。");RebuildRegion();});
        AddCompact(supplies2,"commission","提交养护记录",()=>ShowResult(Sim.ClaimCareContract()));

        _modalShield=new ColorRect{Color=new Color(0.12f,.18f,.14f,.22f),MouseFilter=Control.MouseFilterEnum.Stop};
        _uiRoot.AddChild(_modalShield);_modalShield.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        BuildMilestoneMenu();BuildMilestoneSettings();BuildDiary();BuildHelp();
        _newGameConfirm=new ConfirmationDialog{Title="开始新的小园生活",DialogText="当前未保存的进度会被替换。\n已保存在槽位里的存档不会删除。",OkButtonText="新开局",CancelButtonText="返回",MinSize=new(420,150)};
        _uiRoot.AddChild(_newGameConfirm);_newGameConfirm.Confirmed+=StartNewMilestone;
        Sim.State.Settings.Pause=true;
    }
    private Button AddCompact(HBoxContainer row,string id,string label,Action action)
    {
        var b=new Button{Text=label,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,CustomMinimumSize=new(0,28),FocusMode=Control.FocusModeEnum.None};
        b.AddThemeFontSizeOverride("font_size",13);row.AddChild(b);b.Pressed+=action;Buttons[id]=b;return b;
    }
    private void BuildMilestoneMenu()
    {
        _welcome=new PanelContainer{Position=new(406,144),CustomMinimumSize=new(628,584)};
        _welcome.AddThemeStyleboxOverride("panel",PanelStyle("faf5e9"));_uiRoot.AddChild(_welcome);
        var box=new VBoxContainer();box.AddThemeConstantOverride("separation",9);_welcome.AddChild(box);
        var art=new TextureRect{Texture=GD.Load<Texture2D>("res://Assets/Art/sanctuary-morning.png"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered,CustomMinimumSize=new(592,210)};box.AddChild(art);
        Text(box,"油库里乐园",32);Text(box,"小园初晴",18,"8d9c77");
        Text(box,"和油库里慢慢熟悉，在林缘寻找新的伙伴。\n照顾它们、一起练习，也在危险来临时保护这个家。",16);
        Action(box,"start","开始小园生活",()=>{if(_milestoneStarted)CloseMilestoneMenu();else StartNewMilestone();});
        Action(box,"continue","读取槽位 1",()=>{_slot=1;if(Load(1)){_milestoneStarted=true;CloseMilestoneMenu();}});
        Action(box,"continue-auto","读取自动存档",()=>{if(Load(0)){_milestoneStarted=true;CloseMilestoneMenu();}});
        Action(box,"new","重新开始",()=>_newGameConfirm.PopupCentered());
        Action(box,"menu-help","操作与玩法",OpenHelp);
        Text(box,"点击油库里选择 · E 靠近 · 按住目标互动\nTab 切换主角 · WASD 移动 · 空格暂停 · F5 保存",13,"84917b");
    }
    private void BuildMilestoneSettings()
    {
        _settingsPanel=new PanelContainer{Position=new(420,170),CustomMinimumSize=new(600,530),Visible=false};_settingsPanel.AddThemeStyleboxOverride("panel",PanelStyle("faf5e9"));_uiRoot.AddChild(_settingsPanel);
        var box=new VBoxContainer();box.AddThemeConstantOverride("separation",11);_settingsPanel.AddChild(box);
        Text(box,"按自己的节奏生活",27);
        Text(box,"下面的开关影响之后的规则，不会倒改已经发生的结果。",14,"7e8a75");
        void Toggle(string label,Func<bool> read,Action<bool> change)
        {var b=new CheckButton{Text=label,ButtonPressed=read()};b.Toggled+=v=>change(v);box.AddChild(b);_settingToggles.Add((b,read));}
        Toggle("允许野生油库里入侵",()=>Sim.State.Settings.Invasions,v=>Sim.State.Settings.Invasions=v);
        Toggle("跌落等意外可造成伤害",()=>Sim.State.Settings.Accidents,v=>Sim.State.Settings.Accidents=v);
        Toggle("严重伤势可能导致死亡",()=>Sim.State.Settings.LethalInjuries,v=>Sim.State.Settings.LethalInjuries=v);
        Toggle("照护工具保护：避免抚摸、拥抱时误伤",()=>Sim.State.Settings.ProtectCareTools,v=>Sim.State.Settings.ProtectCareTools=v);
        _sandbox=new CheckButton{Text="自由体验：资源不限",ButtonPressed=Sim.State.UnlimitedResources};_sandbox.Toggled+=v=>Sim.State.UnlimitedResources=v;box.AddChild(_sandbox);
        Text(box,"音乐音量",14);
        var music=new HSlider{MinValue=0,MaxValue=1,Step=.05,Value=Sim.State.Settings.MusicVolume,CustomMinimumSize=new(0,30)};
        music.ValueChanged+=v=>{Sim.State.Settings.MusicVolume=v;_music.VolumeDb=v<=0?-80:Mathf.LinearToDb((float)v)-12;};box.AddChild(music);
        _musicSetting=music;
        Text(box,"音效音量",14);
        var effects=new HSlider{MinValue=0,MaxValue=1,Step=.05,Value=Sim.State.Settings.EffectsVolume,CustomMinimumSize=new(0,30)};
        effects.ValueChanged+=v=>{Sim.State.Settings.EffectsVolume=v;_sfx.VolumeDb=v<=0?-80:Mathf.LinearToDb((float)v)-10;};box.AddChild(effects);
        _effectsSetting=effects;
        Action(box,"close-settings","回到乐园",CloseSettings);
    }
    private void StartNewMilestone()
    {
        Sim=WorldSimulation.CreateMilestone();_config=Sim.Config;_recovered=false;_autosaveTimer=0;_milestoneStarted=true;
        _gestureActive=false;_held=null;_foodInHand=false;_residentKey="";_visibleActorKey="";
        _contactAction=ContactAction.Stroke;_contactTool=ContactTool.Hand;_grabTool=false;
        _pressure.Value=.25;_toolMaterial.Select(0);
        RebuildRegion();CloseMilestoneMenu();Sim.State.Settings.Pause=false;Sim.State.Progress.IntroDismissed=true;UpdateUi();
        Toast("先选中小铃或豆豆，喂一点食物，再试试轻柔地抚摸。");
    }
    private void OpenMilestoneMenu()
    {
        if(Sim.State.Mode==PlayMode.CloseUp)ExitCloseUp();
        _menuPreviousPause=Sim.State.Settings.Pause;Sim.State.Settings.Pause=true;_welcome.Visible=true;
        _modalShield.Visible=true;
        Buttons["start"].Text=_milestoneStarted?"回到乐园":"开始小园生活";
    }
    private void CloseMilestoneMenu(){_welcome.Visible=false;_settingsPanel.Visible=false;_modalShield.Visible=false;Sim.State.Settings.Pause=_menuPreviousPause;_milestoneStarted=true;}
    private void CloseSettings(){_settingsPanel.Visible=false;_modalShield.Visible=false;Sim.State.Settings.Pause=_menuPreviousPause;}
    private void OpenSettings()
    {
        SuspendCloseInteraction();
        _menuPreviousPause=Sim.State.Settings.Pause;Sim.State.Settings.Pause=true;_settingsPanel.Visible=true;
        foreach(var entry in _settingToggles)entry.button.SetPressedNoSignal(entry.read());
        _musicSetting.SetValueNoSignal(Sim.State.Settings.MusicVolume);_effectsSetting.SetValueNoSignal(Sim.State.Settings.EffectsVolume);
        UpdateUi();
    }
    private void BuildDiary()
    {
        _diaryPanel=new PanelContainer{Position=new(340,110),CustomMinimumSize=new(760,660),Visible=false};
        _diaryPanel.AddThemeStyleboxOverride("panel",PanelStyle("faf5e9"));_uiRoot.AddChild(_diaryPanel);
        var box=new VBoxContainer();box.AddThemeConstantOverride("separation",12);_diaryPanel.AddChild(box);
        Text(box,"相处的记录",27);
        Text(box,"只记录已经获知的事情。性格判断仍需要观察，经历不会随切换地区消失。",14,"7e8a75");
        var scroll=new ScrollContainer{CustomMinimumSize=new(720,495),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};box.AddChild(scroll);
        var entries=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(entries);
        _diaryText=Text(entries,"",16);_diaryText.CustomMinimumSize=new(690,0);
        Action(box,"close-diary","收好记录",CloseDiary);
    }
    private void OpenDiary()
    {
        var c=Sim.Selected;if(c==null)return;
        _menuPreviousPause=Sim.State.Settings.Pause;Sim.State.Settings.Pause=true;SuspendCloseInteraction();
        var known=Sim.State.Incidents.Where(e=>Sim.State.PlayerKnownIncidents.Contains(e.Id)&&(e.TargetId==c.Id||e.ActorId==c.Id)).TakeLast(14);
        _diaryText.Text=$"{c.Name} · {c.Activity}\n\n观察笔记\n"+string.Join("\n",c.Observation.Notes.TakeLast(6))+
            "\n\n已知经历\n"+string.Join("\n",known.Select(e=>$"[{WorldSimulation.FormatClock(e.At)}] {e.Description}"))+
            "\n\n眼前的反应\n"+c.LastReaction+
            (c.Condition==PhysicalCondition.Critical?$"\n需要立即护理：剩余约 {Math.Ceiling(c.CriticalRemaining)} 秒。":"")+
            "\n\n这些是当前已获得的线索；未知的事情不会提前写进记录。";
        _diaryPanel.Visible=true;UpdateUi();
    }
    private void CloseDiary(){_diaryPanel.Visible=false;Sim.State.Settings.Pause=_menuPreviousPause;UpdateUi();}
    private void ChooseContact(ContactAction action,bool grab=false)
    {
        _contactAction=action;_grabTool=grab;_foodInHand=false;_gestureActive=false;_held=null;
        if(Sim.State.Mode!=PlayMode.CloseUp)CloseUp();
        Toast(grab?"按住油库里拿起，松开放下。滚轮调整高度，注意跌落。":$"选择了{WorldSimulation.ActionLabel(action)}。在头顶、脸颊或身体按住，松开完成。");
    }
    private void MilestoneFeed()
    {
        if(Sim.State.Settings.Pause){Toast("先继续时间，再喂食。");return;}
        if(Sim.Selected==null||Sim.Selected.Condition is PhysicalCondition.Critical or PhysicalCondition.Dead)
        {Toast("当前无法喂食。重伤需先用护理包稳定，已经离去的油库里无法回应。");return;}
        if(Sim.State.Mode==PlayMode.CloseUp){_gestureActive=false;_foodInHand=!_foodInHand;Toast("把食物移到嘴部附近，点击喂食。");}
        else if(Sim.Selected!=null&&Sim.Feed(Sim.Selected.Id)){Sound("feed");Toast("它记住了这次照护。");}
        else Toast(LegacyFeedFailure());
    }
    private static string LegacyVoiceCue(Creature? speaker,InteractionResult result,ContactAction action)
    {
        if(speaker==null||speaker.Condition==PhysicalCondition.Dead||speaker.Family.ColdStorage.Stored)return "";
        if(speaker.Condition==PhysicalCondition.Critical)return "coo-help";
        if(speaker.Pain>=40||speaker.Expression==Expression.Hurt||result.Damage>0)return "coo-hurt";
        if(speaker.Expression==Expression.Calling)return "coo-help";
        if(!MouthFunction.CanSpeakClearly(speaker))return "coo-refuse";
        if(!result.Success)return "coo-refuse";
        return action switch
        {ContactAction.Stroke=>"coo-stroke",ContactAction.Hug=>"coo-hug",ContactAction.Nuzzle=>"coo-nuzzle",_=>"feed"};
    }
    private void ShowResult(InteractionResult result,Creature? speaker=null)
    {
        Toast(result.Message);
        string cue=LegacyVoiceCue(speaker??Sim.Selected,result,_contactAction);
        if(cue.Length>0)Sound(cue);UpdateUi();
    }
    private void UpdateMilestoneUi()
    {
        if(_top==null)return;var s=Sim.State;var c=Sim.Selected;bool close=s.Mode==PlayMode.CloseUp;
        if(_profileCreatureId!=c?.Id){_profileCreatureId=c?.Id;_profileScroll.ScrollVertical=0;}
        if(_modalShield!=null)_modalShield.Visible=MilestoneModalOpen;
        _top.Text=$"油库里乐园    /    {Sim.ActiveRegion.Name}                    资金 {s.Money}     食物 {s.Food}     护理包 {s.Medicine}                    {WorldSimulation.FormatClock(s.WorldSeconds)}";
        _modeLabel.Text=s.Settings.Pause?"暂停中":close?"互动特写 · 世界暂停":s.Mode==PlayMode.Human?"主角视角 · WASD 移动":"管理视角 · 点击选择";
        _threatInfo.Text=s.Threat.Phase switch{ThreatPhase.Approaching=>"林缘有动静，留意乐园入口。",ThreatPhase.Intruding=>s.Threat.HelpRequested?"油库里正在求助！":"油库里正在守护乐园。",ThreatPhase.Retreating=>"入侵者正在离开。",_=>"慢慢照顾，慢慢认识。"};
        string Task(string key,string text)=>$"{(s.Progress.Completed.Contains(key)?"✓":"○")}  {text}";
        _objectives.Text=string.Join("\n",new[]{Task("feed","照顾一顿饭"),Task("affection","建立温和的接触"),Task("train","一起练习三次"),Task("admit","接纳林缘的新伙伴"),Task("defense","共同守护乐园"),Task("contract","整理一次养护记录")});
        _selectedName.Text=c?.Name??"选一只油库里";
        string condition=c?.Condition switch{PhysicalCondition.Critical=>"重伤待救",PhysicalCondition.Recovering=>"休养恢复",PhysicalCondition.Dead=>"已经离去",_=>c?.Quarantine==true?"观察区":"未评级"};
        _detail.Text=c==null?"点击场景，或从左侧名单选择。":$"{(c.Body.SpeciesId=="marisa"?"魔理沙":"灵梦")} · {(c.IsYoung?"幼年":c.IsElder?"年长":"成年")} · {condition}\n{c.Activity}";
        _fullness.Value=c?.Fullness??0;_energy.Value=c?.Energy??0;_health.Value=c?.Health??0;
        _relationship.Text=c==null?"":c.Condition switch
        {
            PhysicalCondition.Dead=>"它已经无法回应。相处的经历仍保存在记录中。",
            PhysicalCondition.Critical=>"伤势危急，需要尽快护理；当前无法参与日常互动。",
            PhysicalCondition.Recovering=>$"伤势已稳定；按当前营养，还需休养约 {Math.Ceiling(c.RecoveryRemaining/(c.Fullness>35?1:.3))} 秒。",
            _=>c.Familiarity==0?"还不熟悉，先看看它的反应。":c.Fear>55?"它显得不安，动作也很谨慎。":c.Trust>50?"它愿意主动靠近你。":c.Trust>25?"它正在慢慢熟悉你。":"它仍在观察你的举动。"
        };
        string speech=c==null?"":c.Condition==PhysicalCondition.Critical?c.Speech:MouthFunction.FormatSpeech(c,c.Speech);
        _reaction.Text=c==null?"":speech.Length>0?$"「{speech}」":c.LastReaction;
        _observation.Text=c==null?"":$"{c.Observation.Hypothesis}\n{c.Observation.Notes.LastOrDefault()??"尚未记录它的行为。"}";
        Buttons["close"].Text=close?"E  回到场景":"E  靠近与互动";Buttons["feed"].Text=close?(_foodInHand?"F  放回食物":"F  拿取食物 · 1份"):"F  喂食 · 1份";
        Buttons["rescue"].Disabled=close||c==null||c.RegionId=="home";Buttons["admit"].Disabled=close||c?.Quarantine!=true;
        Buttons["protect"].Disabled=c==null||c.Condition==PhysicalCondition.Dead;Buttons["help"].Disabled=s.HelpTargetId==null;
        Buttons["feed"].Disabled=c==null||c.Condition is PhysicalCondition.Critical or PhysicalCondition.Dead;
        foreach(string id in new[]{"home","wild","save","load","refill"})Buttons[id].Disabled=close;
        Buttons["pause"].Text=s.Settings.Pause?"继续":"暂停";
        if(_saveSlotOption.Selected!=_slot-1)_saveSlotOption.Select(_slot-1);
        _toolHint.Text=close?(_grabTool?"拿起与放下 · 注意高度":$"{WorldSimulation.ActionLabel(_contactAction)} · 按住目标，松开完成"):"选择一种互动，靠近后按住目标。";
        _leaveCloseButton.Visible=close;
        string activeTool=_grabTool?"grab":_contactAction switch
        {ContactAction.Stroke=>"stroke",ContactAction.Hug=>"hug",ContactAction.Nuzzle=>"nuzzle",ContactAction.Train=>"train",ContactAction.Warn=>"warn",ContactAction.Discipline=>"discipline",_=>"treat"};
        foreach(string id in new[]{"stroke","hug","nuzzle","train","grab","warn","discipline","treat"})
            Buttons[id].SetPressedNoSignal(id==activeTool);
        _contactReadout.Text=_gestureActive?$"{AreaLabel(_contactArea)} · {_gestureDuration:F1}秒 · {_gestureForce:P0}":$"力度 {_pressure.Value:P0}";
        _events.Text=string.Join("\n",s.Journal.TakeLast(3));
        _hint.Text=_toastTime>0?_toast:close?"按住目标互动 · 滚轮调整拿取高度 · E / Esc 返回":"滚轮缩放 · 中键拖动视野 · E 靠近 · Tab 主角 · WASD 移动 · 空格暂停 · F5 保存";
        _sandbox.SetPressedNoSignal(s.UnlimitedResources);
        string key=string.Join(',',s.Creatures.Where(x=>x.RegionId==s.ActiveRegionId).Select(x=>x.Id));
        if(key!=_residentKey)
        {
            _residentKey=key;
            var visible=s.Creatures.Where(x=>x.RegionId==s.ActiveRegionId).ToArray();
            var ids=visible.Select(x=>x.Id).ToHashSet();
            var existing=_residents.GetChildren().OfType<Button>().ToDictionary(b=>b.GetMeta("creature_id").AsString());
            foreach(var entry in existing.Where(entry=>!ids.Contains(entry.Key)))
            {_residents.RemoveChild(entry.Value);entry.Value.QueueFree();}
            int order=0;
            foreach(var oil in visible)
            {
                if(!existing.TryGetValue(oil.Id,out var b))
                {
                    string creatureId=oil.Id;
                    b=new Button{ToggleMode=true,CustomMinimumSize=new(0,28),FocusMode=Control.FocusModeEnum.None};
                    b.AddThemeFontSizeOverride("font_size",14);b.SetMeta("creature_id",creatureId);
                    b.Pressed+=()=>{if(Sim.State.Mode!=PlayMode.CloseUp){Sim.Select(creatureId);UpdateUi();}};_residents.AddChild(b);
                }
                b.Text=oil.Name;_residents.MoveChild(b,order++);
            }
        }
        foreach(var child in _residents.GetChildren())if(child is Button resident)
        {
            bool selected=resident.GetMeta("creature_id").AsString()==s.SelectedId;
            resident.SetPressedNoSignal(selected);resident.Disabled=close&&!selected;
        }
    }
    private static string AreaLabel(ContactArea area)=>area switch{ContactArea.Forehead=>"头顶",ContactArea.Cheek=>"脸颊",_=>"身体"};
}
