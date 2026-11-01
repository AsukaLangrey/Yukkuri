using Godot;
using System.Collections.Generic;
using Yukkuri.Simulation;

public partial class CreatureActor
{
    private readonly Dictionary<(BodyPart,SurfaceSide),(MeshInstance3D Bruise,MeshInstance3D Scar)> _injuryMarks=new();

    private void RefreshInjuryMarks(InteractionLife life)
    {
        if(_injuryMarks.Count==0&&life.Injuries.Sites.Count==0)return;
        if(_injuryMarks.Count==0)
        {
            foreach(var source in ActorSurfaceMarks.Dressing)
            {
                var site=source with {Size=source.Size*.82f};
                MeshInstance3D Layer(bool scar)
                {
                    var mesh=SculptSurface(_visual,ActorInjurySurface.Rows,ActorInjurySurface.Sides,(u,v)=>
                    {
                        var point=ActorInjurySurface.Point(site,u,v*Mathf.Tau,scar);
                        return new(point.X,point.Y,point.Z);
                    },scar?"b19b86":"a78291",
                    (u,v)=>
                    {
                        var tint=ActorInjurySurface.Tint(u,v*Mathf.Tau,scar);
                        return new Color(tint.X,tint.Y,tint.Z,ActorInjurySurface.EdgeOpacity(u));
                    },flipNormal:true);
                    mesh.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
                    // SculptSurface owns the vertex-colour material; the fade
                    // copy preserves it. Its alpha multiplies real injury opacity.
                    SceneArt.EnableSurfaceFade(mesh);mesh.Visible=false;return mesh;
                }
                _injuryMarks[(site.Part,site.Side)]=(Layer(false),Layer(true));
            }
        }
        foreach(var (site,layers) in _injuryMarks)
        {
            float bruise=ActorInjuryPresentation.MarkOpacity(life,site.Item1,site.Item2,false);
            float scar=ActorInjuryPresentation.MarkOpacity(life,site.Item1,site.Item2,true);
            layers.Bruise.Visible=bruise>.002f;layers.Scar.Visible=scar>.002f;
            SceneArt.SetSurfaceOpacity(layers.Bruise,bruise);SceneArt.SetSurfaceOpacity(layers.Scar,scar);
        }
    }
}
