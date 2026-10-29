using Godot;
using Yukkuri.Simulation;

public partial class CreatureActor
{
    private Node3D? _hangHarness;
    private void BuildHangingHarness()
    {
        _hangHarness=new Node3D();_visual.AddChild(_hangHarness);
        SculptTube(_hangHarness,u=>AccessoryPoint(ActorHarnessSurface.Belt(u*Mathf.Tau)),.010f,"9e846b",64);
        foreach(float side in new[]{-1f,1f})
            SculptTube(_hangHarness,u=>AccessoryPoint(ActorHarnessSurface.Strap(side,u)),.012f,"9e846b",48);
        SculptTube(_hangHarness,u=>AccessoryPoint(ActorHarnessSurface.Yoke(u)),.012f,"9e846b",32);
        SceneArt.Ball(_hangHarness,AccessoryPoint(ActorHarnessSurface.Anchor),new(.065f,.055f,.025f),"726d62");
    }
    private void RefreshHangingHarness(InteractionLife life)
    {
        _hangHarness!.Visible=life.Restraint==RestraintKind.Hanging;
        if(!_hangHarness.Visible)return;
        Vector3 top=new(-.07f,1.46f,-.55f);
        Vector3 bottom=_hangEquipment!.ToLocal(_visual.ToGlobal(AccessoryPoint(ActorHarnessSurface.Anchor)));
        Vector3 span=bottom-top;
        _hangRope!.Position=(top+bottom)*.5f;
        _hangRope.Quaternion=span.LengthSquared()>.000001f?new Quaternion(Vector3.Up,span.Normalized()):Quaternion.Identity;
        _hangRope.Scale=new(.025f,Mathf.Max(.001f,span.Length()),.025f);
    }
}
