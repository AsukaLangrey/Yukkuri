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
        var camera=new Camera3D{Position=new(16,20,24),Projection=Camera3D.ProjectionType.Orthogonal,Size=25};
        AddChild(camera);camera.LookAt(Vector3.Zero);
        AddChild(new DirectionalLight3D{RotationDegrees=new(-50,-35,0),ShadowEnabled=true});
        AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new("a9b9aa"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.3f}});
        for(int i=0;i<10;i++)
        {
            var c=new Creature{X=i%5*2-5,Z=i/5*3,Body=new BodyState{SpeciesId=i%2==0?"reimu":"marisa"}};
            var a=new CreatureActor();AddChild(a);a.Setup(c,i,null);actors.Add((a,c));
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
