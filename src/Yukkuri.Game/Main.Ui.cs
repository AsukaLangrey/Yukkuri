using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Yukkuri.Simulation;

public partial class Main
{
    internal readonly Dictionary<string,Button> Buttons=new();
    private Label _top=null!,_regionInfo=null!,_selectedName=null!,_detail=null!,_observation=null!,_events=null!,_hint=null!,_modeLabel=null!;
    private ProgressBar _fullness=null!,_energy=null!,_health=null!;
    private PanelContainer _welcome=null!;
    private VBoxContainer _residents=null!;
    private string _residentKey="";
    private Control _uiRoot=null!;
    private CheckButton _sandbox=null!;
    private static StyleBoxFlat PanelStyle(string color,int radius=14)
    {
        var s=new StyleBoxFlat{BgColor=new Color(color),CornerRadiusTopLeft=radius,CornerRadiusTopRight=radius,CornerRadiusBottomLeft=radius,CornerRadiusBottomRight=radius,
            ContentMarginLeft=18,ContentMarginRight=18,ContentMarginTop=16,ContentMarginBottom=16,ShadowColor=new Color(0,0,0,.12f),ShadowSize=6};return s;
    }
    private Label Text(VBoxContainer box,string text,int size=17,string color="42584f")
    {
        var l=new Label{Text=text,AutowrapMode=TextServer.AutowrapMode.WordSmart};l.AddThemeFontSizeOverride("font_size",size);l.AddThemeColorOverride("font_color",new Color(color));box.AddChild(l);return l;
    }
    private VBoxContainer Panel(Control parent,Vector2 pos,Vector2 size,string color="f7f2e4ed")
    {
        var panel=new PanelContainer{Position=pos,Size=size,CustomMinimumSize=size};panel.AddThemeStyleboxOverride("panel",PanelStyle(color));parent.AddChild(panel);
        var box=new VBoxContainer();box.AddThemeConstantOverride("separation",6);panel.AddChild(box);return box;
    }
    private Button Action(VBoxContainer parent,string id,string text,Action action)
    {
        var b=new Button{Text=text,CustomMinimumSize=new Vector2(0,38),Alignment=HorizontalAlignment.Left,FocusMode=Control.FocusModeEnum.None};
        b.Pressed+=()=>{action();UpdateUi();};parent.AddChild(b);Buttons[id]=b;return b;
    }
    private ProgressBar Meter(VBoxContainer parent,string text,string color)
    {
        Text(parent,text,14,"6e7d70");
        var p=new ProgressBar{CustomMinimumSize=new(0,10),ShowPercentage=false};
        foreach(var entry in new[]{("background","dedfce"),("fill",color)})
        {var s=PanelStyle(entry.Item2,5);s.ContentMarginLeft=0;s.ContentMarginRight=0;s.ContentMarginTop=0;s.ContentMarginBottom=0;s.ShadowSize=0;p.AddThemeStyleboxOverride(entry.Item1,s);}
        parent.AddChild(p);return p;
    }
    private void BuildUi()
    {
        if(Sim.State.MilestoneScenario){BuildMilestoneUi();return;}
        var canvas=new CanvasLayer();AddChild(canvas);
        _uiRoot=new Control{MouseFilter=Control.MouseFilterEnum.Ignore};_uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);canvas.AddChild(_uiRoot);
        var theme=new Theme{DefaultFont=_font,DefaultFontSize=17};
        theme.SetStylebox("normal","Button",PanelStyle("e2e7d7",8));theme.SetStylebox("hover","Button",PanelStyle("cbd9bb",8));theme.SetStylebox("pressed","Button",PanelStyle("b8caa6",8));
        theme.SetStylebox("disabled","Button",PanelStyle("e8e6da",8));theme.SetColor("font_color","Button",new Color("334e40"));theme.SetColor("font_hover_color","Button",new Color("213e31"));theme.SetColor("font_pressed_color","Button",new Color("213e31"));theme.SetColor("font_disabled_color","Button",new Color("9b9e91"));
        foreach(string state in new[]{"normal","hover","pressed","disabled"})
        {var s=(StyleBoxFlat)theme.GetStylebox(state,"Button");s.ContentMarginTop=6;s.ContentMarginBottom=6;s.ContentMarginLeft=12;s.ContentMarginRight=12;s.ShadowSize=0;}
        _uiRoot.Theme=theme;
        var top=Panel(_uiRoot,new(24,20),new(1392,75));
        _top=Text(top,"油库里乐园",24);_top.AutowrapMode=TextServer.AutowrapMode.Off;
        var left=Panel(_uiRoot,new(24,112),new(250,652));
        Text(left,"慢慢来，慢慢生活。",20);_modeLabel=Text(left,"管理视角",14,"79926a");
        _regionInfo=Text(left,"",15);
        Action(left,"home","乐园 · 返回照护",()=>Travel("home"));
        Action(left,"wild","林缘 · 外出观察",()=>Travel("wild"));
        Action(left,"human","Tab  进入人类视角",HumanMode);
        Text(left,"生活设施",14,"8b967e");
        Action(left,"feeder","1  放置食盆 · 25",()=>{_buildKind=FacilityKind.Feeder;Toast("在地面点击放置，右键取消。");});
        Action(left,"bed","2  放置睡床 · 20",()=>{_buildKind=FacilityKind.Bed;Toast("在地面点击放置，右键取消。");});
        Action(left,"supplies","补给 +20 食物 · 10",()=>Toast(Sim.BuyFood()?"食物已入库。":"资金不足。"));
        Action(left,"refill","给食盆补充 10 份",()=>{Toast(Sim.Refill()?"已补充食物。":"没有食盆或库存不足。");RebuildRegion();});
        Action(left,"staff","员工：自动照护 开",()=>Sim.ActiveRegion.StaffEnabled=!Sim.ActiveRegion.StaffEnabled);
        Action(left,"music","音乐：开",()=>{_muted=!_muted;_music.VolumeDb=_muted?-80:-15;});
        Action(left,"guide","玩法提示 / 素材预览",()=>_welcome.Visible=true);

        var right=Panel(_uiRoot,new(1090,112),new(326,652));
        Text(right,"个体档案",14,"8b967e");_selectedName=Text(right,"",25);_detail=Text(right,"",14);
        _fullness=Meter(right,"饱腹","b9c98f");_energy=Meter(right,"精力","ddb87c");_health=Meter(right,"身体","91bca8");
        _observation=Text(right,"",15);_observation.CustomMinimumSize=new(0,72);
        Action(right,"observe","O  观察并记入档案",()=>{Sim.Observe();Toast("行为已记录，判断会随证据修正。");});
        Action(right,"close","E  进入互动特写",CloseUp);
        Action(right,"feed","F  喂食 · 1份",()=>
        {
            if(Sim.State.Settings.Pause){Toast("先继续时间，再喂食。");return;}
            if(Sim.State.Mode==PlayMode.CloseUp){_foodInHand=!_foodInHand;Toast("移动食物到嘴部附近，点击喂食。");}
            else if(Sim.Selected!=null&&Sim.Feed(Sim.Selected.Id))Sound("feed");else Toast(LegacyFeedFailure());
        });
        Action(right,"rescue","带回乐园观察区",()=>{if(Sim.Rescue()){RebuildRegion();Toast("已安置；返回乐园可继续观察。");}else Toast("在野地选中个体后使用。");});
        Action(right,"admit","结束隔离 · 玩家接纳",()=>{if(Sim.Selected?.Quarantine==true){Sim.Selected.Quarantine=false;Sim.Log("玩家决定接纳"+Sim.Selected.Name);Toast("已接纳，但不会自动获得牌照。");}});
        Action(right,"commission","提交照护记录 · 奖励 40",()=>Toast(Sim.CompleteCareRecord()?"照护记录完成，收到示例委托奖励。":"需已接纳、饱腹≥70、身体≥80、观察2次与喂食2次；每只仅一次。"));

        var bottom=Panel(_uiRoot,new(24,782),new(1392,98));
        var row=new HBoxContainer();row.AddThemeConstantOverride("separation",12);bottom.AddChild(row);
        var slot=new OptionButton{CustomMinimumSize=new(130,36)};for(int i=1;i<=3;i++)slot.AddItem("存档槽位 "+i,i);slot.ItemSelected+=i=>_slot=(int)i+1;row.AddChild(slot);
        foreach(var item in new[]{("save","F5 保存",(Action)(()=>Save(_slot))),("load","F9 读取",(Action)(()=>Load(_slot))),("backup","读自动档",(Action)(()=>Load(0)))})
        {var b=new Button{Text=item.Item2,CustomMinimumSize=new(120,36),FocusMode=Control.FocusModeEnum.None};b.Pressed+=item.Item3;row.AddChild(b);Buttons[item.Item1]=b;}
        _hint=new Label{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,AutowrapMode=TextServer.AutowrapMode.WordSmart};_hint.AddThemeColorOverride("font_color",new Color("526c58"));row.AddChild(_hint);
        Text(bottom,"原型框架 · 设定未决项尚未实现 · 本地单人 · 存档不计算离线变化",12,"87917e");

        var eventBox=Panel(_uiRoot,new(300,635),new(358,128),"344a42df");
        Text(eventBox,"乐园日志",14,"e4d6b4");_events=Text(eventBox,"",13,"f6edd8");
        var residentBox=Panel(_uiRoot,new(840,635),new(225,128),"f7f2e4e8");
        Text(residentBox,"附近的居民",14,"71836b");
        var scroll=new ScrollContainer{CustomMinimumSize=new(0,64),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};residentBox.AddChild(scroll);
        _residents=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(_residents);

        _welcome=new PanelContainer{Position=new(334,140),CustomMinimumSize=new(712,490),Visible=false};_welcome.AddThemeStyleboxOverride("panel",PanelStyle("faf5e9"));_uiRoot.AddChild(_welcome);
        var welcomeBox=new VBoxContainer();welcomeBox.AddThemeConstantOverride("separation",10);_welcome.AddChild(welcomeBox);
        var art=new TextureRect{Texture=GD.Load<Texture2D>("res://Assets/Art/sanctuary-morning.png"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered,CustomMinimumSize=new(676,240)};welcomeBox.AddChild(art);
        Text(welcomeBox,"一座小小的乐园，从照护开始",25);
        Text(welcomeBox,"点击居民查看档案；Tab 切换人类，WASD 移动。\nE 进入特写：鼠标按住拿起，松开放下，滚轮调高度。\nF 拿取食物，在嘴部点击。建造后给食盆补充库存。\n野地可观察并带回个体；F5 / F9 保存和读取。",17);
        _sandbox=new CheckButton{Text="自由沙盒：资源不限",ButtonPressed=Sim.State.UnlimitedResources};_sandbox.Toggled+=enabled=>{Sim.State.UnlimitedResources=enabled;Toast(enabled?"已启用无限资源。":"已恢复有限资源。");};welcomeBox.AddChild(_sandbox);
        Action(welcomeBox,"dismiss","回到乐园",()=>_welcome.Visible=false);
        foreach(var icon in new[]{("home","home"),("observe","observe"),("close","hand"),("feed","food")})
        {Buttons[icon.Item1].Icon=GD.Load<Texture2D>("res://Assets/Icons/"+icon.Item2+".svg");Buttons[icon.Item1].AddThemeConstantOverride("h_separation",8);}
    }
    private void UpdateUi()
    {
        if(Sim.State.MilestoneScenario){UpdateMilestoneUi();return;}
        if(_top==null)return;
        var s=Sim.State;var c=Sim.Selected;bool close=s.Mode==PlayMode.CloseUp;
        var view=c==null?null:Sim.View(c);
        _sandbox.SetPressedNoSignal(s.UnlimitedResources);
        _top.Text=$"油库里乐园    /    {Sim.ActiveRegion.Name}                          资金 {s.Money}     食物 {s.Food}     第 {(int)(s.WorldSeconds/600)+1} 天 · {WorldSimulation.FormatClock(s.WorldSeconds)}";
        _modeLabel.Text=s.Mode switch{PlayMode.Human=>"人类视角 · WASD 移动",PlayMode.CloseUp=>"互动特写 · 被动时间暂停",_=>"管理视角 · 点击选择"};
        _regionInfo.Text=$"本区 {Actors.Count} 只 · 观察技能 {s.ObservationSkill:F1}\n{(s.ActiveRegionId=="home"?"生活设施与员工提供基础照护":"野生个体尚未筛查，先观察再接纳")}";
        _selectedName.Text=view?.Name??"未选择个体";
        _detail.Text=view==null?"点击场景中的团子。":$"{c!.Registration.Grade} · {(view.Quarantine?"隔离观察中":"普通型 · 原型占位")}\n当前：{view.Activity}";
        _fullness.Value=view?.Fullness??0;_energy.Value=view?.Energy??0;_health.Value=view?.Health??0;
        _observation.Text=view==null?"":$"{view.Observation}\n证据可信度 {view.Confidence:P0}\n{c!.Observation.Notes.LastOrDefault()??"野生档案空白，需逐步观察。"}";
        Buttons["close"].Text=close?"E  退出特写":"E  进入互动特写";
        Buttons["feed"].Text=close?(_foodInHand?"F  放回食物":"F  拿取食物 · 1份"):"F  喂食 · 1份";
        Buttons["staff"].Text="员工：自动照护 "+(Sim.ActiveRegion.StaffEnabled?"开":"关");
        Buttons["music"].Text="音乐："+(_muted?"关":"开");
        foreach(var id in new[]{"home","wild","feeder","bed","refill","rescue","save","load","backup","staff"})Buttons[id].Disabled=close;
        Buttons["rescue"].Disabled=close||s.ActiveRegionId=="home"||c==null;
        Buttons["admit"].Disabled=c?.Quarantine!=true||close;
        Buttons["commission"].TooltipText="示例委托：已接纳、饱腹≥70、身体≥80、观察2次并手动喂食2次。每个个体只奖励一次。";
        _events.Text=string.Join("\n",s.Journal.TakeLast(3));
        _hint.Text=_toastTime>0?_toast:close?$"左键拖拽 / 松开放下 · 滚轮调高度 · 接触力度 {_force:F0} · Esc 返回":_buildKind!=null?"点击地面放置设施；右键取消":"Tab 人类视角 · E 特写 · O 观察 · F 喂食 · F5 保存";
        string key=string.Join(',',Actors.Keys);
        if(key!=_residentKey)
        {
            _residentKey=key;foreach(var child in _residents.GetChildren()){_residents.RemoveChild(child);child.QueueFree();}
            foreach(var resident in s.Creatures.Where(x=>x.RegionId==s.ActiveRegionId))
            {
                var button=new Button{Text=resident.Name,FocusMode=Control.FocusModeEnum.None};button.Pressed+=()=>{if(Sim.State.Mode!=PlayMode.CloseUp){Sim.Select(resident.Id);UpdateUi();}};_residents.AddChild(button);
            }
        }
    }
    private string LegacyFeedFailure()
    {
        var c=Sim.Selected;
        if(c==null)return "先选择要喂食的油库里。";
        if(Sim.State.Settings.Pause)return "先继续时间，再喂食。";
        if(c.Condition is PhysicalCondition.Critical or PhysicalCondition.Dead)return "当前身体状态无法进食。";
        if(c.Interaction.EatingRemaining>0)return "上一口还没咽下，稍等一会儿再递食。";
        return "暂时无法喂食，请检查食物库存与身体状态。";
    }
}
