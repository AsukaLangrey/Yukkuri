using Godot;
using System;
using System.Collections.Generic;
using Yukkuri.Simulation;

// Decorative imports stay inside the existing non-walkable root footprint.
// They never create collision, navigation, intake, or selectable objects.
public static class WoodlandDecorations
{
    private const string Directory="res://Assets/ThirdParty/KenneyNature/";
    private static readonly Dictionary<string,PackedScene?> Scenes=new();
    private static readonly HashSet<string> Warnings=new(),Loaded=new();

    public static void Add(Node3D parent,WoodlandTree tree)
    {
        float size=(float)tree.Scale;
        // The model footprint occupies the narrow exposed root ring: center
        // distance .176 plus radius .043 stays below .22; its inner edge .133
        // clears the .13 trunk base. Keep the source aspect ratio unchanged.
        AddModel(parent,"grass.glb",new((float)tree.X+.17f*size,.006f,(float)tree.Z+.045f*size),.043f*size,.22f*size,false);
        AddModel(parent,"flower_yellowA.glb",new((float)tree.X-.17f*size,.006f,(float)tree.Z+.045f*size),.043f*size,.30f*size,true);
    }

    private static void AddModel(Node3D parent,string file,Vector3 position,float radius,float height,bool flower)
    {
        Node3D? model=Instantiate(file,radius,height);
        if(model==null)
        {
            model=new Node3D{Name=flower?"FallbackRootFlower":"FallbackRootGrass"};
            if(flower)FallbackFlower(model,radius,height);else FallbackGrass(model,radius,height);
        }
        parent.AddChild(model);model.Position=position;
        SceneArt.SetRenderLayer(model,SceneArt.SceneryLayer);
    }

    private static Node3D? Instantiate(string file,float radius,float height)
    {
        string path=Directory+file;
        Node? instance=null;
        try
        {
            if(!Scenes.TryGetValue(path,out var scene))
            {
                scene=ResourceLoader.Exists(path,"PackedScene")?ResourceLoader.Load<PackedScene>(path):null;
                Scenes[path]=scene;
            }
            if(scene==null||!scene.CanInstantiate())
            {Warn(path,"未找到可实例化的已导入资源，请在授权测试机完成 LFS 取回与 Godot 导入");return null;}
            instance=scene.Instantiate();
            if(instance is not Node3D imported||!TryBounds(imported,out var min,out var max))
            {
                instance.Free();instance=null;Scenes[path]=null;
                Warn(path,"模型不是有效的纯装饰网格，或包围范围无效");return null;
            }
            Vector3 extent=max-min;
            float halfDiagonal=new Vector2(extent.X,extent.Z).Length()*.5f;
            float scale=Mathf.Min(radius/halfDiagonal,height/extent.Y);
            if(!float.IsFinite(scale)||scale<=0)
            {instance.Free();instance=null;Scenes[path]=null;Warn(path,"模型尺寸无效");return null;}
            var result=new Node3D{Name=ModelNodeName(file),Scale=Vector3.One*scale};
            var pivot=new Node3D{Position=new(-(min.X+max.X)*.5f,-min.Y,-(min.Z+max.Z)*.5f)};
            result.AddChild(pivot);pivot.AddChild(imported);instance=null;
            if(Loaded.Add(path))GD.Print("低面树根资源已加载："+path);
            return result;
        }
        catch(Exception error)
        {
            instance?.Free();Scenes[path]=null;
            Warn(path,"加载失败："+error.Message);return null;
        }
    }

    private static string ModelNodeName(string file)=>file=="grass.glb"?"KenneyRootGrass":"KenneyRootFlower";

    private static bool TryBounds(Node3D root,out Vector3 min,out Vector3 max)
    {
        Vector3 low=new(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);
        Vector3 high=new(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);
        bool found=false,valid=true;
        void Visit(Node node,Transform3D parent)
        {
            if(!valid)return;
            // These source models contain only spatial groups and meshes. Reject
            // extra imported behavior instead of adding invisible physics or lights.
            if(node is not MeshInstance3D&&node.GetType()!=typeof(Node3D))
            {valid=false;return;}
            Transform3D transform=parent;
            if(node is Node3D spatial)
            {
                if(spatial.TopLevel){valid=false;return;}
                if(!spatial.Visible)return;
                transform*=spatial.Transform;
            }
            if(node is MeshInstance3D mesh&&mesh.Mesh!=null)
            {
                var box=mesh.Mesh.GetAabb();
                for(int n=0;n<8;n++)
                {
                    Vector3 p=transform*(box.Position+new Vector3((n&1)==0?0:box.Size.X,(n&2)==0?0:box.Size.Y,(n&4)==0?0:box.Size.Z));
                    if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z)){valid=false;return;}
                    low=new(Mathf.Min(low.X,p.X),Mathf.Min(low.Y,p.Y),Mathf.Min(low.Z,p.Z));
                    high=new(Mathf.Max(high.X,p.X),Mathf.Max(high.Y,p.Y),Mathf.Max(high.Z,p.Z));
                }
                found=true;
            }
            foreach(var child in node.GetChildren())Visit(child,transform);
        }
        Visit(root,Transform3D.Identity);min=low;max=high;
        return valid&&found&&high.Y-low.Y>.0001f&&new Vector2(high.X-low.X,high.Z-low.Z).Length()>.0001f;
    }

    private static void Warn(string path,string reason)
    {
        if(Warnings.Add(path))GD.PushWarning("低面树根资源降级："+path+"；"+reason+"。当前显示程序化草花，未代表外部模型已加载。");
    }

    private static void FallbackGrass(Node3D root,float radius,float height)
    {
        for(int n=0;n<5;n++)
        {
            float angle=n*Mathf.Tau/5;
            InteractionToolArt.Surface(root,4,2,(u,v)=>new Vector3(
                Mathf.Cos(angle)*u*radius*.7f-Mathf.Sin(angle)*(v-.5f)*radius*.4f*(1-u),
                u*height,
                Mathf.Sin(angle)*u*radius*.7f+Mathf.Cos(angle)*(v-.5f)*radius*.4f*(1-u)),n%2==0?"8fa568":"aab975");
        }
    }

    private static void FallbackFlower(Node3D root,float radius,float height)
    {
        SceneArt.Mesh(root,new CylinderMesh{TopRadius=radius*.08f,BottomRadius=radius*.10f,Height=height*.85f,RadialSegments=6},new(0,height*.425f,0),"829b65");
        for(int n=0;n<5;n++)
        {
            float angle=n*Mathf.Tau/5;
            SceneArt.Ball(root,new(Mathf.Cos(angle)*radius*.43f,height*.85f,Mathf.Sin(angle)*radius*.43f),new(radius*.6f,height*.07f,radius*.6f),"e5c66d");
        }
        SceneArt.Ball(root,new(0,height*.87f,0),new(radius*.4f,height*.07f,radius*.4f),"b88a45");
    }
}
