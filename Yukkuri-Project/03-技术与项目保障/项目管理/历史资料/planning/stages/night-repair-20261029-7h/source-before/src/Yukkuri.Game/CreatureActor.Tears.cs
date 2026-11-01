using Godot;

public partial class CreatureActor
{
    private readonly ActorTearPresentation _tearPresentation=new();
    private readonly System.Collections.Generic.List<MeshInstance3D> _tearSurfaces=new();
    private void BuildTears()
    {
        _tearRoot=new Node3D();_visual.AddChild(_tearRoot);
        foreach(float side in new[]{-1f,1f})foreach(bool glint in new[]{false,true})
        {
            var tear=SculptSurface(_tearRoot,8,48,(u,v)=>AccessoryPoint(ActorTearSurface.Point(side,u,v*Mathf.Tau,glint)),glint?"e7f2ee":"94c4d1",flipNormal:true);
            tear.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
            SceneArt.EnableSurfaceFade(tear);
            if(tear.MaterialOverride is StandardMaterial3D material)material.Roughness=glint?.16f:.22f;
            _tearSurfaces.Add(tear);
        }
        _tearRoot.Visible=false;
    }
    private void RefreshTears(bool crying,float delta)
    {
        _tearPresentation.Advance(crying,delta);float amount=_tearPresentation.Amount;
        _tearRoot.Visible=amount>.002f;
        for(int index=0;index<_tearSurfaces.Count;index++)SceneArt.SetSurfaceOpacity(_tearSurfaces[index],amount*(index%2==0?.72f:.78f));
    }
}
