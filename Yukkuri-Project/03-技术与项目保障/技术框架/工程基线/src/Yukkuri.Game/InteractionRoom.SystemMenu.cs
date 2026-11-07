using Godot;

public partial class InteractionRoom
{
    private Control _speedButton=null!;
    private RichTextLabel _speedLinks=null!;
    private HBoxContainer _clockLine=null!;
    private void ShowSystemMenu()
    {
        OpenSystemModal(SystemDialogKind.Menu,"系统菜单 · Esc返回");
        _modalContents.AddChild(Button(_pauseBeforeModal?"继续时间 · Space":"停一停 · Space",()=>
        {_pauseBeforeModal=!_pauseBeforeModal;CloseModal();}));
        _modalContents.AddChild(Button(GetWindow().Mode is Window.ModeEnum.Fullscreen or Window.ModeEnum.ExclusiveFullscreen?"切换窗口 · F11":"切换全屏 · F11",()=>{ToggleFullscreen();ShowSystemMenu();}));
        _modalContents.AddChild(Button("调试 · F3",()=>{CloseModal();ToggleDebugPanel();}));
        _modalContents.AddChild(Button("怎么玩",()=>{CloseModal();ShowHelp();}));
        _modalContents.AddChild(Button("保存游戏 · F5",()=>ShowSaveSlots(false)));
        _modalContents.AddChild(Button("读取游戏 · F9",()=>ShowSaveSlots(true)));
        _modalContents.AddChild(Button("游戏设置",ShowSettings));
        var speeds=Row(_modalContents);
        foreach(int speed in Yukkuri.Simulation.WorldSimulation.CourtyardSpeeds)
        {
            var button=Button($"{speed}倍",()=>{Sim.SetCourtyardSpeed(speed);ShowSystemMenu();},Sim.CourtyardSpeed==speed);
            button.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;button.AutowrapMode=TextServer.AutowrapMode.Off;
            speeds.AddChild(button);
        }
        _modalContents.AddChild(Button("返回小院 · Esc",CloseModal));
    }
}
