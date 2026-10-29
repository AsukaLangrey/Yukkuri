using Godot;
using Yukkuri.Simulation;
using System.Collections.Generic;

public partial class CreatureActor
{
    private readonly Dictionary<(BodyPart,SurfaceSide),Node3D> _dressings=new();
    private void BuildDressings()
    {
        if(_dressings.Count>0)return;
        foreach(var mark in ActorSurfaceMarks.Dressing)
        {
            var root=new Node3D();_visual.AddChild(root);_dressings[(mark.Part,mark.Side)]=root;
            MeshInstance3D Layer(bool pad,string color)=>SculptSurface(root,ActorDressingSurface.Rows,ActorDressingSurface.Sides,(u,v)=>
            {
                var p=ActorDressingSurface.Point(mark,u,v*Mathf.Tau,pad);return new(p.X,p.Y,p.Z);
            },color,pad?(u,v)=>{float tint=ActorDressingSurface.Weave(u,v*Mathf.Tau);return new(tint,tint,tint);}:null,flipNormal:true);
            var bandage=Layer(false,"e8ddbf");bandage.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
            var pad=Layer(true,"f3ebd8");pad.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
            root.Visible=false;
        }
    }
    private void RefreshDressings(InteractionLife life)
    {
        // The old general health marker is not evidence that this body part was actually treated.
        _bandage.Visible=false;
        foreach(var (site,root) in _dressings)root.Visible=life.HasDressing(site.Item1,site.Item2);
    }
}
