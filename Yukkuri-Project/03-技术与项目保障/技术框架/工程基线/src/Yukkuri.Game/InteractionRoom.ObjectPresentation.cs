using Godot;
using System.Collections.Generic;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private readonly Dictionary<string,Node3D> _gravestones=new();
    private void PresentPortableObjects()
    {
        foreach(var m in Sim.State.PortableMotions)
        {
            Node3D? visual=null;
            if(_objects.TryGetValue(m.Id,out var prop))
            {
                visual=prop;
                var o=Sim.State.RoomObjects.Find(o=>o.Id==m.Id);
                visual.Visible=o!=null&&!o.Stored&&o.RegionId==Sim.State.ActiveRegionId;
            }
            else if(_supplyStationActors.TryGetValue(m.Id,out var station))visual=station;
            else if(_detachedStemActors.TryGetValue(m.Id,out var stem))visual=stem;
            else if(_actors.TryGetValue(m.Id,out var body))visual=body;
            if(visual!=null)visual.Position+=Vector3.Up*(float)m.Height;
        }
    }
    private void PresentGravestones()
    {
        foreach(var id in _gravestones.Keys.Where(id=>!Sim.State.Creatures.Any(c=>c.Id==id&&c.Family.HasGravestone)).ToArray())
        {_gravestones[id].QueueFree();_gravestones.Remove(id);}
        foreach(var c in Sim.State.Creatures.Where(c=>c.Family.HasGravestone))
        {
            if(!_gravestones.TryGetValue(c.Id,out var marker))
            {
                marker=new Node3D();AddChild(marker);_gravestones[c.Id]=marker;
                SceneArt.Box(marker,new(0,.20f,0),new(.32f,.4f,.12f),"9a9f91");
                marker.AddChild(new Label3D{Text=c.Name,Font=_font,FontSize=20,PixelSize=.005f,Position=new(0,.22f,.07f),Modulate=new Color("3d4b3e")});
            }
            marker.Position=new((float)c.X,0,(float)c.Z);marker.Visible=c.RegionId==Sim.State.ActiveRegionId&&Sim.State.Mode!=PlayMode.CloseUp;
        }
    }
}
