using Godot;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private InteractionAction? _shownTool;
    private MeshInstance3D? _landingMarker,_landingStem;
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
        bool clear=Sim.CanPlaceInteractionAt(c!.Id,point.X,point.Z);
        _landingMarker.Position=point+Vector3.Up*.028f;
        _landingMarker.Scale=Vector3.One*_actors[c.Id].BodyScale;
        _landingMarker.MaterialOverride=SceneArt.Mat(clear?"739683":"b66f56");
        _landingStem.Visible=!placing;
        float height=(float)c.Interaction.Height;
        _landingStem.Position=point+Vector3.Up*(height*.5f);
        _landingStem.Scale=new(1,Mathf.Max(.01f,height),1);
    }
    private void ShowInteractionTool()
    {
        if(_shownTool==_action)return;_shownTool=_action;
        foreach(var child in _hand.GetChildren()){_hand.RemoveChild(child);child.QueueFree();}
        _hand.Rotation=Vector3.Zero;
        switch(_action)
        {
            case InteractionAction.Clean:case InteractionAction.Dry:case InteractionAction.CleanAccessory:
                SceneArt.Box(_hand,Vector3.Zero,new(.24f,.055f,.20f),"eae5d4");
                SceneArt.Box(_hand,new(.08f,.03f,0),new(.02f,.006f,.18f),"91a9a2");break;
            case InteractionAction.FeedMeal:
                SceneArt.Ball(_hand,new(0,0,0),new(.23f,.13f,.18f),"e6d8ac");
                SceneArt.Box(_hand,new(0,-.07f,.02f),new(.26f,.018f,.22f),"d9d0b9");break;
            case InteractionAction.FeedSweet:case InteractionAction.Tease:
                SceneArt.Mesh(_hand,new CylinderMesh{TopRadius=.10f,BottomRadius=.10f,Height=.06f,RadialSegments=24},Vector3.Zero,"d4a464");
                foreach(float x in new[]{-.035f,.035f})SceneArt.Ball(_hand,new(x,.032f,.025f),new(.022f,.008f,.022f),"755747");break;
            case InteractionAction.Drink:
                SceneArt.Mesh(_hand,new CylinderMesh{TopRadius=.12f,BottomRadius=.08f,Height=.15f,RadialSegments=32},new(0,0,0),"d9e1dc");
                SceneArt.Mesh(_hand,new CylinderMesh{TopRadius=.10f,BottomRadius=.10f,Height=.008f,RadialSegments=32},new(0,.079f,0),"9dbdc4");break;
            case InteractionAction.Care:
                SceneArt.Box(_hand,Vector3.Zero,new(.23f,.055f,.14f),"eee9d9");
                SceneArt.Box(_hand,new(0,.03f,0),new(.09f,.008f,.09f),"d6d2bd");break;
            case InteractionAction.RepairAccessory:
                SceneArt.Ball(_hand,Vector3.Zero,new(.13f,.10f,.13f),"e2d7be");
                SceneArt.Box(_hand,new(.03f,0,-.10f),new(.025f,.025f,.18f),"b2bdaf");break;
            case InteractionAction.BurnFeet:
                SceneArt.Box(_hand,new(0,-.035f,0),new(.24f,.06f,.18f),"777569");
                SceneArt.Box(_hand,new(0,.003f,0),new(.20f,.018f,.14f),"dc9c62");
                SceneArt.Box(_hand,new(0,-.04f,.15f),new(.065f,.06f,.20f),"927c64");break;
            case InteractionAction.Press:case InteractionAction.Squeeze:case InteractionAction.Hold:
                SceneArt.Ball(_hand,Vector3.Zero,new(.36f,.10f,.32f),"efc5a4");
                for(int n=0;n<4;n++)SceneArt.Ball(_hand,new((n-1.5f)*.075f,-.015f,-.18f),new(.07f,.07f,.23f),"f4d4b7");break;
            default:
                SceneArt.Ball(_hand,Vector3.Zero,new(.17f,.12f,.20f),"efc5a4");
                SceneArt.Ball(_hand,new(0,0,-.12f),new(.07f,.08f,.20f),"f4d4b7");break;
        }
        SceneArt.SetRenderLayer(_hand,SceneArt.InteractionLayer);
    }
}
