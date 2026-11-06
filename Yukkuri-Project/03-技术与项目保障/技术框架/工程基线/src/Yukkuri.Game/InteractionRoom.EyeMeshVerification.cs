using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    // Reachable only inside the existing guarded speed verification, not a product flag.
    private bool _eyeReuseDisabledForCheck;
    private async Task VerifyEyeMeshReuse(string output,Action<bool,string> require,List<object> results,
        Func<double,int,Task> reset,Func<string> worldSnapshot,Func<string> actorSnapshot,Func<string> ballSnapshot)
    {
        // The speed-check entry has already validated this absolute, fresh output path.
        // Prepare it once for both checkpoints and any mismatch snapshots.
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output)!);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        object Field(CreatureActor actor,string name)=>typeof(CreatureActor).GetField(name,flags)!.GetValue(actor)!;
        MeshInstance3D Eye(CreatureActor actor,bool right)=>(MeshInstance3D)Field(actor,right?"_rightEye":"_leftEye");
        var set=typeof(CreatureActor).GetMethod("SetSurfaceEyes",flags)!;
        var build=typeof(CreatureActor).GetMethod("EyeSurfaceMesh",flags)!;
        void Set(CreatureActor actor,float open,float arc)=>set.Invoke(actor,new object[]{open,arc});
        string MeshHash(ArrayMesh mesh)=>Convert.ToHexString(SHA256.HashData(GD.VarToBytes(mesh.SurfaceGetArrays(0))));
        void EqualMesh(ArrayMesh actual,ArrayMesh expected,string label)
        {
            require(actual.GetSurfaceCount()==expected.GetSurfaceCount(),label+" surface count");
            require(actual.GetAabb()==expected.GetAabb(),label+" AABB");
            for(int surface=0;surface<expected.GetSurfaceCount();surface++)
            {
                require(actual.SurfaceGetPrimitiveType(surface)==expected.SurfaceGetPrimitiveType(surface),label+" primitive");
                require(actual.SurfaceGetMaterial(surface)==expected.SurfaceGetMaterial(surface),label+" mesh material");
                var a=actual.SurfaceGetArrays(surface);var b=expected.SurfaceGetArrays(surface);
                require(a.Count==b.Count,label+" array slots");
                for(int slot=0;slot<a.Count;slot++)
                    require(GD.VarToBytes(a[slot]).SequenceEqual(GD.VarToBytes(b[slot])),label+" exact array slot "+slot);
            }
        }
        string MaterialSnapshot(MeshInstance3D eye)
        {
            var material=(StandardMaterial3D)eye.MaterialOverride;
            return $"{material.AlbedoColor}:{material.VertexColorUseAsAlbedo}:{material.Roughness}:{material.MetallicSpecular}:{material.Transparency}:{material.ShadingMode}:{eye.CastShadow}";
        }
        string FaceSnapshot()=>string.Join("\n",_actors.OrderBy(p=>p.Key).Select(p=>
        {
            var actor=p.Value;
            var values=typeof(CreatureActor).GetFields(flags|BindingFlags.DeclaredOnly).Where(f=>
                !f.Name.Contains("EyeMeshBuildCount")&&(f.FieldType.IsPrimitive||f.FieldType.IsEnum||f.FieldType==typeof(Vector2)||f.FieldType==typeof(Vector3)))
                .OrderBy(f=>f.Name).Select(f=>f.Name+"="+f.GetValue(actor));
            var weights=(Dictionary<Yukkuri.Simulation.Expression,float>)Field(actor,"_mouthWeights");
            return p.Key+":"+string.Join(";",values)+":"+string.Join(";",weights.OrderBy(w=>w.Key))+":"+
                MeshHash((ArrayMesh)Eye(actor,false).Mesh)+":"+MeshHash((ArrayMesh)Eye(actor,true).Mesh)+":"+
                MaterialSnapshot(Eye(actor,false))+":"+MaterialSnapshot(Eye(actor,true));
        }));
        string Snapshot()=>worldSnapshot()+"\n"+actorSnapshot()+"\n"+ballSnapshot()+"\n"+FaceSnapshot();
        int Builds()=>_actors.Values.Sum(a=>a.EyeMeshBuildCount);
        void Checkpoint()=>System.IO.File.WriteAllText(output+".checkpoint.json",JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
        try
        {
            var cache=new EyeSurfaceMeshCache();var fixtures=new List<CreatureActor>();
            foreach(string species in new[]{"reimu","marisa"})foreach(int age in new[]{0,1,2})
            {
                var c=JsonSerializer.Deserialize<Creature>(JsonSerializer.Serialize(Sim.State.Creatures[0],SaveStore.JsonOptions),SaveStore.JsonOptions)!;
                c.Id="eye-fixture-"+species+age;c.Body.SpeciesId=species;c.IsYoung=age==1;c.IsElder=age==2;
                var actor=new CreatureActor{SharedEyeMeshes=cache};AddChild(actor);actor.Setup(c,0,_font);fixtures.Add(actor);
                require(actor.BodyScale==(age==1?.73f:age==2?1.03f:1),"fixture body scale");
            }
            require(fixtures.Sum(a=>a.EyeMeshBuildCount)==4,"initial eye pair should build once per palette");
            foreach(var key in new[]{(1f,0f),(.08f,0f),(0f,0f),(0f,.018f),(.5f,-.012f),(.5f,.0015f),(.5f,.00150001f)})
            {
                foreach(var actor in fixtures)
                {
                    Set(actor,1,0);Set(actor,key.Item1,key.Item2);
                    foreach(bool right in new[]{false,true})
                    {
                        var original=(ArrayMesh)build.Invoke(actor,new object[]{right?1f:-1f,key.Item1,key.Item2})!;
                        EqualMesh((ArrayMesh)Eye(actor,right).Mesh,original,"original/reused "+key);
                        original.Dispose();
                    }
                }
                foreach(int first in new[]{0,3})foreach(int second in new[]{first+1,first+2})
                {
                    require(Eye(fixtures[first],false).Mesh==Eye(fixtures[second],false).Mesh,"same exact key/hat must share left");
                    require(Eye(fixtures[first],true).Mesh==Eye(fixtures[second],true).Mesh,"same exact key/hat must share right");
                    require(Eye(fixtures[first],false).MaterialOverride!=Eye(fixtures[second],false).MaterialOverride,"actor eye material independence");
                }
            }
            var retained=(ArrayMesh)Eye(fixtures[1],false).Mesh;string retainedHash=MeshHash(retained);
            fixtures[0].Free();fixtures.RemoveAt(0);
            require(GodotObject.IsInstanceValid(retained)&&MeshHash(retained)==retainedHash,"one actor exit must not dispose another eye");
            var survivor=fixtures[0];Set(survivor,.08f,0);var beforeEviction=(ArrayMesh)Eye(survivor,false).Mesh;
            for(int n=0;n<EyeSurfaceMeshCache.Capacity+5;n++)
            {
                Set(fixtures[1],.5f,n*.0001f);
                require(cache.Count<=EyeSurfaceMeshCache.Capacity,"bounded cache occupancy");
            }
            require(GodotObject.IsInstanceValid(beforeEviction),"evicted but bound eye stays valid");
            Set(survivor,.5f,.02f);Set(survivor,.08f,0);
            require(Eye(survivor,false).Mesh==beforeEviction,"local working set survives shared eviction");
            Set(fixtures[1],.08f,0);
            require(Eye(fixtures[1],false).Mesh!=beforeEviction,"FIFO actually evicts oldest exact key");
            EqualMesh((ArrayMesh)Eye(fixtures[1],false).Mesh,beforeEviction,"evicted key regenerated exactly");
            cache.Clear();require(cache.Count==0,"lifecycle clear releases cache references");
            require(GodotObject.IsInstanceValid(Eye(survivor,false).Mesh),"lifecycle clear must not dispose bound eye");
            foreach(var actor in fixtures)actor.Free();
            results.Add(new{name="eye-exact-geometry-palette-scale-exit-eviction",passed=true,capacity=EyeSurfaceMeshCache.Capacity});

            // Equal initial serialized state, resource environment and explicit simulation inputs.
            // Compare every frame outside timings, including each actor's smoothing and bound meshes.
            var reference=new List<string>();
            foreach(bool shared in new[]{false,true})
            {
                _eyeReuseDisabledForCheck=!shared;await reset(10,6);
                for(int frame=0;frame<24;frame++)
                {
                    _Process(1.0/60);string snapshot=Snapshot();
                    if(!shared)reference.Add(snapshot);else
                    {
                        if(snapshot!=reference[frame])
                        {
                            System.IO.File.WriteAllText(output+".eye-actual.txt",snapshot);
                            System.IO.File.WriteAllText(output+".eye-reference.txt",reference[frame]);
                        }
                        require(snapshot==reference[frame],"paired frame state/pose/face/mesh "+frame);
                    }
                }
                results.Add(new{name="eye-paired-correctness",shared,frames=24,builds=Builds(),passed=true});
            }
            // Bounded long process samples, alternate AB/BA to expose warmup/order noise.
            for(int repetition=0;repetition<4;repetition++)foreach(int population in new[]{3,6})
            {
                string? expected=null;
                foreach(bool shared in repetition%2==0?new[]{false,true}:new[]{true,false})
                {
                    _eyeReuseDisabledForCheck=!shared;await reset(10,population);
                    int initialBuilds=Builds();var samples=new double[120];var watch=new Stopwatch();
                    for(int frame=0;frame<samples.Length;frame++)
                    {watch.Restart();_Process(1.0/60);watch.Stop();samples[frame]=watch.Elapsed.TotalMilliseconds;}
                    string state=Snapshot();if(expected==null)expected=state;else require(state==expected,"timed pair final equivalence");
                    var sorted=samples.OrderBy(x=>x).ToArray();
                    results.Add(new{name="eye-paired-process-timing",repetition,population,shared,frames=samples.Length,speed=10,
                        builds=Builds()-initialBuilds,cacheCount=_eyeMeshes.Count,rawMs=samples,
                        totalMs=samples.Sum(),medianMs=sorted[sorted.Length/2],p95Ms=sorted[(int)Math.Ceiling(sorted.Length*.95)-1],
                        note="CPU _Process only, identical initial state; no rendered FPS claim"});
                    Checkpoint();
                }
            }
            // Deliberately exceed shared capacity with staggered per-actor exact shapes.
            // This is an adversarial cache workload, not a gameplay performance prediction.
            int? originalStressBuilds=null;
            foreach(bool shared in new[]{false,true})
            {
                _eyeReuseDisabledForCheck=!shared;await reset(1,6);int initial=Builds();var actors=_actors.Values.ToArray();
                for(int cycle=0;cycle<2;cycle++)for(int key=0;key<32;key++)for(int actor=0;actor<actors.Length;actor++)
                    Set(actors[actor],.5f,(key+actor*32)*.0001f);
                require(_eyeMeshes.Count<=EyeSurfaceMeshCache.Capacity,"adversarial occupancy cap");
                foreach(var actor in actors)
                    require(((System.Collections.IDictionary)Field(actor,"_eyeSurfaceCache")).Count<=96,"original local occupancy cap");
                if(!shared)originalStressBuilds=Builds()-initial;
                else require(Builds()-initial<=originalStressBuilds,"shared second level must not regress local working set builds");
                results.Add(new{name="eye-over-capacity-staggered-revisit",shared,uniqueRequests=192,cycles=2,builds=Builds()-initial,cacheCount=_eyeMeshes.Count});Checkpoint();
            }
            _eyeReuseDisabledForCheck=false;await reset(1,6);
            require(_actors.Values.All(a=>ReferenceEquals(a.SharedEyeMeshes,_eyeMeshes)),"rebuild actors use room cache");
            var grown=Sim.State.Creatures[0];grown.IsYoung=!grown.IsYoung;SynchronizeFamilyActors();
            require(ReferenceEquals(_actors[grown.Id].SharedEyeMeshes,_eyeMeshes),"age rebuild uses room cache");
            results.Add(new{name="eye-room-rebuild-age-cache-binding",passed=true});
        }
        finally{_eyeReuseDisabledForCheck=false;}
    }
}