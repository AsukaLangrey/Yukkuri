using Godot;
using System.Collections.Generic;
using Yukkuri.Simulation;
using Expression=Yukkuri.Simulation.Expression;
public static class Main { public static bool SimpleActors=false,NameplatesEnabled=false; }
public partial class Probe:Node3D
{
    double elapsed,next;int cycles;
    readonly List<(CreatureActor actor,Creature creature)> actors=new();
    public override void _Ready()
    {
        if(System.Array.Exists(OS.GetCmdlineUserArgs(),a=>a=="--no-gc"))System.GC.TryStartNoGCRegion(256*1024*1024);
        var camera=new Camera3D{Position=new(16,20,24),Projection=Camera3D.ProjectionType.Orthogonal,Size=25};
        AddChild(camera);camera.LookAt(Vector3.Zero);
        AddChild(new DirectionalLight3D{RotationDegrees=new(-50,-35,0),ShadowEnabled=true});
        AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new("a9b9aa"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.3f}});
        for(int i=0;i<10;i++)
        {
            var c=new Creature{X=i%5*2-5,Z=i/5*3,Body=new BodyState{SpeciesId=i%2==0?"reimu":"marisa"}};
            var a=new CreatureActor();AddChild(a);a.Setup(c,i,null);actors.Add((a,c));
        }
        if(System.Array.Exists(OS.GetCmdlineUserArgs(),a=>a=="--dump"))
        {
            var root=new Node3D{Name="Models"};
            void Clone(Node3D source,Node3D parent)
            {
                Node3D node=source is MeshInstance3D m ? new MeshInstance3D{Mesh=m.Mesh,MaterialOverride=m.MaterialOverride}:new Node3D();
                parent.AddChild(node);node.Owner=root;node.Transform=source.Transform;node.Visible=source.Visible;
                foreach(var child in source.GetChildren())if(child is Node3D spatial)Clone(spatial,node);
            }
            foreach(var pair in actors)Clone(pair.actor,root);
            var scene=new PackedScene();scene.Pack(root);
            GD.Print(ResourceSaver.Save(scene,"D:/Project/Yukkuri/artifacts/m01/renderer-probe/Models.tscn"));GetTree().Quit();
        }
    }
    public override void _Process(double dt)
    {
        elapsed+=dt;
        if(elapsed>next)
        {
            next+=.5;cycles++;
            if(!System.Array.Exists(OS.GetCmdlineUserArgs(),a=>a=="--neutral"))foreach(var pair in actors)pair.creature.Expression=(Expression)(cycles%10);
            GD.Print("CSHARP_ACTOR_PROBE_CYCLE "+cycles);
        }
        if(!System.Array.Exists(OS.GetCmdlineUserArgs(),a=>a=="--static"))foreach(var pair in actors)pair.actor.Refresh(pair.creature,false,false,(float)dt,0);
        if(elapsed>30){GD.Print("CSHARP_ACTOR_PROBE_COMPLETE");GetTree().Quit();}
    }
}
