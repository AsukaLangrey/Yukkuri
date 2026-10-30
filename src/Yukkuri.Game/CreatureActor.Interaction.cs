using Godot;
using Yukkuri.Simulation;

public partial class CreatureActor
{
    private Node3D? _accessoryRoot, _looseAccessory, _roomEquipment, _boxEquipment, _frameEquipment,
        _wrapEquipment, _hangEquipment, _dirtVisual, _wetVisual, _carrySupport, _accessoryGrip;
    private MeshInstance3D? _footVisual,_hangRope;
    private readonly ActorPoseClock _interactionPoseClock=new();
    private Transform3D _accessoryRest;
    private readonly ActorPoseSmoothing _poseSmoothing=new();

    private void BuildInteractionVisuals()
    {
        _roomEquipment = new Node3D(); AddChild(_roomEquipment);
        _carrySupport=new Node3D();_roomEquipment.AddChild(_carrySupport);
        // Carry support remains a logical anchor. No human hand mesh obscures the oil.
        _boxEquipment = new Node3D(); _roomEquipment.AddChild(_boxEquipment);
        // Clearance includes the tallest hat and the widened body during pressure reactions.
        InteractionFixtureArt.RoundedBox(_boxEquipment,new(0,InteractionFixtureGeometry.BoxFloorY,0),new(InteractionFixtureGeometry.BoxWidth,InteractionFixtureGeometry.BoxFloorThickness,InteractionFixtureGeometry.BoxDepth),"9c7552");
        InteractionFixtureArt.RoundedBox(_boxEquipment,new(0,InteractionFixtureGeometry.BoxRoofY,0),new(InteractionFixtureGeometry.BoxWidth,InteractionFixtureGeometry.BoxRoofThickness,InteractionFixtureGeometry.BoxDepth),"b28c62");
        foreach(float side in new[]{-InteractionFixtureGeometry.BoxSideX,InteractionFixtureGeometry.BoxSideX})
        {
            InteractionFixtureArt.RoundedBox(_boxEquipment,new(side,InteractionFixtureGeometry.BoxWallY,InteractionFixtureGeometry.BoxSidePanelZ),new(.10f,InteractionFixtureGeometry.BoxWallHeight,InteractionFixtureGeometry.BoxSidePanelDepth),"b08b61");
            InteractionFixtureArt.RoundedBox(_boxEquipment,new(side,InteractionFixtureGeometry.BoxWallY,InteractionFixtureGeometry.BoxFrontZ),new(.10f,InteractionFixtureGeometry.BoxWallHeight,.10f),"8e684c");
        }
        InteractionFixtureArt.RoundedBox(_boxEquipment,new(0,InteractionFixtureGeometry.BoxWallY,InteractionFixtureGeometry.BoxBackZ),new(InteractionFixtureGeometry.BoxBackWidth,InteractionFixtureGeometry.BoxWallHeight,InteractionFixtureGeometry.BoxBackThickness),"ab835b");
        float barTop=InteractionFixtureGeometry.BoxWallTop-.01f,barBottom=InteractionFixtureGeometry.BoxBarBottom;
        foreach(float x in new[]{-.48f,-.24f,0f,.24f,.48f})
            SceneArt.Mesh(_boxEquipment,new CylinderMesh{TopRadius=.0175f,BottomRadius=.0175f,Height=barTop-barBottom,RadialSegments=12},new(x,(barTop+barBottom)*.5f,InteractionFixtureGeometry.BoxFrontZ),"6e706b");
        InteractionFixtureArt.RoundedBox(_boxEquipment,new(0,barBottom,InteractionFixtureGeometry.BoxFrontZ),new(InteractionFixtureGeometry.BoxBackWidth,.035f,.035f),"6e706b",.008f);
        InteractionFixtureArt.RoundedBox(_boxEquipment,new(0,InteractionFixtureGeometry.BoxWallTop-.0175f,InteractionFixtureGeometry.BoxFrontZ),new(InteractionFixtureGeometry.BoxBackWidth,.035f,.035f),"6e706b",.008f);
        InteractionFixtureArt.BoxFastenings(_boxEquipment,InteractionFixtureGeometry.BoxFrontZ,barTop,barBottom);
        _frameEquipment=new Node3D();_roomEquipment.AddChild(_frameEquipment);
        InteractionFixtureArt.RoundedBox(_frameEquipment,new(0,-.43f,0),new(InteractionFixtureGeometry.FrameWidth,.08f,InteractionFixtureGeometry.FrameDepth),"9e8170");
        foreach(float x in new[]{-.68f,.68f})
            InteractionFixtureArt.RoundedBox(_frameEquipment,new(x,-.08f,0),new(.10f,.62f,1.28f),"b9afa2");
        _wrapEquipment=new Node3D();_roomEquipment.AddChild(_wrapEquipment);
        BuildSoftCradle(_wrapEquipment);
        _hangEquipment=new Node3D();_roomEquipment.AddChild(_hangEquipment);
        InteractionFixtureArt.RoundedBox(_hangEquipment,new(.8f,.35f,-.55f),new(.08f,2.3f,.08f),"806b58");
        InteractionFixtureArt.RoundedBox(_hangEquipment,new(.36f,1.46f,-.55f),new(.96f,.08f,.08f),"806b58");
        _hangRope=SceneArt.Box(_hangEquipment,new(-.07f,.91f,-.5f),Vector3.One,"bba478");
        InteractionFixtureArt.RoundedBox(_hangEquipment,new(.8f,-.8f,-.55f),new(.9f,.10f,.7f),"806b58");
        BuildHangingHarness();
        _dirtVisual=BuildBodySurfaceMarks(ActorSurfaceMarks.Dirt,false);
        _wetVisual=BuildBodySurfaceMarks(ActorSurfaceMarks.Wet,true);
        BuildDressings();
        _footVisual=BottomOval(_visual,new(.72f,.65f),"775347",.010f);
        SceneArt.EnableSurfaceFade(_footVisual);
        if(_accessoryRoot!=null)
        {
            _accessoryRest=_accessoryRoot.Transform;
            _looseAccessory=(Node3D)_accessoryRoot.Duplicate();AddChild(_looseAccessory);
            _looseAccessory.Visible=false;
            _accessoryGrip=new Node3D{Position=_hatAccessory?new(.39f,.015f,.40f):new(0,0,.065f)};
            _looseAccessory.AddChild(_accessoryGrip);
        }
    }

    private bool _hadHopPresentation;
    public void RefreshInteraction(Creature c, bool selected, bool close, float delta,
        string pose, float force, Vector2 gestureDirection, bool paused,SurfaceSide contactSide=SurfaceSide.Whole,System.Collections.Generic.IReadOnlyList<RoomObject>? roomObjects=null)
    {
        if(_roomEquipment==null)BuildInteractionVisuals();
        var previousBodyPosition=Position+_visual.Position;
        var previousBodyScale=_visual.Scale;
        var previousBodyRotation=_visual.Quaternion;
        // Reuse facial expression assets, but room positions are authored by the simulation.
        Refresh(c,selected,false,paused?0:delta,0,true,true);
        Freeze=true;
        var life=c.Interaction;
        bool waterBody=HasWaterBodyPresentation(c);
        pose=ActorInjuryPresentation.GuardPose(c,pose);
        // Physiology is an idle posture, not a final override of contact or pain.
        // In close-up its world timer can stay active while a gesture owns the pose.
        if(pose=="idle"&&life.Physiology.Active&&c.Condition is not (PhysicalCondition.Dead or PhysicalCondition.Critical)
            &&!life.Carried&&!life.Falling&&!life.Supine&&!life.Rolling&&!life.Resting&&life.EatingRemaining<=0)
            pose=life.Physiology.Excreting==ExcretionKind.Urine?"urinate":"defecate";
        Position=new((float)c.X,.46f*BodyScale+(float)life.Height,(float)c.Z);
        bool canHop=ActorHopMotion.AllowsView(close,paused,_hadHopPresentation)&&WorldSimulation.CanSelfMove(c)&&!life.Carried&&!life.Falling&&!life.Rolling&&!life.Supine
            &&!waterBody
            &&!life.Resting&&life.Restraint==RestraintKind.None&&life.Height<=.001
            &&System.Math.Abs(life.VelocityX)+System.Math.Abs(life.VelocityZ)<.005
            &&pose is "idle" or "guard" or "stomach" or "careful-step";
        bool smoothHandoff=close||life.Physiology.Active||pose=="help-look"||life.GroundIntake&&life.EatingRemaining>0&&pose is "eat" or "sip";
        bool physicalHandoff=(life.Carried||life.Falling)&&!waterBody&&!life.Supine&&!life.Rolling&&
            life.Restraint==RestraintKind.None&&c.Condition is PhysicalCondition.Active or PhysicalCondition.Recovering;
        if(!paused&&_hadHopPresentation&&!canHop&&physicalHandoff)
        {
            // Capture the final hop's local shape and tilt, which are applied after
            // the regular smoother. Never carry its travel lag or fake hop height
            // into a real pickup/fall: simulation position and support stay authoritative.
            _poseSmoothing.BeginFrom(new(previousBodyScale.X,previousBodyScale.Y,previousBodyScale.Z),
                _poseSmoothing.Offset,new(previousBodyRotation.X,previousBodyRotation.Y,previousBodyRotation.Z,previousBodyRotation.W));
        }
        else if(!paused&&_hadHopPresentation&&!canHop&&smoothHandoff)
        {
            // Close-up or natural intake can interrupt a hop. On resume, hand the
            // complete rendered transform to smoothing, including travel lag and lift.
            var offset=previousBodyPosition-Position;
            _poseSmoothing.BeginFrom(new(previousBodyScale.X,previousBodyScale.Y,previousBodyScale.Z),
                new(offset.X,offset.Y,offset.Z),new(previousBodyRotation.X,previousBodyRotation.Y,previousBodyRotation.Z,previousBodyRotation.W));
        }
        if(!paused)_hadHopPresentation=canHop;
        _hopMotion.Advance(new(Position.X,Position.Z),delta,canHop,paused);
        _interactionPoseClock.Advance(pose,c.ReactionSerial,delta,paused);
        float age=_interactionPoseClock.Age;
        var intake=ActorIntakePresentation.SampleMouth(life.EatingKind,life.EatingRemaining,pose=="offer",life.GroundIntake);
        if(!intake.Active&&pose=="sing")intake=ActorIntakeMouthSurface.ShapeForFrame(ActorReactionMotion.SingingMouthFrame(age));
        bool intakeBlocked=c.Condition is PhysicalCondition.Dead or PhysicalCondition.Critical;
        PresentIntakeMouth(c,intake,paused?0:delta,intakeBlocked);
        float wave=Mathf.Sin(age*6);
        _visual.RotationDegrees=new(0,0,0);
        _visual.Position=Vector3.Zero;
        _visual.Scale=Vector3.One*BodyScale;
        if(c.Condition==PhysicalCondition.Dead)
        {_visual.Scale=new Vector3(1.12f,.64f,1.08f)*BodyScale;PoseTag="dead";}
        else
        {
            if(DisplayedExpression is Yukkuri.Simulation.Expression.Hurt or Yukkuri.Simulation.Expression.Frightened)
                _visual.Position+=new Vector3(Mathf.Sin(_phase*27)*.009f,0,0);
            PoseTag=pose;
            if(ActorReactionMotion.Sample(pose,age,new((float)life.ReactionDirectionX,(float)life.ReactionDirectionZ)) is { } motion)
            {
                _visual.Scale=AccessoryPoint(motion.Scale)*BodyScale;
                _visual.Position+=AccessoryPoint(motion.Offset)*BodyScale;
                _visual.RotationDegrees=AccessoryPoint(motion.RotationDegrees);
            }
            double naturalFacing=pose=="help-look"?System.Math.Atan2(life.ReactionDirectionX,life.ReactionDirectionZ):
                life.GroundIntake&&life.EatingRemaining>0?life.IntakeFacing:life.Facing;
            if(ActorNaturalPose.Sample(pose,age,naturalFacing,
                life.GroundIntake&&life.GroundIntakeSourceId!=null&&!life.Carried&&!life.Falling&&life.Height<=.25,life.EatingKind,life.EatingRemaining,life.Digestion.Severity,
                life.GroundIntakeKind,life.GroundIntakeDistance,life.GroundIntakeSourceHeight,BodyScale,new(c,roomObjects,BodyScale,_hatAccessory)) is { } natural)
            {
                _visual.Scale=AccessoryPoint(natural.Scale)*BodyScale;
                _visual.Position+=AccessoryPoint(natural.Offset)*BodyScale;
                _visual.RotationDegrees=AccessoryPoint(natural.RotationDegrees);
            }
            switch(pose)
            {
                case "urinate":case "defecate":_visual.Scale=new Vector3(1.04f,.91f,1.04f)*BodyScale;break;
                case "stroke":
                    _visual.Scale=AccessoryPoint(ActorTouchShape.DirectContact(force,false))*BodyScale;
                    _visual.RotationDegrees=new(0,0,gestureDirection.X*3);break;
                case "touch-press":_visual.Scale=AccessoryPoint(ActorTouchShape.DirectContact(force,true))*BodyScale;break;
                case "nuzzle":
                    float nuzzleSide=contactSide==SurfaceSide.Left?-1:contactSide==SurfaceSide.Right?1:gestureDirection.X<0?-1:1;
                    float returnRub=Mathf.Sin(age*4)*.012f;
                    _visual.Position=new(nuzzleSide*.025f+gestureDirection.X*.03f+returnRub,0,.025f);
                    _visual.RotationDegrees=new(0,gestureDirection.X*5,nuzzleSide*3+returnRub*100);break;
                case "nuzzle-ready":
                    float readySide=contactSide==SurfaceSide.Left?-1:contactSide==SurfaceSide.Right?1:gestureDirection.X<0?-1:1;
                    _visual.Position=new(readySide*.025f,0,.018f);
                    _visual.RotationDegrees=new(0,readySide*3,readySide*4);break;
                case "invite":_visual.RotationDegrees=new(0,0,8);break;
                case "carry":_visual.Scale=new Vector3(1.04f,.97f,1.04f)*BodyScale;break;
                case "rock":_visual.RotationDegrees=new(0,0,gestureDirection.X*10);break;
                case "wipe":_visual.RotationDegrees=new(0,0,gestureDirection.X*4);break;
                case "sing":_visual.Scale=new Vector3(1,1+Mathf.Abs(wave)*.035f,1)*BodyScale;break;
                case "stretch":_visual.Scale=new Vector3(1-Mathf.Abs(wave)*.045f,1+Mathf.Abs(wave)*.13f,1)*BodyScale;break;
                case "proud":_visual.RotationDegrees=new(-7,0,3);break;
                case "learn":_visual.RotationDegrees=new(0,0,Mathf.Sin(age*2)*8);break;
                case "withdraw":_visual.RotationDegrees=new(-5,-15,0);_visual.Scale=new Vector3(1.02f,.92f,1)*BodyScale;break;
                case "guard":
                    _visual.RotationDegrees=new(-5,ActorReactionMotion.GuardYaw(life.CheekSensitivityAt(SurfaceSide.Left),life.CheekSensitivityAt(SurfaceSide.Right)),0);
                    _visual.Scale=new Vector3(1.02f,.92f,1)*BodyScale;break;
                case "pressure":_visual.Scale=AccessoryPoint(ActorTouchShape.Press(force))*BodyScale;break;
                case "squeeze":_visual.Scale=AccessoryPoint(ActorTouchShape.Squeeze(force))*BodyScale;break;
                case "reach":_visual.Scale=new Vector3(.96f,1.09f,1)*BodyScale;break;
                case "foot-hurt":case "failed-step":_visual.Position=new(Mathf.Sin(age*9)*.02f,Mathf.Abs(Mathf.Sin(age*4))*.012f,0);break;
            }
            if(life.Supine){_visual.RotationDegrees=new(InteractionFixtureGeometry.SupineAngle,0,0);_visual.Position=new(0,.04f,InteractionFixtureGeometry.SupineOffsetZ);}
            if(life.Rolling)
            {
                var roll=life.RollOrientation;
                _visual.Quaternion=new(roll.X,roll.Y,roll.Z,roll.W);
                _visual.Position=Vector3.Zero;
            }
            if(life.Carried&&pose=="idle")PoseTag="carry";
            if(life.Falling){_visual.RotationDegrees=new(0,0,Mathf.Sin(_phase*7)*15);PoseTag="fall";}
            if(!life.Supine&&!life.Rolling&&!life.Falling&&ActorNaturalPose.AllowsAccessoryAttention(pose,life.EatingRemaining)&&
                life.Accessory.Place is AccessoryPlace.Hand or AccessoryPlace.Hung or AccessoryPlace.Ground)
            {
                // A head-bodied oil turns its whole face to keep its treasured item in sight.
                float dx=(float)(life.Accessory.X-c.X),dz=(float)(life.Accessory.Z-c.Z);
                var rotation=_visual.Rotation;
                rotation.Y=Mathf.Clamp(Mathf.Atan2(dx,dz),-.42f,.42f);_visual.Rotation=rotation;
            }
        }
        if(canHop)
        {
            var rotation=_visual.Rotation;rotation.Y=_hopMotion.Heading;_visual.Rotation=rotation;
        }
        // Refresh's critical shape was reset above. Preserve it without discarding
        // the supported/rolling/falling orientation and offset already calculated.
        if(c.Condition==PhysicalCondition.Critical)
        {_visual.Scale=new Vector3(1.13f,.63f,1.07f)*BodyScale;PoseTag="critical";}
        ApplyHumanCompanyBody(c,pose,paused?0:delta);
        ApplyWaterConcernBody(c,pose,paused?0:delta);
        ApplyPregnancyBodyPose(c,pose,paused?0:delta);
        ApplyHeatBodyPose(c,pose);
        ApplyWaterBodyPose(c,pose,paused?0:delta);
        Vector3 targetScale=_visual.Scale,targetOffset=_visual.Position;
        Quaternion targetRotation=_visual.Quaternion;
        _poseSmoothing.Advance(new(targetScale.X,targetScale.Y,targetScale.Z),new(targetOffset.X,targetOffset.Y,targetOffset.Z),
            new(targetRotation.X,targetRotation.Y,targetRotation.Z,targetRotation.W),delta,paused,life.GroundIntake&&life.EatingRemaining>0);
        _visual.Scale=AccessoryPoint(_poseSmoothing.Scale);_visual.Position=AccessoryPoint(_poseSmoothing.Offset);
        var smoothed=_poseSmoothing.Rotation;_visual.Quaternion=new(smoothed.X,smoothed.Y,smoothed.Z,smoothed.W);
        if(canHop&&!_waterConcernFacing&&!_companyFacing)
        {
            // Heading has already turned at a bounded speed while planted. A second
            // rotation filter would leave the face trailing behind the flight direction.
            var rotation=_visual.Rotation;rotation.Y=_hopMotion.Heading;_visual.Rotation=rotation;
            var heading=_visual.Quaternion;_poseSmoothing.Face(new(heading.X,heading.Y,heading.Z,heading.W));
            if(_hopMotion.Turning&&pose=="idle")PoseTag="turn";
        }
        else if(!paused)_hopMotion.Face(_visual.Rotation.Y);
        // The hop already has authored timing. Filtering its arc again erases takeoff and impact.
        if(_hopMotion.Active)
        {
            var hop=ActorInjuryPresentation.AdaptHop(c,_hopMotion.Frame);
            _visual.Scale*=AccessoryPoint(hop.Scale);
            _visual.Position+=new Vector3(_hopMotion.Position.X-Position.X,hop.Lift*BodyScale,_hopMotion.Position.Y-Position.Z);
            _visual.Rotation+=new Vector3(Mathf.DegToRad(hop.Pitch),0,0);
            if(pose is "idle" or "careful-step")PoseTag=ActorInjuryPresentation.StepStrength(c)<.8f?"careful-step":"hop";
        }
        else if(canHop)
            _visual.Position+=new Vector3(_hopMotion.Position.X-Position.X,0,_hopMotion.Position.Y-Position.Z);
        if(_accessoryRoot!=null)
        {
            _accessoryRoot.Visible=life.HasAccessory;
            // The body deforms under a hand; the whole hat should not stretch into the box roof.
            var accessory=_accessoryRest;
            Basis tilt=life.Accessory.Integrity<75?Basis.FromEuler(new(0,0,Mathf.DegToRad(12))):Basis.Identity;
            accessory.Basis=Basis.FromScale(new(BodyScale/_visual.Scale.X,BodyScale/_visual.Scale.Y,BodyScale/_visual.Scale.Z))*tilt*_accessoryRest.Basis;
            if(_hatAccessory)
            {
                var deformation=_visual.Scale/BodyScale;
                float lift=ActorAccessoryPlacement.HatClearance(new(deformation.X,deformation.Y,deformation.Z),life.Accessory.Integrity<75?12:0);
                accessory.Origin+=Vector3.Up*(lift/deformation.Y);
            }
            _accessoryRoot.Transform=accessory;
        }
        RefreshDressings(life);
        if(!waterBody&&!life.Carried&&!life.Falling&&life.Restraint!=RestraintKind.Hanging)
            KeepBodyAboveSupport(life.Restraint is RestraintKind.Box or RestraintKind.Frame?
                (.46f+InteractionFixtureGeometry.BoxFloorY+InteractionFixtureGeometry.BoxFloorThickness*.5f)*BodyScale:0);
        if(life.Restraint==RestraintKind.Wrap)
        {
            var scale=_visual.Scale;var rotation=_visual.Quaternion;var offset=_visual.Position;
            float bottom=ActorCradleSurface.LowestY(new(scale.X,scale.Y,scale.Z),new(rotation.X,rotation.Y,rotation.Z,rotation.W),new(offset.X,offset.Y,offset.Z));
            _visual.Position+=Vector3.Up*Mathf.Max(0,.003f-Position.Y-bottom);
        }
        if(!waterBody&&!life.Carried&&!life.Falling&&life.Restraint is RestraintKind.None or RestraintKind.Wrap&&roomObjects!=null)
        {
            var scale=_visual.Scale;var rotation=_visual.Quaternion;
            for(int cushionIndex=0;cushionIndex<roomObjects.Count;cushionIndex++)
            {
                var cushion=roomObjects[cushionIndex];
                if(cushion.Stored||cushion.Kind!=RoomObjectKind.Cushion||cushion.RegionId!=c.RegionId||System.Math.Abs(cushion.X-c.X)>1.8||System.Math.Abs(cushion.Z-c.Z)>1.7)continue;
                var origin=Position+_visual.Position;
                var bed=new System.Numerics.Vector3((float)cushion.X,(float)cushion.Height,(float)cushion.Z);
                var body=new System.Numerics.Vector3(origin.X,origin.Y,origin.Z);var bodyScale=new System.Numerics.Vector3(scale.X,scale.Y,scale.Z);
                var bodyRotation=new System.Numerics.Quaternion(rotation.X,rotation.Y,rotation.Z,rotation.W);
                float lift=ActorNaturalSupport.CushionLift(life,_hatAccessory,bed,body,bodyScale,BodyScale,bodyRotation);
                _visual.Position+=Vector3.Up*lift;
            }
        }
        _carrySupport!.Visible=life.Carried;
        _carrySupport.Position=_visual.Position/BodyScale;
        if(life.Carried)
        {
            var scale=_visual.Scale;var q=_visual.Quaternion;var offset=_visual.Position;
            float bottom=ActorBodyBounds.LowestSkinY(new(scale.X,scale.Y,scale.Z),new(q.X,q.Y,q.Z,q.W),new(offset.X,offset.Y,offset.Z));
            if(life.HasDressing(BodyPart.Bottom))bottom-=.024f*BodyScale;
            _carrySupport.Position=new(offset.X/BodyScale,bottom/BodyScale+.462f,offset.Z/BodyScale);
        }
        _boxEquipment!.Visible=life.Restraint==RestraintKind.Box;
        _frameEquipment!.Visible=life.Restraint==RestraintKind.Frame;
        _wrapEquipment!.Visible=life.Restraint==RestraintKind.Wrap;
        _wrapEquipment.Rotation=_visual.Rotation;
        _wrapEquipment.Position=_visual.Position/BodyScale;
        _wrapEquipment.Scale=_visual.Scale/BodyScale;
        _hangEquipment!.Visible=life.Restraint==RestraintKind.Hanging;
        // The support stands on the yard, not on the moving oil's elevated origin.
        _hangEquipment.Position=new(0,-(float)life.Height/BodyScale+.39f,0);
        _roomEquipment!.Scale=Vector3.One*BodyScale;
        RefreshHangingHarness(life);
        RefreshBodySurfaceMarks(_dirtVisual!,life,false);
        RefreshBodySurfaceMarks(_wetVisual!,life,true);
        _footVisual!.Visible=life.FootIntegrity<80&&life.Injuries.At(BodyPart.Bottom)==null&&!life.HasDressing(BodyPart.Bottom);
        SceneArt.SetSurfaceOpacity(_footVisual,Mathf.Clamp((float)(100-life.FootIntegrity)/70,.4f,1));
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
        PresentPregnancyPlant(c,paused?0:delta);
        PresentWaterSurface(c,paused?0:delta);
        PresentWaterTransitions(c,paused?0:delta);
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
