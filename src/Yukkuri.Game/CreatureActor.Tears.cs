using Godot;
using Yukkuri.Simulation;
using Expression=Yukkuri.Simulation.Expression;

public partial class CreatureActor
{
    private readonly ActorTearPresentation _tearPresentation=new();
    private Creature? _tearOwner;
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
    private void RefreshTears(Creature creature,Expression expression,float delta)
    {
        bool dead=creature.Condition==PhysicalCondition.Dead;
        float target=ActorTearPresentation.Target((float)creature.Pain,(float)creature.Fear,
            (float)creature.Interaction.Emotion.Alarm,expression is Expression.Hurt or Expression.Frightened,
            creature.Condition==PhysicalCondition.Critical,dead);
        if(_tearOwner!=creature||dead)
        {_tearOwner=creature;_tearPresentation.Initialize(target);}
        else _tearPresentation.Advance(target,delta);
        float amount=_tearPresentation.Amount;
        _tearRoot.Visible=amount>.002f;
        for(int index=0;index<_tearSurfaces.Count;index++)
        {
            // When distress eases, the wet track remains after the bright bead
            // softens. Food changes neither this memory nor the source emotion.
            float opacity=index%2==0?amount*.72f:amount*amount*.78f;
            SceneArt.SetSurfaceOpacity(_tearSurfaces[index],opacity);
        }
    }
}
