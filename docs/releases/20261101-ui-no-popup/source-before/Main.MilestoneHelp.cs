using Godot;

public partial class Main
{
    private PanelContainer _helpPanel=null!;
    private ColorRect _helpShield=null!;
    private bool _helpPreviousPause;
    private void BuildHelp()
    {
        _helpShield=new ColorRect{Color=new Color(.12f,.18f,.14f,.22f),MouseFilter=Control.MouseFilterEnum.Stop,Visible=false};
        _uiRoot.AddChild(_helpShield);_helpShield.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _helpPanel=new PanelContainer{Position=new(330,90),CustomMinimumSize=new(780,720),Visible=false};
        _helpPanel.AddThemeStyleboxOverride("panel",PanelStyle("faf5e9"));_uiRoot.AddChild(_helpPanel);
        var box=new VBoxContainer();box.AddThemeConstantOverride("separation",12);_helpPanel.AddChild(box);
        Text(box,"小园生活指南",28);
        Text(box,"打开此页时，世界和特写物理暂停。关上后继续。",14,"7e8a75");
        var scroll=new ScrollContainer{CustomMinimumSize=new(740,570),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};box.AddChild(scroll);
        var content=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};content.AddThemeConstantOverride("separation",9);scroll.AddChild(content);
        void Section(string title,string body){Text(content,title,20,"648561");var text=Text(content,body,16);text.CustomMinimumSize=new(705,0);}
        Section("先照顾眼前的一只","点击油库里或左侧名单选择。F 喂食，O 观察；右侧档案可以滚动。先喂饱、轻轻抚摸，再慢慢练习。左侧六件事完成后，可以继续经营。培养的变化需要重复相处和生活经历。");
        Section("特写里怎样互动","E 靠近；底部选择抚摸、抱抱、蹭蹭或其他动作，在头顶、脸颊或身体按住，松开完成。力度、持续时间、位置、工具与身体状态共同影响反应。刚认识或疼痛时可能拒绝亲近。先试手掌和 25% 力度。E、Esc 或“返回场景”离开特写。");
        Section("拿起、放下与喂食","选择“拿起 / 放下”，按住目标拖动，滚轮调整高度，松开释放。跌落可能受伤。特写中 F 拿出食物，点击嘴部附近喂食，右键取消。特写暂停被动时间，仍会结算接触与物理后果。");
        Section("到林缘认识新伙伴","点击左侧“林缘”，观察当地油库里。遇到欺凌，选择受害者并“保护它”。改善信任与身体状态后邀请回园；返回乐园选择它，结束观察并正式接纳。拒绝邀请时留意提示，先照护或继续相处。");
        Section("守护乐园与惩戒","入侵时，油库里会尝试守护并可能呼救。“查看求助”定位后，可以保护受害者，或对入侵者警告、惩戒。知晓原因的同伴、无辜受害者与未获知事件者反应不同；过度伤害仍可能引起恐惧。年长或亲油也可能主动管教。");
        Section("伤病与休养","重伤存在救治时限，选择“护理”并接触目标，消耗护理包。稳定后还要休养；缺食会拖慢恢复，可以继续喂食。若仍有其他重伤个体，留意求助。M01 提供基础伤病与救助，完整病种和内馅转移尚未开放。");
        Section("补给与收入","右下购买食物、护理包，并补充食盆。雇员会按基础预案照护，依然消耗库存。完成新的养护记录后可提交委托获得收入，委托有冷却时间。设置里可选无限资源、入侵、意外伤害和照护保护等开关。");
        Section("移动、视野与保存","Tab 切换可见主角，WASD 移动。管理视角滚轮缩放、中键拖动，空格暂停。F5 保存、F9 读取左下选中的 1—3 号槽位；先离开特写。运行期间自动存档，可在菜单读取。关闭游戏不推进时间，跨地区则继续简化模拟。");
        Section("这个版本的范围","小园初晴是第一个可玩阶段，当前以灵梦、魔理沙及基础照护、培养、探索、护园循环为主。六种物种、完整繁衍、升级、评级和主线等内容按后续里程碑推进。");
        Action(box,"close-help","回到刚才的界面",CloseHelp);
    }
    private void OpenHelp()
    {
        _helpPreviousPause=Sim.State.Settings.Pause;Sim.State.Settings.Pause=true;
        SuspendCloseInteraction();_helpShield.Visible=true;_helpPanel.Visible=true;UpdateUi();
    }
    private void CloseHelp()
    {
        _helpPanel.Visible=false;_helpShield.Visible=false;Sim.State.Settings.Pause=_helpPreviousPause;UpdateUi();
    }
}
