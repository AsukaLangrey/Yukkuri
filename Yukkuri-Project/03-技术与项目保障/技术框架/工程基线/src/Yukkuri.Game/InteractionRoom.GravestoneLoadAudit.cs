using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private void StartGravestoneLoadAudit(string[] args)
    {
        int i=Array.IndexOf(args,GravestoneLoadGuard.Flag);
        if(i<0)return;
        SetProcess(false);
        _autoClock=-10000;
        SetRoomMuted(true);
        _=AuditGravestoneLoads(args[i+1]);
    }

    private async Task AuditGravestoneLoads(string output)
    {
        var results=new List<object>();
        var snapshots=new List<object>();
        var graveNames=new HashSet<string>();
        var settledCounts=new Dictionary<int,(int Tree,int Children)>();
        int failures=0;
        string Json<T>(T value)=>JsonSerializer.Serialize(value,SaveStore.JsonOptions);
        void Check(string name,bool passed)
        {
            results.Add(new{name,passed});
            if(!passed)failures++;
            GD.Print((passed?"PASS ":"FAIL ")+name);
        }
        void Require(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        string Hash(string file)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        void Stage(string stage)=>File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!,"stage.json"),Json(new{stage,at=DateTime.UtcNow}));
        void Checkpoint()=>File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!,"checkpoint.json"),Json(new{checks=results.Count,failures,results,snapshots}));
        void Click(string text)
        {
            var button=_modalContents.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.IsVisibleInTree()&&b.Text==text);
            Require(!button.Disabled,"disabled: "+text);
            button.EmitSignal(BaseButton.SignalName.Pressed);
        }
        string Label(Node3D node)=>node.GetChildren().OfType<Label3D>().Single().Text;
        // CreatureActor nameplates are not grave roots.
        int TreeGraves()=>GetChildren().OfType<Node3D>().Count(n=>n.GetType()==typeof(Node3D)&&
            n.GetChildren().OfType<Label3D>().Any(l=>graveNames.Contains(l.Text)));
        try
        {
            string fixturePath=Path.Combine(GravestoneLoadGuard.Root,"fixture-attempt-01","slots");
            Require(Enumerable.Range(1,3).All(i=>File.Exists(Path.Combine(fixturePath,$"slot-{i}.json"))),"Generate all three isolated fixtures first.");
            var fixtures=new SaveStore(fixturePath);
            var fixtureStates=new Dictionary<int,WorldState>();
            for(int i=1;i<=3;i++)
            {
                var state=fixtures.Load(i).State;
                SaveStore.Validate(state);
                fixtureStates[i]=state;
                foreach(var c in state.Creatures.Where(c=>c.Family.HasGravestone))graveNames.Add(c.Name);
                _saves.Save(state,i);
            }
            var a=fixtureStates[1].Creatures.Single(c=>c.Family.HasGravestone);
            var b=fixtureStates[2].Creatures.Single(c=>c.Family.HasGravestone);
            Check("valid divergent fixtures share production ID but differ in name",a.Id==b.Id&&a.Name!=b.Name);
            Check("no-grave control is valid",fixtureStates[3].Creatures.All(c=>!c.Family.HasGravestone));
            var hashes=Enumerable.Range(1,3).ToDictionary(i=>i,i=>Hash(_saves.SlotPath(i)));
            Check("initial fresh scene has no gravestones",_gravestones.Count==0&&TreeGraves()==0);
            await RenderFrames(3);
            Node3D? previous=null;
            var controls=new[]{(1,"A first"),(1,"A same-file"),(2,"B after A"),(2,"B same-file stale control"),
                (1,"A after B"),(3,"no-grave cleanup"),(2,"B fresh after cleanup"),(2,"B fresh same-file"),
                (1,"A after fresh B"),(3,"second no-grave cleanup"),(1,"A fresh after cleanup")};
            var steps=controls.Concat(Enumerable.Range(0,11).Select(i=>(i%2==0?2:1,"alternating reload "+(i+1))));
            foreach(var step in steps)
            {
                var expected=fixtures.Load(step.Item1).State;
                Stage(step.Item2+": before settings");
                ShowSettings();await RenderFrames(3);
                if(_slot!=step.Item1){Click("槽位 "+step.Item1);await RenderFrames(3);}
                Stage(step.Item2+": before load click");
                Click("读取槽位 "+step.Item1);await RenderFrames(3);
                Check(step.Item2+": actual system slot button loads exact valid world",!_modalOpen&&Json(Sim.State)==Json(expected));
                Stage(step.Item2+": before zero-delta production frame");
                _Process(0);await RenderFrames(4);
                var graves=Sim.State.Creatures.Where(c=>c.Family.HasGravestone).ToArray();
                Check(step.Item2+": grave dictionary and tree count",_gravestones.Count==graves.Length&&TreeGraves()==graves.Length);
                Check(step.Item2+": label matches current loaded identity",graves.All(c=>_gravestones.TryGetValue(c.Id,out var n)&&Label(n)==c.Name));
                Check(step.Item2+": marker position and visibility match loaded world",graves.All(c=>_gravestones.TryGetValue(c.Id,out var n)&&
                    n.Position.IsEqualApprox(new((float)c.X,0,(float)c.Z))&&n.Visible==(c.RegionId==Sim.State.ActiveRegionId&&Sim.State.Mode!=PlayMode.CloseUp)));
                Check(step.Item2+": source save bytes preserved",hashes.All(h=>Hash(_saves.SlotPath(h.Key))==h.Value));
                if(previous!=null)Check(step.Item2+": prior grave node actually freed",!GodotObject.IsInstanceValid(previous));
                var counts=((int)GetTree().GetNodeCount(),GetChildCount());
                if(settledCounts.TryGetValue(step.Item1,out var settled))
                    Check(step.Item2+": settled node counts remain constant for this fixture",counts==settled);
                else settledCounts[step.Item1]=counts;
                var marker=graves.Length==1?_gravestones[graves[0].Id]:null;
                // Materialize nested snapshots now; old nodes are freed by later loads.
                snapshots.Add(new{stage=step.Item2,slot=step.Item1,treeNodeCount=counts.Item1,directChildren=counts.Item2,
                    graveDictionary=_gravestones.Count,graveTreeCount=TreeGraves(),previousNodeStillValid=previous!=null&&GodotObject.IsInstanceValid(previous),
                    graves=graves.Select(c=>new{c.Id,expectedName=c.Name,label=Label(_gravestones[c.Id]),nodeId=_gravestones[c.Id].GetInstanceId(),
                        c.X,c.Z,c.Interaction.Injuries.DeathAt,c.Family.MemorializedAt}).ToArray()});
                previous=marker;
                Checkpoint();
            }
            Stage("close-up and region presentation controls");
            var current=Sim.State.Creatures.Single(c=>c.Family.HasGravestone);
            var currentMarker=_gravestones[current.Id];
            ulong markerId=currentMarker.GetInstanceId();
            string selectedId=Sim.State.SelectedId!;
            double worldSeconds=Sim.State.WorldSeconds;
            Sim.State.SelectedId=Sim.State.Creatures.First(c=>HasVisibleFamilyBody(c)&&c.RegionId==Sim.State.ActiveRegionId).Id;
            ToggleClose();_Process(0);await RenderFrames(3);
            Check("production close-up toggle hides grave",Sim.State.Mode==PlayMode.CloseUp&&!currentMarker.Visible);
            ToggleClose();_Process(0);await RenderFrames(3);
            Check("return from close-up restores grave",Sim.State.Mode!=PlayMode.CloseUp&&currentMarker.Visible&&Label(currentMarker)==current.Name);
            // Only the presentation region is controlled; no save or creature identity is edited.
            string region=Sim.State.ActiveRegionId;
            Sim.State.ActiveRegionId=Sim.State.Regions.FirstOrDefault(r=>r.Id!=region)?.Id??"grave-audit-other-region";
            PresentGravestones();
            Check("grave is hidden outside its region",!currentMarker.Visible);
            Sim.State.ActiveRegionId=region;
            Sim.State.SelectedId=selectedId;
            PresentGravestones();
            Check("return to region restores marker without reallocating it",currentMarker.Visible&&currentMarker.GetInstanceId()==markerId&&
                currentMarker.Position.IsEqualApprox(new((float)current.X,0,(float)current.Z))&&Label(currentMarker)==current.Name);
            Check("presentation controls do not advance simulation",Sim.State.WorldSeconds==worldSeconds);
            Check("manual files remain byte identical",hashes.All(h=>Hash(_saves.SlotPath(h.Key))==h.Value));
            Check("no automatic save was created",!File.Exists(_saves.SlotPath(0)));
            Checkpoint();
            File.WriteAllText(output,Json(new{success=failures==0,version=GameVersion.Display,checks=results.Count,failures,results,snapshots,
                scope="Opt-in isolated diagnostic; actual LoadRoom via system slot button signals and production presentation with zero simulation delta; finite reloads, headless, no native mouse or visual acceptance"}));
            GD.Print($"GRAVESTONE_LOAD_CHECKS: {results.Count-failures}/{results.Count}; failures={failures}");
            await RenderFrames(3);
            _sound.Stop();_sound.Stream=null;_roomMusic.Stop();_roomMusic.Stream=null;
            await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);
            GetTree().Quit(failures==0?0:1);
        }
        catch(Exception e)
        {
            try{File.WriteAllText(output,Json(new{success=false,version=GameVersion.Display,checks=results.Count,failures,results,error=e.ToString()}));}
            finally{GD.PushError(e.ToString());GetTree().Quit(2);}
        }
    }
}