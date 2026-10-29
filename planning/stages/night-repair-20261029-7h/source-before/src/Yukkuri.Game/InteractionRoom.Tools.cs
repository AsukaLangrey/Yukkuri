using Godot;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private InteractionAction? _shownTool;
    private MeshInstance3D? _landingMarker,_landingStem;
    private MeshInstance3D? _cupWater;
    private Node3D? _toolFood;
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
        bool visible=c?.Interaction.Carried==true&&!_modalOpen;
        _landingMarker.Visible=visible;_landingStem!.Visible=visible;
        if(!visible)return;
        Vector2 mouse=GetViewport().GetMousePosition();
        bool placing=_toolArmed&&_action==InteractionAction.PutDown&&IsScenePoint(mouse);
        Vector3 point=placing?GroundPoint(mouse):new Vector3((float)c!.X,0,(float)c.Z);
        if(placing&&!TryGroundPoint(mouse,0,out point)){_landingMarker.Visible=false;_landingStem.Visible=false;return;}
        bool clear=Sim.CanPlaceInteractionAt(c!.Id,point.X,point.Z);
        _landingMarker.Position=point+Vector3.Up*.028f;
        _landingMarker.Scale=Vector3.One*(float)(WorldSimulation.InteractionFootprint(c)/.60);
        _landingMarker.MaterialOverride=SceneArt.Mat(clear?"739683":"b66f56");
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
                InteractionFoodArt.Sweet(_hand,InteractionFoodSurface.SweetHandScale);break;
            case InteractionAction.Drink:
                _cupWater=InteractionToolArt.Cup(_hand);break;
            case InteractionAction.Care:
                InteractionToolArt.CarePad(_hand);break;
            case InteractionAction.RepairAccessory:
                InteractionToolArt.RepairNeedle(_hand);break;
            case InteractionAction.BurnFeet:
                InteractionFixtureArt.RoundedBox(_hand,new(0,-.035f,0),new(.24f,.06f,.18f),"777569",.012f);
                var hotPlate=InteractionFixtureArt.RoundedBox(_hand,new(0,.003f,0),new(.20f,.018f,.14f),"dc9c62",.004f);
                var hotMaterial=(StandardMaterial3D)hotPlate.MaterialOverride.Duplicate();hotMaterial.EmissionEnabled=true;
                hotMaterial.Emission=new Color("d38c52");hotMaterial.EmissionEnergyMultiplier=.25f;hotPlate.MaterialOverride=hotMaterial;
                InteractionFixtureArt.RoundedBox(_hand,new(0,-.04f,.15f),new(.065f,.06f,.20f),"927c64",.014f);break;
            case InteractionAction.Press:case InteractionAction.Squeeze:case InteractionAction.Hold:case InteractionAction.Slap:
                SceneArt.Ball(_hand,Vector3.Zero,new(.36f,.10f,.32f),"efc5a4");
                for(int n=0;n<4;n++)
                {
                    float x=(n-1.5f)*.075f;
                    SceneArt.Ball(_hand,new(x,-.015f,-.18f),new(.07f,.07f,.23f),"f4d4b7");
                    SceneArt.Ball(_hand,new(x,.013f,-.24f),new(.038f,.004f,.05f),"f9dfcb");
                }
                break;
            default:
                SceneArt.Ball(_hand,Vector3.Zero,new(.17f,.12f,.20f),"efc5a4");
                SceneArt.Ball(_hand,new(0,0,-.12f),new(.07f,.08f,.20f),"f4d4b7");
                SceneArt.Ball(_hand,new(0,.033f,-.18f),new(.04f,.004f,.055f),"f9dfcb");break;
        }
        SceneArt.SetRenderLayer(_hand,SceneArt.InteractionLayer);
    }
}
