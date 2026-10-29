using Godot;
using System.Collections.Generic;
using System.Linq;
using Yukkuri.Simulation;

public partial class Main
{
    private sealed record RegionPresentation(Node3D Root,Node3D Human,Node3D Food,Node3D Hand,Node3D Employee);
    private readonly Dictionary<string,RegionPresentation> _regionPresentations=new();
    private readonly Dictionary<string,CreatureActor> _persistentActors=new();
    private readonly Dictionary<string,Dictionary<string,Node3D>> _facilityPresentations=new();
    private static string FacilityKey(Facility f)=>$"{f.Kind}:{f.X:R}:{f.Z:R}";
    private void RememberFacility(Facility f,Node3D node)
    {
        if(!_facilityPresentations.TryGetValue(Sim.State.ActiveRegionId,out var entries))
            _facilityPresentations[Sim.State.ActiveRegionId]=entries=new();
        entries[FacilityKey(f)]=node;
    }
    private void SyncFacilityPresentation()
    {
        if(!_facilityPresentations.TryGetValue(Sim.State.ActiveRegionId,out var entries))
            _facilityPresentations[Sim.State.ActiveRegionId]=entries=new();
        foreach(var node in entries.Values)node.Visible=false;
        foreach(var f in Sim.ActiveRegion.Facilities)
        {
            string key=FacilityKey(f);
            if(!entries.TryGetValue(key,out var node)){node=SceneArt.Facility(_stage,f);entries[key]=node;}
            node.Visible=true;
            if(f.Kind==FacilityKind.Feeder&&node.GetChildCount()>1&&node.GetChild(1) is MeshInstance3D food)
                food.MaterialOverride=SceneArt.Mat(f.Stock>0?"e8bc73":"6b5a45");
        }
    }
    private void RememberRegionPresentation()
    {
        _regionPresentations[Sim.State.ActiveRegionId]=new(_stage,_human,_foodVisual,_handVisual!,_employee!);
    }
    private bool ActivateCachedRegion(bool resetCamera)
    {
        // Region views are retained. Traversal changes visibility and membership rather than
        // discarding thousands of renderer instances while a rendered frame still refers to them.
        CancelGesture();
        foreach(var region in _regionPresentations.Values){region.Root.Visible=false;region.Root.ProcessMode=ProcessModeEnum.Disabled;}
        foreach(var actor in _persistentActors.Values){actor.Freeze=true;actor.Visible=false;actor.ProcessMode=ProcessModeEnum.Disabled;actor.CollisionLayer=0;}
        if(!_regionPresentations.TryGetValue(Sim.State.ActiveRegionId,out var view))return false;
        _stage=view.Root;_human=view.Human;_foodVisual=view.Food;_handVisual=view.Hand;_employee=view.Employee;
        _stage.ProcessMode=ProcessModeEnum.Inherit;_stage.Visible=true;Actors.Clear();
        SyncFacilityPresentation();
        int index=0;
        foreach(var c in Sim.State.Creatures.Where(c=>c.RegionId==Sim.State.ActiveRegionId))
        {
            if(!_persistentActors.TryGetValue(c.Id,out var actor))
            {
                actor=new CreatureActor();_stage.AddChild(actor);actor.Setup(c,index,_font);
                actor.Impact=(id,speed)=>{var result=Sim.RegisterImpact(id,speed);if(result.Success)Toast(result.Message);};
                _persistentActors[c.Id]=actor;
            }
            else if(actor.GetParent()!=_stage)actor.Reparent(_stage);
            actor.ProcessMode=ProcessModeEnum.Inherit;actor.Visible=true;actor.CollisionLayer=2;actor.CollisionMask=3;
            actor.Position=new((float)c.X,.46f*actor.BodyScale,(float)c.Z);actor.LinearVelocity=Vector3.Zero;
            Actors[c.Id]=actor;index++;
        }
        SetMusic();
        if(resetCamera){_mapPan=Vector3.Zero;_mapZoom=25;Camera.Position=new(16,20,24);Camera.LookAt(Vector3.Zero);Camera.Size=25;}
        return true;
    }
}
