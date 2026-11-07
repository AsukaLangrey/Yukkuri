using Godot;
using System;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private Godot.Environment _yardEnvironment=null!;
    private DirectionalLight3D _sunLight=null!,_moonLight=null!;
    private HBoxContainer _systemControls=null!;

    private void BuildDayNightLighting()
    {
        _yardEnvironment=new(){BackgroundMode=Godot.Environment.BGMode.Color,
            AmbientLightSource=Godot.Environment.AmbientSource.Color,TonemapMode=Godot.Environment.ToneMapper.Linear};
        AddChild(new WorldEnvironment{Environment=_yardEnvironment});
        _sunLight=new(){ShadowEnabled=true};AddChild(_sunLight);
        _moonLight=new(){RotationDegrees=new(-42,-28,0),LightColor=new Color("9bb6e5"),ShadowEnabled=false};AddChild(_moonLight);
        BuildCelestialObjects();RefreshDayNightLighting();
    }
    private void RefreshDayNightLighting()
    {
        var time=CourtyardCalendar.At(Sim.State.WorldSeconds);float daylight=(float)time.Daylight;
        float sunTravel=(float)Math.Clamp((time.Hour-time.Sunrise)/(time.Sunset-time.Sunrise),0,1);
        _sunLight.RotationDegrees=new(-10-65*Mathf.Sin(sunTravel*Mathf.Pi),-70+140*sunTravel,0);
        _sunLight.LightEnergy=.34f*daylight;
        float warmth=1-Mathf.Sin(sunTravel*Mathf.Pi);
        _sunLight.LightColor=Colors.White.Lerp(new Color("ffc38a"),warmth*.55f);
        _moonLight.LightEnergy=.22f*(1-daylight);
        _yardEnvironment.AmbientLightColor=new Color("7a91b7").Lerp(new Color("f3ead8"),daylight);
        _yardEnvironment.AmbientLightEnergy=Mathf.Lerp(.28f,.30f,daylight);
        _yardEnvironment.BackgroundColor=new Color("25334b").Lerp(new Color("c3cfba"),daylight);
        if(_ui!=null)RefreshCelestialObjects(time);
    }
    private void BuildCanvasClock()
    {
        _clockLabel=Text("",14,"fff8e6");_clockLabel.Name="CanvasClock";
        _clockLabel.AutowrapMode=TextServer.AutowrapMode.Off;
        _clockLabel.AddThemeColorOverride("font_outline_color",new Color("253b32"));
        _clockLabel.AddThemeConstantOverride("outline_size",4);
        _clockLine=Row(_ui,14);_clockLine.Name="ClockAndSpeed";_clockLine.MouseFilter=Control.MouseFilterEnum.Ignore;_clockLine.AddChild(_clockLabel);
        _speedLinks=new RichTextLabel{BbcodeEnabled=true,FitContent=true,ScrollActive=false,CustomMinimumSize=new(190,24),MouseFilter=Control.MouseFilterEnum.Stop};
        _speedLinks.AddThemeFontSizeOverride("normal_font_size",14);_speedLinks.AddThemeColorOverride("default_color",new Color("fff8e6"));
        _speedLinks.AddThemeConstantOverride("outline_size",4);_speedLinks.AddThemeColorOverride("font_outline_color",new Color("253b32"));
        _speedLinks.MetaClicked+=value=>{if(int.TryParse(value.AsString(),out int speed)){Sim.SetCourtyardSpeed(speed);RefreshRoomUi();}};
        _speedButton=_speedLinks;_clockLine.AddChild(_speedLinks);
        RefreshCalendarClock();
    }
    private void RefreshCalendarClock()
    {
        if(_speedLinks!=null)_speedLinks.Text=string.Join("  ",System.Linq.Enumerable.Select(WorldSimulation.CourtyardSpeeds,speed=>speed==Sim.CourtyardSpeed?$"[color=#f5d58c][u]{speed}×[/u][/color]":$"[url={speed}]{speed}×[/url]"));
        var time=CourtyardCalendar.At(Sim.State.WorldSeconds);
        string status=Sim.State.Settings.Pause?" · 暂停":Sim.State.Mode==PlayMode.CloseUp?" · 近看暂停":"";
        _clockLabel.Text=$"{time.SeasonName} {time.DayOfSeason}日 · {time.Clock} · {time.Phase} · {CourtyardClimate.Temperature(Sim.State.WorldSeconds):0}°C{status}";
    }
    private void BuildSidebarSystemControls(Control parent)
    {
        BuildYardHelpButton(parent);
        _systemControls=Row(parent,4);_systemControls.Visible=false;
        _pauseButton=Button("停一停",TogglePause);_systemControls.AddChild(_pauseButton);
        _fullscreenButton=Button("全屏",ToggleFullscreen);_systemControls.AddChild(_fullscreenButton);
        var navigation=Row(parent,4);
        var system=Button("系统 · Esc",ShowSystemMenu);system.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;navigation.AddChild(system);
    }

    private void LayoutCalendarStrip()
    {
        if(_resourceBar==null||_clockLabel==null)return;
        var canvas=GameUiLayout.Canvas(_ui);
        var resources=_resourceBar.GetCombinedMinimumSize();var clock=_clockLine.GetCombinedMinimumSize();
        bool stacked=resources.X+clock.X+36>canvas.Size.X;
        _clockLine.Size=clock;_clockLine.Position=canvas.Position+new Vector2(12,10);
        _resourceBar.Size=resources;
        _resourceBar.Position=new(canvas.End.X-resources.X-12,canvas.Position.Y+(stacked?40:10));
        if(_eventFeed!=null)_eventFeed.Position=new(_eventFeed.Position.X,canvas.Position.Y+80);
    }
}
