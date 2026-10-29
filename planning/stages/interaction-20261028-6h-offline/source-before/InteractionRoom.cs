using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Yukkuri.Simulation;

public partial class InteractionRoom : Node3D
{
    internal WorldSimulation Sim=null!;
    private Camera3D _camera=null!;
    private Font _font=null!;
    private SaveStore _saves=null!;
    private readonly Dictionary<string,CreatureActor> _actors=new();
    private readonly Dictionary<string,Node3D> _objects=new();
    private readonly Dictionary<string,(string pose,double until)> _poses=new();
    private Node3D _yard=null!,_hand=null!,_player=null!,_closeFloor=null!;
    private AudioStreamPlayer _sound=null!;
    private bool _ready,_muted,_recovered;
    private double _uiClock,_autoClock,_elapsed,_lastSound;
    private int _slot=1;
    private float _force=.25f,_height=.8f,_zoom=16;
    private InteractionAction _action=InteractionAction.Greet;
    private bool _toolArmed;
    private Vector3 _closeFocus;
    private string _message="点一下小油打招呼，双击进入近看。按空格可以停一停。";
    private string? _lastSpoken;

    public override void _Ready()
    {
        try
        {
            var args=OS.GetCmdlineUserArgs();
            if(args.Any(a=>a.StartsWith("--m01-")||a=="--integration"||a=="--legacy"))
            {GetTree().ChangeSceneToFile("res://Main.tscn");return;}
            GetWindow().Title="油库里乐园 · 慢慢来，相处小院 · 人油互动试玩";
            Sim=WorldSimulation.CreateInteractionPlaytest();
            _font=new FontVariation { BaseFont=GD.Load<FontFile>("res://Assets/Fonts/NotoSansSC.ttf"),
                VariationOpentype=new Godot.Collections.Dictionary { [TextServerManager.GetPrimaryInterface().NameToTag("wght")]=500.0 },
                VariationEmbolden=.15f };
            string saveDirectory="user://interaction-saves";
            int testProfileFlag=Array.IndexOf(args,"--interaction-test-profile");
            if(testProfileFlag>=0)
            {
                if(testProfileFlag+1>=args.Length||!System.Text.RegularExpressions.Regex.IsMatch(args[testProfileFlag+1],@"\A[a-zA-Z0-9_-]{1,64}\z"))
                    throw new ArgumentException("Interaction test profile must be a short alphanumeric name.");
                saveDirectory="user://interaction-test-saves/"+args[testProfileFlag+1];
                GD.Print("INTERACTION_TEST_PROFILE: "+args[testProfileFlag+1]);
            }
            _saves=new SaveStore(ProjectSettings.GlobalizePath(saveDirectory));
            _camera=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=_zoom,Current=true,Far=100};AddChild(_camera);
            AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,
                BackgroundColor=new Color("c3cfba"),AmbientLightSource=Godot.Environment.AmbientSource.Color,
                AmbientLightColor=new Color("f3ead8"),AmbientLightEnergy=.30f,TonemapMode=Godot.Environment.ToneMapper.Linear}});
            AddChild(new DirectionalLight3D{RotationDegrees=new(-48,-28,0),LightEnergy=.34f,ShadowEnabled=true});
            BuildYard();BuildRoomUi();BuildActors();DisarmTool();
            _sound=new AudioStreamPlayer{VolumeDb=-20};AddChild(_sound);
            GetWindow().FocusExited+=()=>{CancelGesture();};
            GetTree().AutoAcceptQuit=false;GetWindow().CloseRequested+=QuitRoom;
            UpdateRoomCamera(1);_ready=true;RefreshRoomUi();
            GD.Print("INTERACTION_ROOM_READY: 20 groups, isolated saves, first batch in development");
            StartRoomVerificationIfRequested(args);
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(2);}
    }

    private void BuildYard()
    {
        _yard=new Node3D();AddChild(_yard);
        _closeFloor=new Node3D{Visible=false};AddChild(_closeFloor);
        SceneArt.Box(_closeFloor,new(0,-.045f,0),new(60,.08f,60),"c6cbb4");
        SceneArt.Box(_yard,new(0,-.17f,0),new(17,.28f,17),"c6c99b");
        SceneArt.Box(_yard,new(0,-.025f,0),new(11,.03f,10),"ded3b6");
        var floor=new StaticBody3D{CollisionLayer=1,CollisionMask=0};_yard.AddChild(floor);
        floor.AddChild(new CollisionShape3D{Position=new(0,-.15f,0),Shape=new BoxShape3D{Size=new(17,.3f,17)}});
        for(int a=-8;a<=8;a++)
        {
            foreach(float z in new[]{-8.2f,8.2f})SceneArt.Box(_yard,new(a,.32f,z),new(.14f,.8f,.12f),"aa9a71");
            foreach(float x in new[]{-8.2f,8.2f})SceneArt.Box(_yard,new(x,.32f,a),new(.12f,.8f,.14f),"aa9a71");
        }
        foreach(float edge in new[]{-8.2f,8.2f})
        {SceneArt.Box(_yard,new(0,.41f,edge),new(16.7f,.10f,.10f),"aa9a71");SceneArt.Box(_yard,new(edge,.41f,0),new(.10f,.10f,16.7f),"aa9a71");}
        for(int a=0;a<10;a++)
        {
            float x=a%2==0?-6.8f:6.7f,z=-6.5f+(a/2)*2.7f;
            SceneArt.Ball(_yard,new(x,.12f,z),new(.65f,.36f,.7f),a%3==0?"a6b78c":"b6bf92");
            SceneArt.Ball(_yard,new(x+.16f,.29f,z+.12f),new(.15f,.12f,.15f),"e1c087");
        }
        _player=new Node3D();AddChild(_player);
        SceneArt.Mesh(_player,new TorusMesh{InnerRadius=.24f,OuterRadius=.30f,Rings=24,RingSegments=6},new(0,.035f,0),"faf2d9");
        SceneArt.Box(_player,new(0,.04f,.23f),new(.07f,.02f,.16f),"faf2d9");
        _hand=new Node3D();AddChild(_hand);
        SceneArt.Ball(_hand,Vector3.Zero,new(.17f,.12f,.20f),"efc5a4");
        SceneArt.Ball(_hand,new(0,0,-.12f),new(.07f,.08f,.20f),"f4d4b7");
        _hand.Visible=false;
    }

    private void BuildActors()
    {
        foreach(var actor in _actors.Values)actor.QueueFree();_actors.Clear();
        int index=0;
        foreach(var c in Sim.State.Creatures)
        {var actor=new CreatureActor();AddChild(actor);actor.Setup(c,index++,_font);_actors[c.Id]=actor;}
        foreach(var obj in _objects.Values)obj.QueueFree();_objects.Clear();
    }

    private Node3D MakeObject(RoomObject obj)
    {
        var root=new Node3D();AddChild(root);
        switch(obj.Kind)
        {
            case RoomObjectKind.Meal:
                SceneArt.Mesh(root,new CylinderMesh{TopRadius=.30f,BottomRadius=.25f,Height=.09f,RadialSegments=20},new(0,.055f,0),"e8e3ce");
                for(int i=0;i<5;i++)SceneArt.Ball(root,new((i%3-1)*.12f,.13f,(i/3-.5f)*.15f),new(.16f,.10f,.14f),"e3c081");break;
            case RoomObjectKind.Sweet:
                SceneArt.Mesh(root,new CylinderMesh{TopRadius=.18f,BottomRadius=.18f,Height=.09f,RadialSegments=12},new(0,.055f,0),"d69e61");
                SceneArt.Ball(root,new(.04f,.105f,.02f),new(.045f,.016f,.045f),"76523e");break;
            case RoomObjectKind.Ball:
                SceneArt.Ball(root,new(0,.24f,0),new(.48f,.48f,.48f),"c8a077");
                SceneArt.Mesh(root,new TorusMesh{InnerRadius=.231f,OuterRadius=.245f,Rings=24,RingSegments=6},new(0,.24f,0),"f3e2bd");break;
            case RoomObjectKind.Treasure:SceneArt.Ball(root,new(0,.08f,0),new(.22f,.16f,.18f),"83a7a0");break;
            case RoomObjectKind.Cushion:
                SceneArt.Ball(root,new(0,.08f,0),new(1.8f,.23f,1.35f),"9fae93");
                SceneArt.Ball(root,new(0,.17f,0),new(1.55f,.12f,1.14f),"c3c9ae");break;
            default:SceneArt.Ball(root,new(0,.08f,0),new(.32f,.12f,.32f),"8cbcc5");break;
        }
        return root;
    }

    public override void _Process(double delta)
    {
        if(!_ready)return;
        float dt=(float)Math.Min(.1,delta);_elapsed+=dt;
        if(!_modalOpen)
        {
            ProcessRoomGesture(dt);
            Sim.AdvanceInteractionPresentation(dt);Sim.Advance(dt);
            MovePlayer(dt);
        }
        bool close=Sim.State.Mode==PlayMode.CloseUp;
        bool paused=Sim.State.Settings.Pause||_modalOpen;
        UpdateRoomCamera(dt);
        foreach(var c in Sim.State.Creatures)
        {
            var actor=_actors[c.Id];actor.Visible=!close||Sim.State.SelectedId==c.Id;
            string pose=c.Interaction.EatingRemaining>0?"eat":c.Interaction.RestRemaining>0?"rest":"idle";
            if(_poses.TryGetValue(c.Id,out var p)&&p.until>_elapsed)pose=p.pose;
            if(Sim.InteractionGestureTarget==c.Id)pose=_gesturePose;
            actor.RefreshInteraction(c,c.Id==Sim.State.SelectedId,close,dt,pose,_force,_gestureDirection,paused);
        }
        foreach(var obj in Sim.State.RoomObjects)
        {
            if(!_objects.TryGetValue(obj.Id,out var visual))_objects[obj.Id]=visual=MakeObject(obj);
            bool nearby=Sim.Selected==null||Math.Pow(obj.X-Sim.Selected.X,2)+Math.Pow(obj.Z-Sim.Selected.Z,2)<4;
            visual.Visible=!obj.Stored&&(!close||nearby);visual.Position=new((float)obj.X,(float)obj.Height,(float)obj.Z);
        }
        foreach(var id in _objects.Keys.Where(id=>!Sim.State.RoomObjects.Any(o=>o.Id==id)).ToArray())
        {_objects[id].QueueFree();_objects.Remove(id);}
        _player.Position=new((float)Sim.State.PlayerX,0,(float)Sim.State.PlayerZ);_player.Visible=!close;
        _yard.Visible=!close;
        _closeFloor.Visible=close;
        _uiClock+=dt;_autoClock+=dt;
        if(_uiClock>.1){_uiClock=0;RefreshRoomUi();}
        if(_autoClock>60&&!Sim.HasInteractionGesture&&!_modalOpen&&!_recovered){_autoClock=0;SaveRoom(0,false);}
        UpdatePointer();
        UpdateLandingPreview();
    }

    private void UpdateRoomCamera(float dt)
    {
        bool close=Sim.State.Mode==PlayMode.CloseUp&&Sim.Selected!=null;
        var c=Sim.Selected;
        bool box=close&&c!.Interaction.Restraint==RestraintKind.Box;
        bool hanging=close&&c!.Interaction.Restraint==RestraintKind.Hanging;
        bool carried=close&&c!.Interaction.Carried;
        float closeSize=box?4.6f:hanging?4.5f:carried?Mathf.Max(4.3f,3.2f+(float)c!.Interaction.Height*1.5f):3.5f;
        if(close&&!Sim.HasInteractionGesture)_closeFocus=new((float)c!.X,
            box?.80f:hanging?1.1f:carried?.55f+(float)c.Interaction.Height*.5f:.62f+(float)c.Interaction.Height,(float)c.Z);
        Vector3 focus=close?_closeFocus:Vector3.Zero;
        Vector3 offset=close?new(0,1.45f,5.6f):new(0,11,16);
        float speed=Mathf.Min(1,dt*9);
        // The yard stays world-aligned, so drag directions and cue movement are consistent.
        _camera.Position=_camera.Position.Lerp(focus+offset,speed);
        if(!close||!Sim.HasInteractionGesture)_camera.Size=Mathf.Lerp(_camera.Size,close?closeSize:_zoom,speed);
        _camera.LookAt(focus,Vector3.Up);
    }

    private void MovePlayer(float dt)
    {
        if(Sim.State.Settings.Pause||Sim.State.Mode==PlayMode.CloseUp)return;
        float dx=(Input.IsPhysicalKeyPressed(Key.D)?1:0)-(Input.IsPhysicalKeyPressed(Key.A)?1:0);
        float dz=(Input.IsPhysicalKeyPressed(Key.S)?1:0)-(Input.IsPhysicalKeyPressed(Key.W)?1:0);
        Vector2 dir=new(dx,dz);if(dir.Length()>1)dir=dir.Normalized();
        Sim.State.PlayerX=Math.Clamp(Sim.State.PlayerX+dir.X*dt*2.3,-7.7,7.7);
        Sim.State.PlayerZ=Math.Clamp(Sim.State.PlayerZ+dir.Y*dt*2.3,-7.7,7.7);
    }

    private void AcceptFeedback(InteractionFeedback result,string? id=null)
    {
        if(result.Message.Length>0)_message=result.Message;
        id??=Sim.State.SelectedId;
        if(id!=null)_poses[id]=(result.Pose,_elapsed+2.2);
        if(result.Speech.Length>0&&result.Speech!=_lastSpoken&&_elapsed-_lastSound>.65)
        {
            _lastSpoken=result.Speech;_lastSound=_elapsed;
            string file=result.Refused?"coo-refuse":_action==InteractionAction.Nuzzle?"coo-nuzzle":_group.Rough?"coo-hurt":"coo-stroke";
            string path="res://Assets/Audio/"+file+".wav";
            if(!_muted&&ResourceLoader.Exists(path)){_sound.Stream=GD.Load<AudioStream>(path);_sound.Play();}
        }
    }

    private void SaveRoom(int slot,bool announce=true)
    {
        try{CancelGesture();_saves.Save(Sim.State,slot);if(announce)_message=$"已保存到槽位 {slot}。身体、饰品和经历会保留。";}
        catch(Exception e){_message="保存未成功，原档保留："+e.Message;GD.PushWarning(e.Message);}
    }
    private void LoadRoom(int slot)
    {
        try
        {
            var load=_saves.Load(slot);if(!load.State.InteractionPlaytest)throw new InvalidDataException("这个槽位不是相处小院存档。");
            CancelGesture();Sim=new WorldSimulation(load.State);Sim.State.Settings.Pause=false;
            _recovered=load.RecoveredBackup;_message=load.Message;_poses.Clear();BuildActors();CloseModal();RefreshRoomUi();
        }
        catch(Exception e){_message="读取未成功："+e.Message;ShowModalMessage(_message);}
    }
    private void QuitRoom(){CancelGesture();if(!_recovered)SaveRoom(0,false);GetTree().Quit();}
}
