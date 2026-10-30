using Godot;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private InteractionAction? _shownTool;
    private MeshInstance3D? _landingMarker,_landingStem;
    private MeshInstance3D? _cupWater;
    private Node3D? _toolFood;
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
        bool placing=_toolArmed&&_action==InteractionAction.PutDown&&IsScenePoint(mouse);
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
        _cupWater=null;_toolFood=null;
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
            case InteractionAction.Care:
                InteractionToolArt.CarePad(_hand);break;
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

    private static void BuildLighterTool(Node3D root)
    {
        // Local +Y is the flame tip; PlaceContactTool aligns it with the contacted surface.
        InteractionFixtureArt.RoundedBox(root,new(0,-.20f,0),new(.115f,.24f,.075f),"728b84",.012f);
        InteractionFixtureArt.RoundedBox(root,new(0,-.065f,0),new(.12f,.055f,.08f),"a8aaa1",.007f);
        var flame=SceneArt.Ball(root,new(0,-.021f,0),new(.027f,.065f,.027f),"f5be67");
        var flameMat=(StandardMaterial3D)flame.MaterialOverride.Duplicate();
        flameMat.EmissionEnabled=true;flameMat.Emission=new Color("ffb650");flameMat.EmissionEnergyMultiplier=.65f;
        flame.MaterialOverride=flameMat;flame.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
        SceneArt.Ball(root,new(0,-.045f,0),new(.019f,.025f,.019f),"e9e5b1").CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
    }
}
