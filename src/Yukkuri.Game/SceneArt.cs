using Godot;
using Yukkuri.Simulation;

public static class SceneArt
{
    public const uint SceneryLayer=1,CreatureLayer=2,GroundLayer=4,InteractionLayer=8;
    private static readonly System.Collections.Generic.Dictionary<string,StandardMaterial3D> Materials=new();
    private static readonly System.Collections.Generic.Dictionary<string,Mesh> Meshes=new();
    public static StandardMaterial3D Mat(string color)
    {
        if(System.Array.Exists(OS.GetCmdlineUserArgs(),arg=>arg=="--unique-materials"))
            return new(){AlbedoColor=new Color(color),Roughness=.85f,CullMode=BaseMaterial3D.CullModeEnum.Disabled};
        if(!Materials.TryGetValue(color,out var material))
        {material=new(){AlbedoColor=new Color(color),Roughness=.85f,CullMode=BaseMaterial3D.CullModeEnum.Disabled};Materials[color]=material;}
        return material;
    }
    public static MeshInstance3D Mesh(Node3D parent, Mesh mesh, Vector3 position, string color)
    {
        if(System.Array.Exists(OS.GetCmdlineUserArgs(),arg=>arg=="--omit-"+mesh.GetType().Name))
        {var omitted=new MeshInstance3D{Position=position};parent.AddChild(omitted);return omitted;}
        string? key=mesh switch
        {
            BoxMesh b=>$"box:{b.Size}",
            SphereMesh s=>$"sphere:{s.Radius}:{s.Height}:{s.RadialSegments}:{s.Rings}",
            CylinderMesh c=>$"cylinder:{c.TopRadius}:{c.BottomRadius}:{c.Height}:{c.RadialSegments}:{c.Rings}",
            CapsuleMesh c=>$"capsule:{c.Radius}:{c.Height}:{c.RadialSegments}:{c.Rings}",
            TorusMesh t=>$"torus:{t.InnerRadius}:{t.OuterRadius}:{t.Rings}:{t.RingSegments}",
            _=>null
        };
        if(System.Array.Exists(OS.GetCmdlineUserArgs(),arg=>arg=="--unique-meshes"))key=null;
        if(System.Array.Exists(OS.GetCmdlineUserArgs(),arg=>arg=="--bake-meshes")&&mesh is PrimitiveMesh primitive)
        {var baked=new ArrayMesh();baked.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles,primitive.GetMeshArrays());mesh=baked;}
        if(key!=null){if(Meshes.TryGetValue(key,out var shared))mesh=shared;else Meshes[key]=mesh;}
        // MeshInstance3D owns a native Ref<Mesh>. Procedural meshes belong to their
        // scene nodes; a process-wide list would retain every retired actor on reload.
        uint layer=SceneryLayer;
        for(Node? ancestor=parent;ancestor!=null;ancestor=ancestor.GetParent())
            if(ancestor is RigidBody3D){layer=CreatureLayer;break;}
        var node=new MeshInstance3D { Mesh=mesh, Position=position, MaterialOverride=Mat(color),Layers=layer };parent.AddChild(node);return node;
    }
    // GeometryInstance3D.Transparency is ignored by the Compatibility renderer.
    // Give changing surface marks their own material so one oil cannot fade another's marks.
    public static void EnableSurfaceFade(MeshInstance3D mesh)
    {
        if(mesh.MaterialOverride is not StandardMaterial3D source)return;
        var material=(StandardMaterial3D)source.Duplicate();
        material.Transparency=BaseMaterial3D.TransparencyEnum.Alpha;
        mesh.MaterialOverride=material;
        mesh.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
    }
    public static void SetSurfaceOpacity(MeshInstance3D mesh,float opacity)
    {
        if(mesh.MaterialOverride is not StandardMaterial3D material)return;
        Color color=material.AlbedoColor;float alpha=Mathf.Clamp(opacity,0,1);
        if(Mathf.IsEqualApprox(color.A,alpha))return;
        color.A=alpha;material.AlbedoColor=color;
    }
    public static void SetRenderLayer(Node root,uint layer)
    {
        if(root is VisualInstance3D visual)visual.Layers=layer;
        foreach(var child in root.GetChildren())SetRenderLayer(child,layer);
    }
    public static MeshInstance3D Box(Node3D parent,Vector3 pos,Vector3 size,string color)=>Mesh(parent,new BoxMesh{Size=size},pos,color);
    public static void CombineStatic(Node3D root, params Node3D[] animated)
    {
        var excluded=new System.Collections.Generic.HashSet<Node3D>(animated);
        var groups=new System.Collections.Generic.Dictionary<Material,System.Collections.Generic.List<(MeshInstance3D node,Transform3D transform)>>();
        void Visit(Node3D node,Transform3D transform)
        {
            if(excluded.Contains(node))return;
            transform*=node.Transform;
            if(node is MeshInstance3D instance && instance.Mesh!=null && instance.MaterialOverride!=null)
            {
                if(!groups.TryGetValue(instance.MaterialOverride,out var list))groups[instance.MaterialOverride]=list=new();
                list.Add((instance,transform));
            }
            foreach(var child in node.GetChildren())if(child is Node3D spatial)Visit(spatial,transform);
        }
        foreach(var child in root.GetChildren())if(child is Node3D spatial)Visit(spatial,Transform3D.Identity);
        var merged=new ArrayMesh();
        foreach(var group in groups)
        {
            using var surface=new SurfaceTool();surface.Begin(Godot.Mesh.PrimitiveType.Triangles);
            foreach(var (node,transform) in group.Value)
                for(int i=0;i<node.Mesh.GetSurfaceCount();i++)surface.AppendFrom(node.Mesh,i,transform);
            surface.SetMaterial(group.Key);surface.Commit(merged);
            foreach(var (node,_) in group.Value){node.Mesh=null;}
        }
        root.AddChild(new MeshInstance3D{Mesh=merged});
    }
    public static MeshInstance3D Ball(Node3D parent,Vector3 pos,Vector3 size,string color)
    {
        bool actor=false;
        for(Node? ancestor=parent;ancestor!=null;ancestor=ancestor.GetParent())
            if(ancestor is CreatureActor){actor=true;break;}
        float span=Mathf.Max(size.X,Mathf.Max(size.Y,size.Z));
        int segments=actor?(span>.7f?64:span>.22f?40:24):24;
        int rings=actor?(span>.7f?32:span>.22f?20:12):12;
        var m=Mesh(parent,new SphereMesh{Radius=.5f,Height=1,RadialSegments=segments,Rings=rings},pos,color);
        m.Scale=size;return m;
    }
    public static Node3D Tree(Node3D parent,Vector3 position,float scale,bool wild=false)
    {
        var tree=new Node3D{Position=position,Scale=Vector3.One*scale};parent.AddChild(tree);
        Mesh(tree,new CylinderMesh{TopRadius=.12f,BottomRadius=.2f,Height=1.8f,RadialSegments=8},new(0,.8f,0),"786047");
        Ball(tree,new(0,1.9f,0),new(1.8f,2.1f,1.8f),wild?"42645d":"91ad76");
        Ball(tree,new(.5f,1.6f,0),new(1.3f,1.4f,1.4f),wild?"53796b":"adc786");return tree;
    }
    public static Node3D Facility(Node3D parent,Facility f)
    {
        var n=new Node3D{Position=new((float)f.X,0,(float)f.Z)};parent.AddChild(n);
        if(f.Kind==FacilityKind.Feeder)
        {
            Mesh(n,new CylinderMesh{TopRadius=.65f,BottomRadius=.45f,Height=.3f,RadialSegments=24},new(0,.18f,0),"c68b65");
            Mesh(n,new CylinderMesh{TopRadius=.5f,BottomRadius=.5f,Height=.03f,RadialSegments=24},new(0,.34f,0),f.Stock>0?"e8bc73":"6b5a45");
        }
        else
        {
            Box(n,new(0,.16f,0),new(1.5f,.25f,1.2f),"bc9673");
            Box(n,new(0,.32f,0),new(1.3f,.14f,1),"d9ceaa");
            Box(n,new(0,.43f,-.35f),new(.9f,.15f,.3f),"f6e7c7");
        }
        return n;
    }
}


