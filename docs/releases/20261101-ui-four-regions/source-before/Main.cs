using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Yukkuri.Simulation;

public partial class Main : Node3D
{
    internal static bool NameplatesEnabled=true;
    internal static bool SimpleActors;
    internal WorldSimulation Sim = null!;
    internal Camera3D Camera = null!;
    private DirectionalLight3D _sun=null!;
    internal readonly Dictionary<string,CreatureActor> Actors = new();
    private Node3D _stage=null!, _human=null!, _foodVisual=null!;
    private Font _font=null!;
    private SaveStore _saves=null!;
    private AudioStreamPlayer _music=null!, _sfx=null!;
    private SimulationConfig _config=null!;
    private CreatureActor? _held;
    private FacilityKind? _buildKind;
    private Vector3 _holdTarget;
    private float _holdHeight=1.3f, _force;
    private double _uiTimer, _autosaveTimer;
    private bool _foodInHand, _muted, _ready, _recovered, _verification, _milestoneVerification;
    private string? _smokeOutput;
    private bool _milestoneStress,_milestoneSoak,_milestoneAudio,_noShadows;
    private int _milestoneSoakSeconds=600;
    private bool _milestoneStates;
    private bool _milestoneAutosave;
    private int _frame, _population=10, _slot=1;
    private string _toast="欢迎回来。点击团子查看档案，Tab 切换人类身份。";
    private double _toastTime=5;
    public override void _Ready()
    {
        try
        {
            var args=OS.GetCmdlineUserArgs();
            for(int i=0;i<args.Length;i++)
            {
                if(args[i]=="--smoke-output"&&i+1<args.Length)_smokeOutput=args[++i];
                else if(args[i]=="--population"&&i+1<args.Length)_population=int.Parse(args[++i]);
                else if(args[i]=="--integration")_verification=true;
                else if(args[i]=="--m01-integration"){_verification=true;_milestoneVerification=true;}
                else if(args[i]=="--m01-stress"){_verification=true;_milestoneVerification=true;_milestoneStress=true;}
                else if(args[i]=="--m01-soak"){_verification=true;_milestoneVerification=true;_milestoneSoak=true;}
                else if(args[i]=="--m01-soak-seconds"&&i+1<args.Length)_milestoneSoakSeconds=Math.Clamp(int.Parse(args[++i]),600,28800);
                else if(args[i]=="--m01-audio"){_verification=true;_milestoneVerification=true;_milestoneAudio=true;}
                else if(args[i]=="--m01-states"){_verification=true;_milestoneVerification=true;_milestoneStates=true;}
                else if(args[i]=="--m01-autosave"){_verification=true;_milestoneVerification=true;_milestoneAutosave=true;}
                else if(args[i]=="--no-shadows")_noShadows=true;
                else if(args[i]=="--no-nameplates")NameplatesEnabled=false;
                else if(args[i]=="--simple-actors")SimpleActors=true;
                else if(args[i]=="--mute")_muted=true;
            }
            _config=SimulationConfig.Parse(Godot.FileAccess.GetFileAsString("res://Data/simulation.json"));
            Sim=(!_verification||_milestoneVerification)&&_population<50?WorldSimulation.CreateMilestone():WorldSimulation.Create(_population,false,_config);
            GetWindow().Title=$"油库里乐园 · {(Sim.State.MilestoneScenario?"小园初晴":"基础框架")} · {GameVersion.Display}";
            GD.Print("YUKKURI_VERSION: "+GameVersion.Display);
            _font=new FontVariation { BaseFont=GD.Load<FontFile>("res://Assets/Fonts/NotoSansSC.ttf"),VariationOpentype=new Godot.Collections.Dictionary{[TextServerManager.GetPrimaryInterface().NameToTag("wght")]=500.0},VariationEmbolden=.35f };
            string saveDirectory=_verification
                ? (_smokeOutput!=null?Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_smokeOutput))!,"saves"):ProjectSettings.GlobalizePath("user://verification-saves/run-"+OS.GetProcessId()))
                : ProjectSettings.GlobalizePath(Sim.State.MilestoneScenario?"user://m01-saves":"user://saves");
            _saves=new SaveStore(saveDirectory);
            if(_verification)GD.Print("VERIFICATION_SAVES: "+saveDirectory);
            Camera=new Camera3D {Projection=Camera3D.ProjectionType.Orthogonal,Size=25,Current=true,Far=150};AddChild(Camera);
            _sun=new DirectionalLight3D {RotationDegrees=new(-52,-35,0),LightEnergy=.32f,ShadowEnabled=!_noShadows,DirectionalShadowMaxDistance=60,LightCullMask=uint.MaxValue,ShadowCasterMask=uint.MaxValue,Layers=SceneArt.CreatureLayer};AddChild(_sun);
            AddChild(new WorldEnvironment {Environment=new Godot.Environment {BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("a9b9aa"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("d8dfd4"),AmbientLightEnergy=.28f,TonemapMode=Godot.Environment.ToneMapper.Linear}});
            _music=new AudioStreamPlayer {VolumeDb=-15};AddChild(_music);
            _sfx=new AudioStreamPlayer {VolumeDb=-14};AddChild(_sfx);
            BuildUi(); RebuildRegion(); UpdateUi();_ready=true;
            GetTree().AutoAcceptQuit=false;GetWindow().CloseRequested+=()=>QuitGameCleanly();
            GD.Print("PIPELINE_READY: framework core + persistent regions + C# + assets + audio");
            if(_milestoneAutosave)CallDeferred(nameof(StartMilestoneAutosave));
            else if(_milestoneStates)CallDeferred(nameof(StartMilestoneStates));
            else if(_milestoneAudio)CallDeferred(nameof(StartMilestoneAudio));
            else if(_milestoneSoak)CallDeferred(nameof(StartMilestoneSoak));
            else if(_milestoneStress)CallDeferred(nameof(StartMilestoneStress));
            else if(_milestoneVerification)CallDeferred(nameof(StartMilestoneIntegration));
            else if(_verification)CallDeferred(nameof(StartIntegration));
        }
        catch(Exception e){GD.PushError(e.ToString());QuitGameCleanly(2);}
    }
    private void Toast(string text){_toast=text;_toastTime=5;}
    private void Sound(string name)
    {
        if(_muted)return;
        if(Sim.State.MilestoneScenario)_sfx.VolumeDb=Sim.State.Settings.EffectsVolume<=0?-80:Mathf.LinearToDb((float)Sim.State.Settings.EffectsVolume)-10;
        _sfx.Stream=GD.Load<AudioStream>("res://Assets/Audio/"+name+".wav");_sfx.Play();
    }
    private void SetMusic()
    {
        var audio=GD.Load<AudioStreamWav>("res://Assets/Audio/"+(Sim.State.ActiveRegionId=="home"?"sanctuary-loop":"wild-edge-loop")+".wav");
        audio.LoopMode=AudioStreamWav.LoopModeEnum.Forward;audio.LoopBegin=0;audio.LoopEnd=(int)Math.Round(audio.GetLength()*audio.MixRate);
        _music.Stream=audio;_music.Play();_music.VolumeDb=_muted?-80:Sim.State.MilestoneScenario?(Sim.State.Settings.MusicVolume<=0?-80:Mathf.LinearToDb((float)Sim.State.Settings.MusicVolume)-12):-15;
    }
    public override void _Process(double delta)
    {
        if(!_ready)return;
        // A stall is not offline progress. Simulation time advances only while the app is running.
        float dt=(float)Math.Min(delta,.1);
        if(CloseInteractionSuspended)SuspendCloseInteraction();
        Sim.AdvanceInteractionPresentation(dt);
        Sim.Advance(dt*(Sim.State.MilestoneScenario?Sim.State.Settings.Speed:1));
        if(Sim.State.MilestoneScenario)ProcessMilestone(dt);
        bool close=Sim.State.Mode==PlayMode.CloseUp;
        foreach(var c in Sim.State.Creatures.Where(c=>c.RegionId==Sim.State.ActiveRegionId))
            if(Actors.TryGetValue(c.Id,out var actor))
            {
                actor.Visible=!close||c.Id==Sim.State.SelectedId;
                actor.CollisionLayer=actor.Visible?2u:0u;
                actor.Refresh(c,c.Id==Sim.State.SelectedId,close&&c.Id==Sim.State.SelectedId,CloseInteractionSuspended?0:dt,_force,CloseInteractionSuspended);
            }
        _human.Visible=Sim.State.Mode==PlayMode.Human||(Sim.State.MilestoneScenario&&!close);
        if(_human.Visible&&Sim.State.Mode==PlayMode.Human&&(!Sim.State.MilestoneScenario||!Sim.State.Settings.Pause))
        {
            var move=new Vector3((MoveKeyPressed(Key.D)?1:0)-(MoveKeyPressed(Key.A)?1:0),0,(MoveKeyPressed(Key.S)?1:0)-(MoveKeyPressed(Key.W)?1:0));
            var worldMove=(new Vector3(.8f,0,-.6f)*move.X+new Vector3(.6f,0,.8f)*move.Z).Normalized()*dt*4;
            Sim.State.PlayerX=Math.Clamp(Sim.State.PlayerX+worldMove.X,-8,8);Sim.State.PlayerZ=Math.Clamp(Sim.State.PlayerZ+worldMove.Z,-8,8);
        }
        _human.Position=new((float)Sim.State.PlayerX,0,(float)Sim.State.PlayerZ);
        UpdateCamera(dt);
        if(_foodInHand&&close&&Sim.Selected!=null)
        {
            _foodVisual.Visible=true;_foodVisual.GlobalPosition=PointerOnPlane(GetViewport().GetMousePosition(),.8f);
        }
        else _foodVisual.Visible=false;
        _uiTimer+=delta;_autosaveTimer+=dt;_toastTime-=delta;
        if(_uiTimer>.2){_uiTimer=0;UpdateUi();}
        if(_autosaveTimer>60&&!close&&(!_verification||_milestoneAutosave)&&!_recovered&&(!Sim.State.MilestoneScenario||_milestoneStarted)){_autosaveTimer=0;Save(0,false);}
        if(!_verification&&_population<50&&_smokeOutput!=null&&++_frame==30)CaptureAndQuit();
        CollectBenchmark(delta);
    }
    public override void _PhysicsProcess(double delta)
    {
        if(!_ready)return;
        if(CloseInteractionSuspended){SuspendCloseInteraction();return;}
        if(Sim!=null&&Sim.State.MilestoneScenario&&_gestureActive&&_contactAction==ContactAction.Hug&&Sim.Selected!=null&&Actors.TryGetValue(Sim.Selected.Id,out var hugged))
        {
            var target=new Vector3((float)Sim.Selected.X,.72f*hugged.BodyScale,(float)Sim.Selected.Z);
            hugged.ApplyCentralForce(((target-hugged.GlobalPosition)*30-hugged.LinearVelocity*8+Vector3.Up*9.8f*hugged.Mass).LimitLength(35));
        }
        if(_held!=null)
        {
            var force=(_holdTarget-_held.GlobalPosition)*75-_held.LinearVelocity*12;
            force=force.LimitLength(80);_force=force.Length();_held.ApplyCentralForce(force);
        }
        else _force=Mathf.Lerp(_force,0,.15f);
    }
    private void UpdateCamera(float dt)
    {
        bool close=Sim.State.Mode==PlayMode.CloseUp;
        // Close-up is an inspection view: gates, foliage and location signs must
        // never cover the face or the input target. Physics stays in the same world.
        Camera.CullMask=close&&Sim.State.MilestoneScenario?SceneArt.CreatureLayer|SceneArt.GroundLayer|SceneArt.InteractionLayer:uint.MaxValue;
        _sun.ShadowCasterMask=close&&Sim.State.MilestoneScenario?SceneArt.CreatureLayer|SceneArt.InteractionLayer:uint.MaxValue;
        Vector3 focus=Vector3.Zero;
        if(close&&Sim.Selected!=null&&Actors.TryGetValue(Sim.Selected.Id,out var a))focus=new(a.Position.X,.7f,a.Position.Z);
        else if(Sim.State.Mode==PlayMode.Human)focus=_human.Position*.5f;
        if(!close&&Sim.State.MilestoneScenario)focus+=_mapPan;
        Vector3 desired=focus+(close?new Vector3(0,2.5f,5.8f):new Vector3(16,20,24));
        Camera.Position=Camera.Position.Lerp(desired,Mathf.Min(1,dt*7));
        Camera.Size=Mathf.Lerp(Camera.Size,close?(Sim.State.MilestoneScenario?4.4f:6.5f):Sim.State.MilestoneScenario?_mapZoom:25,Mathf.Min(1,dt*7));Camera.LookAt(focus,Vector3.Up);
        if(Sim.State.MilestoneScenario){Camera.VOffset=Mathf.Lerp(Camera.VOffset,close?-.9f:-3f,Mathf.Min(1,dt*7));Camera.HOffset=close?.18f:1;}
    }
    private void RebuildRegion(bool resetCamera=true)
    {
        _visibleActorKey=string.Join(",",Sim.State.Creatures.Where(c=>c.RegionId==Sim.State.ActiveRegionId).Select(c=>c.Id));
        _held=null;_foodInHand=false;
        if(Sim.State.MilestoneScenario&&ActivateCachedRegion(resetCamera))return;
        if(_stage!=null&&!Sim.State.MilestoneScenario){RemoveChild(_stage);_stage.QueueFree();}
        _stage=new Node3D();AddChild(_stage);Actors.Clear();
        bool wild=Sim.ActiveRegion.Kind==RegionKind.Wilderness;
        SceneArt.Box(_stage,new(0,-.32f,0),new(19,.6f,19),wild?"697968":"a4b78c").Layers=SceneArt.GroundLayer;
        var ground=new StaticBody3D{CollisionLayer=1,CollisionMask=2};_stage.AddChild(ground);
        ground.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(19,.5f,19)},Position=new(0,-.26f,0)});
        foreach(float edge in new[]{-9.3f,9.3f})
        {
            ground.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(.4f,5,19)},Position=new(edge,2,0)});
            ground.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(19,5,.4f)},Position=new(0,2,edge)});
        }
        for(int i=-8;i<=8;i+=2)
        {
            SceneArt.Box(_stage,new(i,.35f,-9),new(.14f,.9f,.14f),"c3a27d");
            SceneArt.Box(_stage,new(-9,.35f,i),new(.14f,.9f,.14f),"c3a27d");
        }
        SceneArt.Box(_stage,new(0,.35f,-9),new(18,.09f,.09f),"c3a27d");
        SceneArt.Box(_stage,new(-9,.35f,0),new(.09f,.09f,18),"c3a27d");
        for(int i=0;i<8;i++)
        {
            SceneArt.Tree(_stage,new(-8+i*2.25f,0,-8),.8f+(i%3)*.12f,wild);
            if(wild)SceneArt.Tree(_stage,new(-8,0,-5+i*1.7f),.8f,wild);
        }
        if(!wild)
        {
            SceneArt.Box(_stage,new(1,.015f,1.5f),new(2,.025f,14),"d6c59e");
            SceneArt.Box(_stage,new(-5.8f,.02f,5.5f),new(3.4f,.04f,3.2f),"c4c3a6");
            if(NameplatesEnabled){var areaLabel=new Label3D{Text="观察区",Font=_font,FontSize=38,PixelSize=.01f,Position=new(-5.8f,.45f,7.4f),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,Modulate=new Color("e4ebd7"),OutlineModulate=new Color("435f50"),OutlineSize=5};_stage.AddChild(areaLabel);}
            SceneArt.Box(_stage,new(5,.65f,-5.5f),new(2,1.3f,1.7f),"d0ac7b");
            var roof=SceneArt.Box(_stage,new(5,1.45f,-5.5f),new(2.4f,.25f,2.1f),"6f8c75");roof.RotationDegrees=new(0,0,5);
            SceneArt.Box(_stage,new(5,.5f,-4.62f),new(.6f,1,.04f),"6e604d");
        }
        foreach(var f in Sim.ActiveRegion.Facilities)
        {
            var node=SceneArt.Facility(_stage,f);
            if(Sim.State.MilestoneScenario)RememberFacility(f,node);
        }
        int index=0;
        foreach(var c in Sim.State.Creatures.Where(c=>c.RegionId==Sim.State.ActiveRegionId))
        {
            CreatureActor actor;
            if(Sim.State.MilestoneScenario&&_persistentActors.TryGetValue(c.Id,out var existing))
            {actor=existing;actor.Reparent(_stage);actor.ProcessMode=ProcessModeEnum.Inherit;actor.Visible=true;actor.Position=new((float)c.X,.46f*actor.BodyScale,(float)c.Z);}
            else{actor=new CreatureActor();_stage.AddChild(actor);actor.Setup(c,index,_font);if(Sim.State.MilestoneScenario)_persistentActors[c.Id]=actor;}
            index++;Actors[c.Id]=actor;
            actor.Impact=(id,speed)=>{if(Sim.State.MilestoneScenario){var result=Sim.RegisterImpact(id,speed);if(result.Success)Toast(result.Message);}};
        }
        _human=new Node3D();_stage.AddChild(_human);
        if(!Sim.State.MilestoneScenario){
        SceneArt.Mesh(_human,new CapsuleMesh{Radius=.22f,Height=.9f},new(0,.65f,0),"526c7c");
        SceneArt.Ball(_human,new(0,1.24f,0),new(.42f,.42f,.42f),"f3d1ae");
        SceneArt.Box(_human,new(0,.17f,0),new(.3f,.35f,.25f),"544d4b");
        }
        _foodVisual=new Node3D();_stage.AddChild(_foodVisual);SceneArt.Ball(_foodVisual,Vector3.Zero,new(.25f,.2f,.25f),"e7ab59");_foodVisual.Visible=false;
        if(Sim.State.MilestoneScenario)DecorateMilestoneRegion();
        if(Sim.State.MilestoneScenario)RememberRegionPresentation();
        SetMusic();
        if(resetCamera){_mapPan=Vector3.Zero;_mapZoom=25;Camera.Position=new(16,20,24);Camera.LookAt(Vector3.Zero);Camera.Size=25;}
    }
    internal void Travel(string id){if(Sim.Travel(id)){_buildKind=null;RebuildRegion();Toast("已抵达"+Sim.ActiveRegion.Name);}else Toast("请先退出特写。");}
    internal void CloseUp()
    {
        if(Sim.State.Mode==PlayMode.CloseUp){ExitCloseUp();return;}
        if(Sim.EnterCloseUp()){_buildKind=null;Toast(Sim.State.MilestoneScenario?"选择下方互动，再按住目标；拿起需选择“拿起 / 放下”。E 返回场景。":"拖拽团子可拿起；滚轮调高度。拿食物后点击嘴部喂食。");}
        else Toast("先选中一只团子。");
    }
    internal void ExitCloseUp()
    {
        if(Sim.State.MilestoneScenario)CancelGesture();
        _held=null;_foodInHand=false;
        if(Sim.Selected!=null&&Actors.TryGetValue(Sim.Selected.Id,out var actor))
        {Sim.MoveCreature(Sim.Selected.Id,actor.Position.X,actor.Position.Z);actor.Freeze=true;actor.LinearVelocity=Vector3.Zero;}
        Sim.ExitCloseUp();Toast(Sim.State.Settings.Pause?"已返回场景；游戏仍暂停。空格可继续。":"已返回管理视角；被动时间恢复。");
    }
    internal void HumanMode(){if(Sim.State.Mode==PlayMode.CloseUp)ExitCloseUp();Sim.State.Mode=Sim.State.Mode==PlayMode.Human?PlayMode.Management:PlayMode.Human;_buildKind=null;}
    internal void Save(int slot,bool notify=true)
    {
        try
        {
            if(Sim.State.Mode==PlayMode.CloseUp){Toast("请先放下团子并退出特写再保存。");return;}
            _saves.Save(Sim.State,slot);if(notify)Toast("已保存到槽位 "+slot);
        }
        catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException){Toast(e.Message);GD.Print("SAVE_REJECTED: "+e.Message);}
    }
    internal bool Load(int slot)
    {
        try
        {
            var loaded=_saves.Load(slot);
            if(Sim.State.MilestoneScenario&&!loaded.State.MilestoneScenario)throw new InvalidDataException("这是早期框架存档，请使用小园初晴的存档槽位。");
            Sim=new WorldSimulation(loaded.State,_config);
            // Closing a menu after loading must restore the loaded clock state.
            _menuPreviousPause=Sim.State.Settings.Pause;
            // Interaction holds are transient and must not survive a reload.
            if(Sim.State.Mode==PlayMode.CloseUp)Sim.ExitCloseUp();
            _recovered=loaded.RecoveredBackup;_autosaveTimer=0;_buildKind=null;RebuildRegion();Toast(loaded.Message);
            return true;
        }
        catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException){Toast(e.Message);GD.Print("LOAD_REJECTED: "+e.Message);return false;}
    }
    private async void CaptureAndQuit()
    {
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        string path=Path.GetFullPath(_smokeOutput!);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var error=GetViewport().GetTexture().GetImage().SavePng(path);GD.Print($"RENDER_CAPTURE: {error} -> {path}");QuitGameCleanly(error==Error.Ok?0:3);
    }
}


