using Godot;
using System.Collections.Generic;
using Yukkuri.Simulation;

public static class WoodlandArt
{
    public static void Add(Node3D parent,IEnumerable<WoodlandPatch> patches)
    {
        foreach(var patch in patches)
            InteractionToolArt.Surface(parent,patch.Rows,patch.Columns,(u,v)=>
            {var p=patch.Point(u,v);return new(p.X,p.Y,p.Z);},patch.Color,patch.Flip);
    }
    public static void Build(Node3D parent)
    {
        Add(parent,WoodlandSurface.River());
        foreach(var tree in WoodlandLayout.Trees)
        {
            Add(parent,WoodlandSurface.Tree(new((float)tree.X,0,(float)tree.Z),(float)tree.Scale));
            WoodlandDecorations.Add(parent,tree);
        }
    }
}
