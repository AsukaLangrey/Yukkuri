using Godot;
using Yukkuri.Simulation;

public partial class CreatureActor
{
    private Node3D BuildBodySurfaceMarks(BodySurfaceMark[] marks,bool wet)
    {
        var root=new Node3D();_visual.AddChild(root);
        foreach(var mark in marks)
        {
            var spot=SculptSurface(root,5,32,(u,v)=>
            {
                var p=ActorSurfaceMarks.Point(mark,u,v*Mathf.Tau,wet?.013f:.009f);
                return new(p.X,p.Y,p.Z);
            },wet?"92bfc3":"baa17c",(u,v)=>new Color(1,1,1,Mathf.Sqrt(Mathf.Max(0,1-u*u))),flipNormal:true);
            spot.SetMeta("surface_part",(int)mark.Part);
            spot.SetMeta("surface_side",(int)mark.Side);
            SceneArt.EnableSurfaceFade(spot);
            if(spot.MaterialOverride is StandardMaterial3D material)material.Roughness=wet?.18f:.95f;
        }
        return root;
    }
    private static void RefreshBodySurfaceMarks(Node3D root,InteractionLife life,bool wet)
    {
        bool any=false;
        foreach(var child in root.GetChildren())if(child is MeshInstance3D spot)
        {
            var part=(BodyPart)(int)spot.GetMeta("surface_part");
            var side=(SurfaceSide)(int)spot.GetMeta("surface_side");
            float alpha=ActorSurfaceMarks.Opacity(life,part,wet,side);
            spot.Visible=alpha>.002f;any|=spot.Visible;
            SceneArt.SetSurfaceOpacity(spot,alpha);
        }
        // A small wet patch must not disappear because other body parts are already dry.
        root.Visible=any;
    }
}
