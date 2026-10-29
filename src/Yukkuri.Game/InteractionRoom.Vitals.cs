using Godot;
using System;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private Label _fullnessLabel=null!,_thirstLabel=null!,_healthLabel=null!;
    private ProgressBar _fullnessMeter=null!,_thirstMeter=null!;
    private StyleBoxFlat _fullnessFill=null!,_thirstFill=null!;
    private void BuildVitalMeters(Control parent)
    {
        var meters=Column(parent,5);
        _fullnessLabel=Text("饱食度",13);meters.AddChild(_fullnessLabel);
        _fullnessMeter=NeedMeter(meters,"8aa576",out _fullnessFill);
        _thirstLabel=Text("口渴度",13);meters.AddChild(_thirstLabel);
        _thirstMeter=NeedMeter(meters,"7cabc0",out _thirstFill);
        _healthLabel=Text("身体状态：正常",14);meters.AddChild(_healthLabel);
    }
    private ProgressBar NeedMeter(Control parent,string color,out StyleBoxFlat fill)
    {
        var bar=new ProgressBar{MinValue=0,MaxValue=100,ShowPercentage=false,CustomMinimumSize=new(0,12),MouseFilter=Control.MouseFilterEnum.Ignore};
        bar.AddThemeStyleboxOverride("background",RoomStyle("e2e3d5",5,0));
        fill=RoomStyle(color,5,0);bar.AddThemeStyleboxOverride("fill",fill);parent.AddChild(bar);return bar;
    }
    private void RefreshVitalMeters(Creature c)
    {
        double full=Math.Clamp(c.Fullness,0,100),thirst=100-Math.Clamp(c.Interaction.Hydration,0,100);
        _fullnessMeter.Value=full;_thirstMeter.Value=thirst;
        _fullnessLabel.Text=$"饱食度  {Math.Round(full):0}/100";
        _thirstLabel.Text=$"口渴度  {Math.Round(thirst):0}/100 · 越高越渴";
        _fullnessFill.BgColor=new Color(full<25?"bd795d":"8aa576");
        _thirstFill.BgColor=new Color(thirst>75?"bd795d":"7cabc0");
        _healthLabel.Text="身体状态："+(c.HealthStatus switch
        {
            HealthStatus.Dead=>"死亡",HealthStatus.Critical=>"重伤 · 急需救治",
            HealthStatus.Recovering=>"恢复中",HealthStatus.Injured=>"受伤",_=>"正常"
        });
    }
    private static string WasteLabel(RoomObject obj)=>obj.Kind==RoomObjectKind.Urine?"尿液 · Del 清理":"粪便 · Del 清理";
    private static void BuildWaste(Node3D root,RoomObjectKind kind)
    {
        if(kind==RoomObjectKind.Urine)
        {
            var puddle=SceneArt.Ball(root,new(0,.014f,0),new(.60f,.024f,.46f),"c5b565");
            SceneArt.EnableSurfaceFade(puddle);
            SceneArt.SetSurfaceOpacity(puddle,.65f);
        }
        else
        {
            SceneArt.Ball(root,new(0,.07f,0),new(.26f,.14f,.23f),"71513a");
            SceneArt.Ball(root,new(.08f,.05f,.09f),new(.17f,.10f,.16f),"806044");
        }
    }
}
