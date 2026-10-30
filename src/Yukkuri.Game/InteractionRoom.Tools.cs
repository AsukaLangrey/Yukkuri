using Godot;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private InteractionAction? _shownTool;
    private MeshInstance3D? _landingMarker,_landingStem;
    private MeshInstance3D? _cupWater;
    private Node3D? _toolFood;
    private MeshInstance3D? _lighterFlame,_lighterCore;
    private float _lighterClock;
    private static void SetCupFill(MeshInstance3D water,float fill)
    {
        fill=Mathf.Clamp(fill,0,1);
        float height=InteractionToolSurface.SipWaterHeight(1-fill),radius=InteractionToolSurface.SipWaterRadius(height);
        water.Visible=fill>0;water.Position=new(0,height,0);
        water.Scale=new(radius/InteractionToolSurface.WaterRadius,1,radius/InteractionToolSurface.WaterRadius);
    }
    private void UpdateLandingPreview()
    {
        if(_landingMarker==null)
        {
            _landingMarker=SceneArt.Mesh(this,new TorusMesh{InnerRadius=.57f,OuterRadius=.60f,Rings=64,RingSegments=8},Vector3.Zero,"739683");
            _landingStem=SceneArt.Mesh(this,new CylinderMesh{TopRadius=.008f,BottomRadius=.008f,Height=1,RadialSegments=8},Vector3.Zero,"b6b69b");
            _landingMarker.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
            _landingStem.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
        }
        var c=Sim.Selected;
        bool visible=c?.Interaction.Carried==true&&!_modalOpen&&_windowFocused;
        _landingMarker.Visible=visible;_landingStem!.Visible=visible;
        if(!visible)return;
        Vector2 mouse=GetViewport().GetMousePosition();
        bool palmPlacement=CanPlaceWithIdlePalm()&&!_panning&&
            (!_orbiting||!_orbitDragged&&_orbitClickAction==InteractionAction.PutDown)&&
            IsScenePoint(mouse)&&ContactAt(mouse)==null&&ObjectAt(mouse)==null;
        bool placing=!Sim.State.Settings.Pause&&IsScenePoint(mouse)&&
            (_toolArmed&&_action==InteractionAction.PutDown||palmPlacement);
        Vector3 point=placing?GroundPoint(mouse):new Vector3((float)c!.X,0,(float)c.Z);
        if(placing&&!TryGroundPoint(mouse,0,out point)){_landingMarker.Visible=false;_landingStem.Visible=false;return;}
        bool clear=Sim.CanPlaceInteractionAt(c!.Id,point.X,point.Z);
        bool water=WoodlandLayout.HasRiver(Sim.State,c.RegionId)&&point.X>WoodlandLayout.RiverEdge(point.Z);
        _landingMarker.Position=point+Vector3.Up*.028f;
        _landingMarker.Scale=Vector3.One*(float)(WorldSimulation.InteractionFootprint(c)/.60);
        _landingMarker.MaterialOverride=SceneArt.Mat(!clear?"b66f56":water?"5896a3":"739683");
        _landingStem.Visible=!placing;
        float height=(float)c.Interaction.Height;
        _landingStem.Position=point+Vector3.Up*(height*.5f);
        _landingStem.Scale=new(1,Mathf.Max(.01f,height),1);
    }
    private void ShowInteractionTool()
    {
        if(_shownTool==_action)return;_shownTool=_action;
        _cupWater=null;_toolFood=null;_lighterFlame=null;_lighterCore=null;_lighterClock=0;
        foreach(var child in _hand.GetChildren()){_hand.RemoveChild(child);child.QueueFree();}
        _hand.Rotation=Vector3.Zero;
        switch(_action)
        {
            case InteractionAction.Clean:case InteractionAction.Dry:case InteractionAction.CleanAccessory:
                InteractionToolArt.Towel(_hand,_action==InteractionAction.Dry);break;
            case InteractionAction.FeedMeal:
                _toolFood=InteractionFoodArt.Spoon(_hand);break;
            case InteractionAction.FeedSweet:case InteractionAction.Tease:
                _toolFood=new Node3D();_hand.AddChild(_toolFood);
                InteractionFoodArt.Sweet(_toolFood,InteractionFoodSurface.SweetHandScale);break;
            case InteractionAction.Drink:
                _cupWater=InteractionToolArt.Cup(_hand);break;
            case InteractionAction.Care:case InteractionAction.SupportStem:case InteractionAction.UnbottleStem:
                InteractionToolArt.CarePad(_hand);break;
            case InteractionAction.CutStem:
                BuildStemScissors(_hand);break;
            case InteractionAction.BottleStem:
                BuildEmptyStemVessel(_hand);break;
            case InteractionAction.ClearStem:
                SceneArt.Box(_hand,new(0,-.006f,0),new(.14f,.012f,.12f),"c7bea0");break;
            case InteractionAction.RepairAccessory:
                InteractionToolArt.RepairNeedle(_hand);break;
            case InteractionAction.Burn:
                BuildLighterTool(_hand);break;
            case InteractionAction.BurnFeet:
                InteractionFixtureArt.RoundedBox(_hand,new(0,-.035f,0),new(.24f,.06f,.18f),"777569",.012f);
                var hotPlate=InteractionFixtureArt.RoundedBox(_hand,new(0,.003f,0),new(.20f,.018f,.14f),"dc9c62",.004f);
                var hotMaterial=(StandardMaterial3D)hotPlate.MaterialOverride.Duplicate();hotMaterial.EmissionEnabled=true;
                hotMaterial.Emission=new Color("d38c52");hotMaterial.EmissionEnergyMultiplier=.25f;hotPlate.MaterialOverride=hotMaterial;
                InteractionFixtureArt.RoundedBox(_hand,new(0,-.04f,.15f),new(.065f,.06f,.20f),"927c64",.014f);break;
            default:
                // Bare-hand actions are conveyed by the cursor and the oil's response.
                // Keep this transform as the contact/perception anchor, without a human model.
                break;
        }
        SceneArt.SetRenderLayer(_hand,SceneArt.InteractionLayer);
    }

    private void BuildLighterTool(Node3D root)
    {
        // Local +Y is the flame tip; PlaceContactTool aligns it with the contacted surface.
        InteractionFixtureArt.RoundedBox(root,new(0,-.20f,0),new(.115f,.24f,.075f),"728b84",.012f);
        InteractionFixtureArt.RoundedBox(root,new(0,-.065f,0),new(.12f,.055f,.08f),"a8aaa1",.007f);
        var flameMesh=BuildLighterFlameMesh();
        var flame=SceneArt.Mesh(root,flameMesh,new(0,-.021f,0),"f5be67");
        flame.Scale=new(.027f,.065f,.027f);
        var flameMat=(StandardMaterial3D)flame.MaterialOverride.Duplicate();
        flameMat.EmissionEnabled=true;flameMat.Emission=new Color("ffb650");flameMat.EmissionEnergyMultiplier=.65f;
        flame.MaterialOverride=flameMat;flame.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
        _lighterFlame=flame;
        _lighterCore=SceneArt.Mesh(root,flameMesh,new(0,-.045f,0),"e9e5b1");
        _lighterCore.Scale=new(.019f,.025f,.019f);
        _lighterCore.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
        _lighterFlame.Visible=_lighterCore.Visible=false;
    }

    private static ArrayMesh BuildLighterFlameMesh()
    {
        using var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        void Vertex(float u,float v)
        {
            var point=InteractionLighterSurface.Point(u,v);
            var normal=InteractionLighterSurface.Normal(u,v);
            surface.SetNormal(new Vector3(normal.X,normal.Y,normal.Z));
            surface.AddVertex(new Vector3(point.X,point.Y,point.Z));
        }
        for(int row=0;row<InteractionLighterSurface.Rows;row++)
            for(int side=0;side<InteractionLighterSurface.Sides;side++)
            {
                float a=(float)row/InteractionLighterSurface.Rows,b=(float)(row+1)/InteractionLighterSurface.Rows;
                float x=(float)side/InteractionLighterSurface.Sides,y=(float)(side+1)/InteractionLighterSurface.Sides;
                // Clockwise outside faces; each pole has one triangle per side,
                // not a collapsed quad. Both flame layers share this bounded mesh.
                if(row<InteractionLighterSurface.Rows-1){Vertex(a,x);Vertex(b,y);Vertex(b,x);}
                if(row>0){Vertex(a,x);Vertex(a,y);Vertex(b,y);}
            }
        surface.Index();return surface.Commit();
    }

    private static void BuildStemScissors(Node3D root)
    {
        // The two tips meet at local origin; contact placement never assumes a hand mesh.
        for(int n=0;n<2;n++)
        {
            float side=n==0?-1:1;
            var blade=SceneArt.Box(root,new(side*.016f,-.08f,0),new(.015f,.16f,.012f),"bfc5bd");
            blade.RotationDegrees=new(0,0,side*-11);
            var handle=SceneArt.Mesh(root,new TorusMesh{InnerRadius=.028f,OuterRadius=.043f,Rings=20,RingSegments=6},new(side*.045f,-.205f,0),"708c80");
            handle.RotationDegrees=new(90,0,0);
        }
        SceneArt.Ball(root,new(0,-.14f,.009f),new(.027f,.027f,.018f),"8e948b");
    }
    private static void BuildEmptyStemVessel(Node3D root)
    {
        // An empty carried supply, kept smaller than the settled world's vessel.
        var shell=SceneArt.Mesh(root,new CylinderMesh{TopRadius=.075f,BottomRadius=.12f,Height=.22f,CapTop=false,RadialSegments=24},new(0,-.12f,0),"adc8c0");
        SceneArt.EnableSurfaceFade(shell);SceneArt.SetSurfaceOpacity(shell,.35f);
        SceneArt.Mesh(root,new TorusMesh{InnerRadius=.055f,OuterRadius=.08f,Rings=24,RingSegments=6},new(0,-.01f,0),"b5c9bd");
    }

    private void UpdateLighterFeedback(float dt,bool paused)
    {
        if(_lighterFlame==null||_lighterCore==null)return;
        bool contact=!paused&&_windowFocused&&_mouseHeld&&_hand.Visible&&_action==InteractionAction.Burn&&
            Sim.CurrentInteractionAction==InteractionAction.Burn&&(Sim.InteractionGestureOwnsPose||_detachedLiveContact)&&
            _gesturePose is "burn-flee" or "burn-tense" or "stem-burn";
        _lighterFlame.Visible=_lighterCore.Visible=contact;
        if(!contact){_lighterClock=0;return;}
        _lighterClock+=dt;
        float flicker=Mathf.Sin(_lighterClock*23)*.06f+Mathf.Sin(_lighterClock*37)*.035f;
        float length=1+flicker;
        _lighterFlame.Scale=new Vector3(.027f,.065f,.027f)*new Vector3(1-flicker*.6f,length,1-flicker*.6f);
        // Keep the original tip fixed at the actual sampled contact point.
        _lighterFlame.Position=new(0,.0115f-.0325f*length,0);
        _lighterCore.Scale=new Vector3(.019f,.025f,.019f)*new Vector3(1+flicker*.25f,1-flicker*.3f,1+flicker*.25f);
        if(_lighterFlame.MaterialOverride is StandardMaterial3D material)
            material.EmissionEnergyMultiplier=.48f+_appliedForce*.25f+flicker;
    }
}
