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
    private Node3D _yard=null!,_hand=null!,_player=null!,_closeFloor=null!;
    private Node3D _woodland=null!;
    private readonly Dictionary<string,int> _shownFoodPortions=new();
    private readonly Dictionary<string,MeshInstance3D> _groundCupWater=new();
    private bool _ready,_recovered,_windowFocused;
    private double _uiClock,_autoClock,_elapsed;
    private int _slot=1;
    private float _force=.25f,_height=.8f,_zoom=16;
    private InteractionAction _action=InteractionAction.Greet;
    private bool _toolArmed;
    private Vector3 _closeFocus;
    private string _message="点一下小油打招呼，双击进入近看。按空格可以停一停。";
    private bool WoodlandVisible=>Sim.State.Mode!=PlayMode.CloseUp&&WoodlandLayout.HasRiver(Sim.State,Sim.State.ActiveRegionId);

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
            InitializeRoomAudio();
            _windowFocused=GetWindow().HasFocus();
            GetWindow().FocusEntered+=()=>{_windowFocused=true;};
            GetWindow().FocusExited+=()=>
            {
                _windowFocused=false;CancelGesture();
                // Focus loss ends live presence immediately; an old follow target is not a hand.
                Sim.State.InteractionHandPresent=false;Sim.State.InteractionHandUntil=0;
                Sim.State.InteractionHandRegionId=null;
            };
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
        for(int a=0;a<5;a++)
        {
            float x=-6.8f,z=-6.5f+a*2.7f;
            SceneArt.Ball(_yard,new(x,.12f,z),new(.65f,.36f,.7f),a%3==0?"a6b78c":"b6bf92");
            SceneArt.Ball(_yard,new(x+.16f,.29f,z+.12f),new(.15f,.12f,.15f),"e1c087");
        }
        _player=new Node3D();AddChild(_player);
        SceneArt.Mesh(_player,new TorusMesh{InnerRadius=.24f,OuterRadius=.30f,Rings=24,RingSegments=6},new(0,.035f,0),"faf2d9");
        SceneArt.Box(_player,new(0,.04f,.23f),new(.07f,.02f,.16f),"faf2d9");
        _hand=new Node3D();AddChild(_hand);
        _hand.Visible=false;
        _woodland=new Node3D();_yard.AddChild(_woodland);WoodlandArt.Build(_woodland);
    }

    private void BuildActors()
    {
        ResetSupplyStationActors();
        ResetDetachedStemActors();
        _heardReactions.Clear();
        foreach(var actor in _actors.Values)actor.QueueFree();_actors.Clear();
        int index=0;
        foreach(var c in Sim.State.Creatures)
        {var actor=new CreatureActor();AddChild(actor);actor.Setup(c,index++,_font);_actors[c.Id]=actor;}
        ResetFamilyPresentation();
        RefreshOilSelector(true);
        foreach(var obj in _objects.Values)obj.QueueFree();_objects.Clear();
        _shownFoodPortions.Clear();_groundCupWater.Clear();
    }

    private int FoodVisualCount(RoomObject obj)
    {
        if(!NaturalResources.IsFood(obj.Kind))return 0;
        int visible=NaturalResources.VisibleFoodPortions(Sim.State,obj);
        return obj.Kind==RoomObjectKind.Leaves?Math.Min(10,visible):visible>0?1:0;
    }
    private bool ObjectHasVisibleGeometry(RoomObject obj)=>obj.Kind switch
    {
        RoomObjectKind.Sweet=>FoodVisualCount(obj)>0,
        RoomObjectKind.RiverWater=>WoodlandVisible,
        _=>true
    };
    private Node3D MakeObject(RoomObject obj)
    {
        if(obj.Kind==RoomObjectKind.Ball)
        {var ball=new RoomBallActor();AddChild(ball);ball.Build();return ball;}
        var root=new Node3D();AddChild(root);
        int visibleFood=FoodVisualCount(obj);
        if(NaturalResources.IsFood(obj.Kind))_shownFoodPortions[obj.Id]=visibleFood;
        switch(obj.Kind)
        {
            case RoomObjectKind.Urine:case RoomObjectKind.Feces:
                BuildWaste(root,obj.Kind);break;
            case RoomObjectKind.Meal:
                InteractionFoodArt.Meal(root,visibleFood>0);break;
            case RoomObjectKind.Sweet:
                if(visibleFood>0)InteractionFoodArt.Sweet(root,grounded:true);break;
            case RoomObjectKind.Treasure:InteractionToolArt.Treasure(root);break;
            case RoomObjectKind.Cushion:
                InteractionToolArt.Cushion(root);break;
            case RoomObjectKind.Leaves:
                WoodlandArt.Add(root,WoodlandSurface.Leaves(Math.Min(10,visibleFood)));break;
            case RoomObjectKind.RiverWater:
                // The shared river mesh supplies the visible water; this is its dry-bank access point.
                break;
            case RoomObjectKind.Water:
                var cup=new Node3D{Position=Vector3.Up*InteractionToolSurface.GroundCupLift};root.AddChild(cup);
                _groundCupWater[obj.Id]=InteractionToolArt.Cup(cup);break;
            default:SceneArt.Ball(root,new(0,.08f,0),new(.32f,.12f,.32f),"8cbcc5");break;
        }
        return root;
    }

    public override void _Process(double delta)
    {
        if(!_ready)return;
        float dt=(float)Math.Min(.1,delta);_elapsed+=dt;
        if(LeaveUnavailableCloseup())_message="当前对象已冷藏或安置，回到小院视角；家庭记录仍可查看，原暂停设置保持不变。";
        if(!_modalOpen)
        {
            if(_windowFocused)ProcessRoomGesture(dt,delta);
            UpdateInteractionHand();
            Sim.AdvanceInteractionPresentation(dt);Sim.Advance(dt);
        }
        bool close=Sim.State.Mode==PlayMode.CloseUp;
        bool paused=Sim.State.Settings.Pause||_modalOpen;
        SynchronizeFamilyActors();
        SynchronizeDetachedStemActors();
        SynchronizeSupplyStationActors();
        UpdateRoomCamera(dt);
        foreach(var c in Sim.State.Creatures)
        {
            var actor=_actors[c.Id];actor.Visible=HasVisibleFamilyBody(c)&&c.RegionId==Sim.State.ActiveRegionId&&(!close||Sim.State.SelectedId==c.Id);
            string pose=InteractionPresentation.Pose(c);
            if(Sim.InteractionGestureTarget==c.Id&&Sim.InteractionGestureOwnsPose)pose=_gesturePose;
            var waterSupport=Sim.WaterSupportStatus(c);
            var supportTarget=waterSupport!=null?Sim.State.Creatures.Find(target=>target.Id==waterSupport.TargetId):null;
            actor.PrepareWaterConcern(c,Sim.WaterConcernTarget(c),waterSupport,supportTarget,
                new Vector2((float)Sim.State.PlayerX,(float)Sim.State.PlayerZ),pose,paused,close);
            actor.PrepareHumanCompany(c,Sim.HasLiveHumanCompany(c),new Vector2((float)Sim.State.PlayerX,(float)Sim.State.PlayerZ),(float)Sim.PreferredHumanDistance(c),pose,paused,close);
            var hungerFeeding=Sim.HungerFeedingStatus(c);
            var hungerTarget=hungerFeeding!=null?Sim.State.Creatures.Find(target=>target.Id==hungerFeeding.TargetId):null;
            actor.PrepareHungerFeeding(c,hungerFeeding,hungerTarget,pose,paused,close);
            actor.ObserveWaterState(c,WaterEnvironment.IsInWater(Sim.State,c),paused);
            actor.PrepareWaterAttempt(c,Sim.WaterAttemptStatus(c),Sim.State.WorldSeconds,paused,close);
            actor.PrepareStemPresentation(Sim.StemCareStatus(c.Id));
            actor.RefreshInteraction(c,c.Id==Sim.State.SelectedId,close,dt,pose,Sim.InteractionGestureTarget==c.Id?_appliedForce:_force,_gestureDirection,paused,Sim.InteractionGestureTarget==c.Id?Sim.CurrentInteractionSide:SurfaceSide.Whole,Sim.State.RoomObjects);
            RefreshFamilyPresentation(c,actor);
        }
        if(!paused)PlayRoomReactionVoice();
        if(!paused)UpdateTapTool(dt);
        UpdateOfferReturn(dt,paused);
        UpdateLighterFeedback(dt,paused);
        foreach(var obj in Sim.State.RoomObjects)
        {
            if(!_objects.TryGetValue(obj.Id,out var visual))_objects[obj.Id]=visual=MakeObject(obj);
            if(NaturalResources.IsFood(obj.Kind)&&_shownFoodPortions.GetValueOrDefault(obj.Id,-1)!=FoodVisualCount(obj))
            {visual.QueueFree();_objects[obj.Id]=visual=MakeObject(obj);}
            if(obj.Kind==RoomObjectKind.Water&&_groundCupWater.TryGetValue(obj.Id,out var water))
                SetCupFill(water,(float)NaturalResources.VisibleWaterFill(Sim.State,obj));
            bool nearby=Sim.Selected==null||Math.Pow(obj.X-Sim.Selected.X,2)+Math.Pow(obj.Z-Sim.Selected.Z,2)<4;
            visual.Visible=!obj.Stored&&!obj.HeldByHand&&obj.RegionId==Sim.State.ActiveRegionId&&(!close||nearby)&&ObjectHasVisibleGeometry(obj);visual.Position=new((float)obj.X,(float)obj.Height,(float)obj.Z);
            if(visual is RoomBallActor ball)ball.Present(obj,paused,RoomBallEnvironment.CenterHeight(Sim.State,obj));
        }
        foreach(var id in _objects.Keys.Where(id=>!Sim.State.RoomObjects.Any(o=>o.Id==id)).ToArray())
        {_objects[id].QueueFree();_objects.Remove(id);_shownFoodPortions.Remove(id);_groundCupWater.Remove(id);}
        _player.Visible=false;
        _yard.Visible=!close;
        _woodland.Visible=WoodlandVisible;
        _closeFloor.Visible=close;
        _uiClock+=dt;_autoClock+=dt;
        if(_uiClock>.1){_uiClock=0;RefreshRoomUi();}
        // Background saving must wait for the whole pointer operation, including
        // carrying a supply away from a mouth or orbiting without a body gesture.
        if(_autoClock>60&&!Sim.HasInteractionGesture&&!_mouseHeld&&_heldSupplyId==null&&!_orbiting&&!_panning&&_tapContact==null&&!_modalOpen&&!_recovered)
        {_autoClock=0;SaveRoom(0,false);}
        UpdatePointer();
        UpdateLandingPreview();
        UpdateSupplyStationPlacement();
    }

    private void UpdateRoomCamera(float dt)
    {
        bool close=Sim.State.Mode==PlayMode.CloseUp&&Sim.Selected!=null;
        var c=Sim.Selected;
        bool box=close&&c!.Interaction.Restraint==RestraintKind.Box;
        bool hanging=close&&c!.Interaction.Restraint==RestraintKind.Hanging;
        bool carried=close&&c!.Interaction.Carried;
        bool supportGesture=Sim.CurrentInteractionAction is InteractionAction.Lift or InteractionAction.AdjustHanging;
        // Direct dragging moves the oil under the cursor; following it with the camera
        // would move the view at the same time and obscure the player's drag.
        supportGesture&=!(_directGesture&&_mouseHeld&&_action==InteractionAction.Lift);
        float closeSize=box?4.6f:hanging?4.5f:carried?4.3f:3.5f;
        if(close&&(!Sim.HasInteractionGesture||supportGesture))_closeFocus=new((float)c!.X,
            box?.80f:hanging?1.1f:.62f+(float)c.Interaction.Height,(float)c.Z);
        Vector3 focus=close?_closeFocus:_yardFocus;
        var orbit=InteractionCameraOrbit.Offset(_closeYaw,_closePitch,focus.Y);
        Vector3 offset=close?new(orbit.X,orbit.Y,orbit.Z):new(0,11,16);
        float speed=Mathf.Min(1,dt*9);
        // The yard stays world-aligned, so drag directions and cue movement are consistent.
        _camera.Position=_camera.Position.Lerp(focus+offset,speed);
        if(!close||!Sim.HasInteractionGesture||supportGesture)_camera.Size=Mathf.Lerp(_camera.Size,close?InteractionCameraOrbit.Size(closeSize,_closeZoom):_zoom,speed);
        _camera.LookAt(focus,Vector3.Up);
    }

    private void UpdateInteractionHand()
    {
        bool quietCompany=_toolArmed&&_action==InteractionAction.Comfort&&!_mouseHeld&&_tapContact==null;
        if(quietCompany)_hand.Visible=false;
        if(!_windowFocused||_panning||Sim.State.Settings.Pause||Sim.State.Mode==PlayMode.CloseUp)
        {
            // Leaving the window stops live input; the existing short memory may expire naturally.
            Sim.SetInteractionHand(0,0,false);return;
        }
        Vector2 point=GetViewport().GetMousePosition();
        bool inScene=IsScenePoint(point);
        // Selecting comfort explicitly offers a quiet hand in the scene. Merely
        // leaving an ordinary cursor over the yard does not create a caregiver.
        if(quietCompany&&!_orbiting&&inScene&&TryGroundPoint(point,.5f,out var quietPoint)&&
            Math.Abs(quietPoint.X)<=7.7f&&Math.Abs(quietPoint.Z)<=7.7f)
        {
            ShowInteractionTool();_hand.GlobalBasis=Basis.Identity;
            _hand.GlobalPosition=quietPoint;_hand.Visible=true;
            Sim.SetInteractionHand(quietPoint.X,quietPoint.Z,true);return;
        }
        // Perception follows the actual tool/support, not its projection onto the floor.
        // A high held oil and a cup at its mouth can be far from that projected point.
        if((inScene&&(_mouseHeld||_tapContact!=null)||_clickOfferId!=null)&&_hand.Visible)
        {
            var hand=_hand.GlobalPosition;
            Sim.SetInteractionHand(hand.X,hand.Z,true);return;
        }
        if(inScene&&_mouseHeld&&Sim.HasInteractionGesture&&
            Sim.State.Creatures.Find(c=>c.Id==Sim.InteractionGestureTarget) is { } held&&held.Interaction.Carried)
        {
            Sim.SetInteractionHand(held.X,held.Z,true);return;
        }
        bool following=Sim.State.Creatures.Any(c=>c.RegionId==Sim.State.ActiveRegionId&&c.Interaction.FollowPlayer);
        if(following&&inScene&&TryGroundPoint(point,0,out var ground))
            Sim.SetInteractionHand(ground.X,ground.Z,true);
        else Sim.SetInteractionHand(0,0,false);
    }

    private void AcceptFeedback(InteractionFeedback result,string? id=null)
    {
        if(result.Message.Length>0)_message=result.Message;
    }
    private bool SaveRoom(int slot,bool announce=true)
    {
        var state=Sim.State;
        bool visiblePause=state.Settings.Pause;
        try
        {
            CancelGesture();
            // The menu pauses the live yard only while it is open. Persist the
            // user's pause choice, then restore the menu even if saving fails.
            if(_modalOpen)state.Settings.Pause=_pauseBeforeModal;
            _saves.Save(state,slot);
            if(announce)_message=$"已保存到槽位 {slot}。身体、饰品和经历会保留。";
            return true;
        }
        catch(Exception e){_message="保存未成功，原档保留："+e.Message;GD.PushWarning(e.Message);return false;}
        finally {state.Settings.Pause=visiblePause;}
    }
    private void LoadRoom(int slot)
    {
        try
        {
            var load=_saves.Load(slot);if(!load.State.InteractionPlaytest)throw new InvalidDataException("这个槽位不是相处小院存档。");
            CancelGesture();Sim=new WorldSimulation(load.State);
            // Closing the old menu must restore the loaded world's choice,
            // rather than borrowing the previous world's paused/running state.
            _pauseBeforeModal=Sim.State.Settings.Pause;
            _recovered=load.RecoveredBackup;_message=load.Message;BuildActors();CloseModal();RefreshRoomUi();
        }
        catch(Exception e){_message="读取未成功："+e.Message;ShowModalMessage(_message);}
    }
    private void QuitRoom()
    {
        CancelGesture();
        if(!_recovered&&!SaveRoom(0,false))
        {
            OpenModal("退出前没有保存成功");
            _modalContents.AddChild(Text(_message));
            _modalContents.AddChild(Text("当前进度仍保留在小院里。可以返回后另存槽位，也可以选择不保存退出。"));
            _modalContents.AddChild(Button("关闭面板",CloseModal));
            _modalContents.AddChild(Button("仍然退出，不保存",()=>GetTree().Quit()));
            return;
        }
        GetTree().Quit();
    }
}
