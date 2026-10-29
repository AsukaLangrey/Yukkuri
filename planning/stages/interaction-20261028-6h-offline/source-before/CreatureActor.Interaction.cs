using Godot;
using Yukkuri.Simulation;

public partial class CreatureActor
{
    private Node3D? _accessoryRoot, _looseAccessory, _roomEquipment, _boxEquipment, _frameEquipment,
        _wrapEquipment, _hangEquipment, _dirtVisual, _wetVisual, _carrySupport, _accessoryGrip;
    private MeshInstance3D? _footVisual,_hangRope;
    private float _interactionPoseAge;
    private string _interactionPose = "idle";
    private Transform3D _accessoryRest;
    private bool _hasPresentedPose;
    private Vector3 _presentedScale,_presentedOffset;
    private Quaternion _presentedRotation=Quaternion.Identity;

    private void BuildInteractionVisuals()
    {
        _roomEquipment = new Node3D(); AddChild(_roomEquipment);
        _carrySupport=new Node3D();_roomEquipment.AddChild(_carrySupport);
        SceneArt.Ball(_carrySupport,new(0,-.54f,.04f),new(.64f,.15f,.49f),"e5b99e");
        for(int finger=0;finger<4;finger++)
        {
            float x=(finger-1.5f)*.14f;
            SceneArt.Ball(_carrySupport,new(x,-.51f,-.18f),new(.125f,.105f,.32f),"efc6ab");
        }
        SceneArt.Ball(_carrySupport,new(.32f,-.46f,.08f),new(.19f,.14f,.24f),"efc6ab");
        SceneArt.Ball(_carrySupport,new(0,-.59f,.32f),new(.35f,.15f,.28f),"e5b99e");
        _boxEquipment = new Node3D(); _roomEquipment.AddChild(_boxEquipment);
        // Clearance includes the tallest hat and the widened body during pressure reactions.
        SceneArt.Box(_boxEquipment, new(0,-.43f,0),new(1.8f,.08f,1.55f),"9c7552");
        SceneArt.Box(_boxEquipment,new(0,1.26f,0),new(1.8f,.10f,1.55f),"b28c62");
        foreach(float side in new[]{-.85f,.85f})
        {
            SceneArt.Box(_boxEquipment,new(side,.4f,0),new(.10f,1.62f,1.55f),"b08b61");
            SceneArt.Box(_boxEquipment,new(side,.4f,.73f),new(.10f,1.62f,.10f),"8e684c");
        }
        SceneArt.Box(_boxEquipment,new(0,.4f,-.73f),new(1.7f,1.62f,.08f),"ab835b");
        foreach(float x in new[]{-.48f,-.24f,0f,.24f,.48f})
            SceneArt.Box(_boxEquipment,new(x,.54f,.73f),new(.035f,1.32f,.035f),"6e706b");
        _frameEquipment=new Node3D();_roomEquipment.AddChild(_frameEquipment);
        SceneArt.Box(_frameEquipment,new(0,-.43f,0),new(1.52f,.08f,1.36f),"9e8170");
        foreach(float x in new[]{-.68f,.68f})
            SceneArt.Box(_frameEquipment,new(x,-.08f,0),new(.10f,.62f,1.28f),"b9afa2");
        _wrapEquipment=new Node3D();_roomEquipment.AddChild(_wrapEquipment);
        BuildSoftCradle(_wrapEquipment);
        _hangEquipment=new Node3D();_roomEquipment.AddChild(_hangEquipment);
        SceneArt.Box(_hangEquipment,new(.8f,.35f,-.55f),new(.08f,2.3f,.08f),"806b58");
        SceneArt.Box(_hangEquipment,new(.36f,1.46f,-.55f),new(.96f,.08f,.08f),"806b58");
        _hangRope=SceneArt.Box(_hangEquipment,new(-.07f,.91f,-.5f),Vector3.One,"bba478");
        SceneArt.Box(_hangEquipment,new(.8f,-.8f,-.55f),new(.9f,.10f,.7f),"806b58");
        _dirtVisual=new Node3D();_visual.AddChild(_dirtVisual);
        foreach(var item in new[]{(new Vector2(-.38f,-.18f),BodyPart.Cheek),(new Vector2(.40f,.1f),BodyPart.Side),(new Vector2(-.1f,-.30f),BodyPart.Mouth)})
        {var spot=FaceOval(_dirtVisual,item.Item1,new(.12f,.055f),"baa17c",.008f);spot.SetMeta("surface_part",(int)item.Item2);}
        var bottomSpot=BottomOval(_dirtVisual,new(.3f,.22f),"baa17c");bottomSpot.SetMeta("surface_part",(int)BodyPart.Bottom);
        foreach(var mark in _dirtVisual.GetChildren())if(mark is MeshInstance3D patch)SceneArt.EnableSurfaceFade(patch);
        _wetVisual=new Node3D();_visual.AddChild(_wetVisual);
        foreach(var p in new[]{new Vector2(-.4f,.0f),new(.36f,-.16f)})
            FaceOval(_wetVisual,p,new(.038f,.085f),"92bfc3",.015f);
        _footVisual=BottomOval(_visual,new(.72f,.65f),"775347",.010f);
        SceneArt.EnableSurfaceFade(_footVisual);
        if(_accessoryRoot!=null)
        {
            _accessoryRest=_accessoryRoot.Transform;
            _looseAccessory=(Node3D)_accessoryRoot.Duplicate();AddChild(_looseAccessory);
            _looseAccessory.Visible=false;
            _accessoryGrip=new Node3D{Position=_hatAccessory?new(.39f,.015f,.40f):new(0,0,.065f)};
            _looseAccessory.AddChild(_accessoryGrip);
            SceneArt.Ball(_accessoryGrip,new(.075f,-.025f,.055f),new(.11f,.16f,.14f),"efc6ab");
            SceneArt.Ball(_accessoryGrip,new(-.075f,-.025f,-.025f),new(.11f,.16f,.14f),"e5b99e");
            SceneArt.Ball(_accessoryGrip,new(0,-.16f,.035f),new(.23f,.18f,.20f),"e5b99e");
        }
    }

    public void RefreshInteraction(Creature c, bool selected, bool close, float delta,
        string pose, float force, Vector2 gestureDirection, bool paused)
    {
        if(_roomEquipment==null)BuildInteractionVisuals();
        // Reuse facial expression assets, but room positions are authored by the simulation.
        Refresh(c,selected,false,paused?0:delta,0,true);
        Freeze=true;
        var life=c.Interaction;
        Position=new((float)c.X,.46f*BodyScale+(float)life.Height,(float)c.Z);
        if(pose!=_interactionPose){_interactionPose=pose;_interactionPoseAge=0;}
        if(!paused)_interactionPoseAge+=delta;
        float age=_interactionPoseAge;
        float wave=Mathf.Sin(age*6);
        _visual.RotationDegrees=new(0,0,0);
        _visual.Position=Vector3.Zero;
        _visual.Scale=Vector3.One*BodyScale;
        if(c.Condition==PhysicalCondition.Dead)
        {_visual.Scale=new Vector3(1.12f,.64f,1.08f)*BodyScale;PoseTag="dead";}
        else
        {
            bool walk=WorldSimulation.CanSelfMove(c)&&(c.Activity.Contains("走")||c.Activity.Contains("跟")||c.Activity.Contains("找"));
            if(walk&&!life.Carried&&life.Restraint==RestraintKind.None)
                _visual.Position=new(0,Mathf.Abs(Mathf.Sin(_phase*7))*.045f*BodyScale,0);
            if(c.Expression is Yukkuri.Simulation.Expression.Hurt or Yukkuri.Simulation.Expression.Frightened)
                _visual.Position+=new Vector3(Mathf.Sin(_phase*27)*.009f,0,0);
            PoseTag=pose;
            switch(pose)
            {
                case "stroke":
                    _visual.Scale=new Vector3(1.03f,.95f,1)*BodyScale;
                    _visual.RotationDegrees=new(0,0,gestureDirection.X*3);break;
                case "nuzzle":
                    float nuzzleSide=gestureDirection.X<0?-1:1;
                    float returnRub=Mathf.Sin(age*4)*.012f;
                    _visual.Position=new(gestureDirection.X*.055f+returnRub,0,.025f);
                    _visual.RotationDegrees=new(0,gestureDirection.X*5,nuzzleSide*3+returnRub*100);break;
                case "nuzzle-ready":
                    _visual.Position=new(gestureDirection.X*.025f,0,.018f);
                    _visual.RotationDegrees=new(0,gestureDirection.X*3,gestureDirection.X<0?-4:4);break;
                case "invite":_visual.RotationDegrees=new(0,0,8);break;
                case "carry":_visual.Scale=new Vector3(1.04f,.97f,1.04f)*BodyScale;break;
                case "rock":_visual.RotationDegrees=new(0,0,gestureDirection.X*10);break;
                case "eat":
                    _visual.Scale=new Vector3(1+Mathf.Abs(wave)*.025f,1-Mathf.Abs(wave)*.02f,1)*BodyScale;break;
                case "wipe":_visual.RotationDegrees=new(0,0,gestureDirection.X*4);break;
                case "sing":_visual.Scale=new Vector3(1,1+Mathf.Abs(wave)*.035f,1)*BodyScale;break;
                case "stretch":_visual.Scale=new Vector3(1-Mathf.Abs(wave)*.045f,1+Mathf.Abs(wave)*.13f,1)*BodyScale;break;
                case "proud":_visual.RotationDegrees=new(-7,0,3);break;
                case "learn":_visual.RotationDegrees=new(0,0,Mathf.Sin(age*2)*8);break;
                case "withdraw":case "guard":_visual.RotationDegrees=new(-5,-15,0);_visual.Scale=new Vector3(1.02f,.92f,1)*BodyScale;break;
                case "flick":_visual.RotationDegrees=new(0,0,Mathf.Sin(age*16)*8*Mathf.Exp(-age*3));break;
                case "slap":case "push":_visual.RotationDegrees=new(0,0,Mathf.Sin(age*12)*16*Mathf.Exp(-age*2));break;
                case "pressure":_visual.Scale=AccessoryPoint(ActorTouchShape.Press(force))*BodyScale;break;
                case "squeeze":_visual.Scale=AccessoryPoint(ActorTouchShape.Squeeze(force))*BodyScale;break;
                case "roll":_visual.RotationDegrees=new(0,0,age*180*(gestureDirection.X>=0?1:-1));break;
                case "reach":_visual.Scale=new Vector3(.96f,1.09f,1)*BodyScale;break;
                case "foot-hurt":case "failed-step":_visual.Position=new(Mathf.Sin(age*9)*.02f,Mathf.Abs(Mathf.Sin(age*4))*.012f,0);break;
                case "rest":_visual.Scale=new Vector3(1.07f,.88f,1.04f)*BodyScale;break;
            }
            if(life.Supine){_visual.RotationDegrees=new(-105,0,0);_visual.Position=new(0,.04f,-.08f);}
            if(life.Carried&&pose=="idle")PoseTag="carry";
            if(life.Falling){_visual.RotationDegrees=new(0,0,Mathf.Sin(_phase*7)*15);PoseTag="fall";}
            if(!life.Supine&&!life.Falling&&life.Accessory.Place is AccessoryPlace.Hand or AccessoryPlace.Hung or AccessoryPlace.Ground)
            {
                // A head-bodied oil turns its whole face to keep its treasured item in sight.
                float dx=(float)(life.Accessory.X-c.X),dz=(float)(life.Accessory.Z-c.Z);
                var rotation=_visual.Rotation;
                rotation.Y=Mathf.Clamp(Mathf.Atan2(dx,dz),-.42f,.42f);_visual.Rotation=rotation;
            }
        }
        Vector3 targetScale=_visual.Scale,targetOffset=_visual.Position;
        Quaternion targetRotation=_visual.Quaternion;
        float blend=paused?0:1-Mathf.Exp(-delta*14);
        if(!_hasPresentedPose){_presentedScale=targetScale;_presentedOffset=targetOffset;_presentedRotation=targetRotation;_hasPresentedPose=true;}
        _presentedScale=_presentedScale.Lerp(targetScale,blend);
        _presentedOffset=_presentedOffset.Lerp(targetOffset,blend);
        _presentedRotation=_presentedRotation.Slerp(targetRotation,blend).Normalized();
        _visual.Scale=_presentedScale;_visual.Position=_presentedOffset;_visual.Quaternion=_presentedRotation;
        if(!life.Carried&&!life.Falling&&life.Restraint!=RestraintKind.Hanging)
            KeepBodyAboveSupport(life.Restraint is RestraintKind.Box or RestraintKind.Frame?.08f:0);
        _carrySupport!.Visible=life.Carried;
        _carrySupport.Position=_visual.Position/BodyScale;
        _boxEquipment!.Visible=life.Restraint==RestraintKind.Box;
        _frameEquipment!.Visible=life.Restraint==RestraintKind.Frame;
        _wrapEquipment!.Visible=life.Restraint==RestraintKind.Wrap;
        _wrapEquipment.Rotation=_visual.Rotation;
        _wrapEquipment.Position=_visual.Position/BodyScale;
        _wrapEquipment.Scale=_visual.Scale/BodyScale;
        _hangEquipment!.Visible=life.Restraint==RestraintKind.Hanging;
        // The support stands on the yard, not on the moving oil's elevated origin.
        _hangEquipment.Position=new(0,-(float)life.Height/BodyScale+.39f,0);
        float anchor=(float)life.Height/BodyScale-.20f;
        _hangRope!.Position=new(-.07f,(1.46f+anchor)*.5f,-.5f);
        _hangRope.Scale=new(.025f,Mathf.Max(.04f,1.46f-anchor),.025f);
        _roomEquipment!.Scale=Vector3.One*BodyScale;
        _dirtVisual!.Visible=life.Dirt>1;
        foreach(var child in _dirtVisual.GetChildren())if(child is MeshInstance3D spot)
        {
            float dirt=(float)life.DirtAt((BodyPart)(int)spot.GetMeta("surface_part"));
            spot.Visible=dirt>1;SceneArt.SetSurfaceOpacity(spot,dirt/35);
        }
        _wetVisual!.Visible=life.Wetness>5;
        _footVisual!.Visible=life.FootIntegrity<80;
        SceneArt.SetSurfaceOpacity(_footVisual,Mathf.Clamp((float)(100-life.FootIntegrity)/70,.4f,1));
        if(_accessoryRoot!=null)
        {
            _accessoryRoot.Visible=life.HasAccessory;
            _accessoryRoot.Transform=_accessoryRest;
            if(life.Accessory.Integrity<75)_accessoryRoot.RotationDegrees+=new Vector3(0,0,12);
        }
        RefreshAccessoryWear(_accessoryRoot,life.Accessory);
        RefreshAccessoryWear(_looseAccessory,life.Accessory);
        if(_looseAccessory!=null)
        {
            var a=life.Accessory;
            _looseAccessory.Visible=a.Place is AccessoryPlace.Ground or AccessoryPlace.Hand or AccessoryPlace.Hung;
            if(_accessoryGrip!=null)_accessoryGrip.Visible=a.Place==AccessoryPlace.Hand;
            _looseAccessory.GlobalPosition=new((float)a.X,.18f+(float)a.Height,(float)a.Z);
            _looseAccessory.RotationDegrees=new(0,0,a.Place==AccessoryPlace.Ground?35:0);
            _looseAccessory.Scale=Vector3.One*BodyScale*(a.Integrity<=0?.35f:1);
            if(a.Place==AccessoryPlace.Ground)RestAccessoryOnGround(_looseAccessory);
        }
        if(_name!=null){_name.Visible=!close;_name.Position=new(0,1.28f*BodyScale,0);}
        if(_speech!=null)_speech.Visible=!close&&c.ReactionRemaining>0&&c.Speech.Length>0;
        _ring.Visible=selected&&!close;
    }

    public Vector3 ContactPoint(BodyPart part, float side = 1)
    {
        if(part==BodyPart.Accessory)
        {
            Node3D? accessory=_looseAccessory?.Visible==true?_looseAccessory:_accessoryRoot;
            if(accessory!=null)return accessory.ToGlobal(_hatAccessory?new Vector3(0,.14f,.39f):new Vector3(0,0,.10f));
        }
        return _visual.ToGlobal(part switch
        {
            BodyPart.Head => new Vector3(0,.33f,.49f),
            BodyPart.Cheek => new Vector3(side*.39f,-.08f,ActorFaceSurface.Height(side*.39f,-.08f,.012f)),
            BodyPart.Mouth => new Vector3(0,-.16f,ActorFaceSurface.Height(0,-.16f,.015f)),
            BodyPart.Bottom => new Vector3(0,ActorFaceSurface.Bottom(0,.15f,.012f),.15f),
            BodyPart.Accessory => new Vector3(0,.66f,.12f),
            _ => new Vector3(side*.48f,-.04f,.12f)
        });
    }
    public Vector3 ContactNormal(BodyPart part,Vector3 point)
    {
        if(part==BodyPart.Accessory)
        {
            var accessory=_looseAccessory?.Visible==true?_looseAccessory:_accessoryRoot;
            if(accessory!=null)return (accessory.GlobalBasis*Vector3.Back).Normalized();
        }
        Vector3 local=_visual.ToLocal(point),normal;
        if(part==BodyPart.Head)normal=new Vector3(0,.7f,.7f);
        else if(part==BodyPart.Accessory)normal=Vector3.Back;
        else normal=new Vector3(local.X/(ActorFaceSurface.RadiusX*ActorFaceSurface.RadiusX),
            (local.Y-ActorFaceSurface.CenterY)/(ActorFaceSurface.RadiusY*ActorFaceSurface.RadiusY),
            (local.Z-ActorFaceSurface.CenterZ)/(ActorFaceSurface.RadiusZ*ActorFaceSurface.RadiusZ));
        if(normal.LengthSquared()<.001f)normal=Vector3.Back;
        return (_visual.GlobalTransform.Basis.Inverse().Transposed()*normal).Normalized();
    }
}
