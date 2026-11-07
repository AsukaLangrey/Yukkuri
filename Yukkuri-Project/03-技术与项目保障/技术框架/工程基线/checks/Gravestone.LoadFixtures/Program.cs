using System.Text.Json;
using Yukkuri.Simulation;
string root=Environment.GetEnvironmentVariable("YUKKURI_GRAVE_TEST_ROOT")??"";
if(!Path.IsPathFullyQualified(root)||args.Length!=1)throw new ArgumentException("Explicit isolated root and fresh output required");
string output=Path.GetFullPath(args[0]);
if(output!=Path.Combine(root,"fixture-attempt-01")||Directory.Exists(output)||File.Exists(output))throw new ArgumentException("Fresh isolated fixture output required");
foreach(string key in new[]{"APPDATA","LOCALAPPDATA","TEMP","TMP","USERPROFILE","DOTNET_CLI_HOME"})
    if(!(Environment.GetEnvironmentVariable(key)??"").StartsWith(root+Path.DirectorySeparatorChar+"process-",StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("Isolated environment required: "+key);
Directory.CreateDirectory(output);
var results=new List<object>();var checkpoints=new List<object>();int checks=0;
void Check(bool valid,string name){checks++;results.Add(new{name,passed=valid});Console.WriteLine((valid?"PASS ":"FAIL ")+name);if(!valid)throw new Exception(name);}
string Json<T>(T x)=>JsonSerializer.Serialize(x,SaveStore.JsonOptions);
void Record(string stage,WorldState s){File.WriteAllText(Path.Combine(output,stage+".json"),Json(s));checkpoints.Add(new{stage,s.WorldSeconds,s.FamilySequence,creatures=s.Creatures.Select(c=>new{c.Id,c.Name,c.Condition,c.Family.HasGravestone,c.Family.Pregnancy.Phase,c.Family.Pregnancy.EventId,c.Family.Pregnancy.Elapsed}).ToArray()});File.WriteAllText(Path.Combine(output,"checkpoints.json"),Json(checkpoints));}
void Tick(WorldSimulation s,double seconds){for(int i=0;i<(int)Math.Round(seconds*20);i++){s.AdvanceInteractionPresentation(.05);s.Advance(.05);}}
try{
 // Controlled mature, healthy deep-bond fixture; NOT a natural courtship journey.
 // The three factory names remain unchanged. Only the added fourth fixture adult has a synthetic identity.
 var sim=WorldSimulation.CreateInteractionPlaytest(72381);sim.State.RoomObjects.Clear();sim.SetAutonomousBreeding(false);
 var fourth=JsonSerializer.Deserialize<Creature>(Json(sim.State.Creatures[1]),SaveStore.JsonOptions)!;
 fourth.Id="fixture-second-father";fourth.Name="夹具父体";fourth.Body.Id="fixture-second-father-body";
 fourth.Registration.IdentityId="fixture-second-father-identity";fourth.Interaction.Accessory.Id="fixture-second-father-accessory";fourth.Family=new();sim.State.Creatures.Add(fourth);
 for(int n=0;n<4;n++){
 var c=sim.State.Creatures[n];c.IsYoung=false;c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
 c.Fullness=c.Energy=c.Interaction.Hydration=c.Health=100;c.Pain=c.Fear=0;
 c.TrueDisposition="gentle";c.Aggression=.08;c.Patience=.6;c.Courage=.2;
 c.X=c.TargetX=n%2*1.65-3;c.Z=c.TargetZ=n<2?-2:3;
 c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;c.Family.NextSocialAt=c.Family.NextCareAt=10000;
 }
 foreach(var pair in new[]{(0,1),(2,3)}){
 var m=sim.State.Creatures[pair.Item1];var f=sim.State.Creatures[pair.Item2];
 m.Family.Peers[f.Id]=new(){Familiarity=90,Affection=90};f.Family.Peers[m.Id]=new(){Familiarity=90,Affection=90};
 Check(sim.FormPartnership(m,f),"production FormPartnership "+m.Name);
 }
 SaveStore.Validate(sim.State);Check(sim.State.FamilySequence==2&&sim.State.CompletedBirths.Count==0,"same prebirth world has exactly two established pairs");
 var store=new SaveStore(Path.Combine(output,"slots"));store.Save(sim.State,0);Record("base",sim.State);
 Check(Json(store.Load(0).State)==Json(sim.State),"base SaveStore disk roundtrip");
 var outcomes=new List<Creature>();
 for(int branch=1;branch<=2;branch++){
 var fork=new WorldSimulation(store.Load(0).State);int mi=branch==1?0:2;var m=fork.State.Creatures[mi];var f=fork.State.Creatures[mi+1];
 string response=fork.RequestPregnancy(m.Id,f.Id,BirthMethod.Live);
 Check(m.Family.Pregnancy.Phase==PregnancyPhase.Preparing,"branch "+branch+" production request enters preparing: "+response);
 for(int i=0;i<120&&m.Family.Pregnancy.Phase==PregnancyPhase.Preparing;i++)Tick(fork,.25);
 Record("branch-"+branch+"-gestating",fork.State);SaveStore.Validate(fork.State);
 Check(m.Family.Pregnancy.Phase==PregnancyPhase.Gestating,"branch "+branch+" production companionship reaches gestation");
 Check(fork.State.FamilySequence==3&&m.Family.Pregnancy.EventId=="family-birth-3","branch "+branch+" birth identity created by production sequence");
 for(int i=0;i<2200&&!fork.State.Creatures.Any(c=>c.Family.BirthEventId!=null);i++)Tick(fork,.25);
 Record("branch-"+branch+"-born",fork.State);SaveStore.Validate(fork.State);
 var babies=fork.State.Creatures.Where(c=>c.Family.BirthEventId!=null).ToArray();
 Check(babies.Length==1&&m.Family.Pregnancy.Phase==PregnancyPhase.Recovering,"branch "+branch+" actual gestation clock reaches one live birth");
 var baby=babies.Single();Check(baby.Name==m.Name+"的宝宝1"&&baby.Id=="family-birth-3-0","branch "+branch+" name and ID are untouched production outputs");
 // Controlled lethal cause through the production injury/death path, not arbitrary death flags.
 fork.Hurt(baby,200,"隔离墓碑诊断的受控致命伤",null);
 Check(baby.Condition==PhysicalCondition.Dead&&baby.Interaction.Injuries.DeathAt==fork.State.WorldSeconds,"branch "+branch+" production Hurt records death");
 string burial=fork.BuryCreature(baby.Id);
 Check(baby.Family.Memorialized&&baby.Family.HasGravestone,"branch "+branch+" production BuryCreature creates grave: "+burial);
 SaveStore.Validate(fork.State);store.Save(fork.State,branch);Record("branch-"+branch+"-buried",fork.State);
 Check(Json(store.Load(branch).State)==Json(fork.State),"branch "+branch+" final SaveStore disk roundtrip");outcomes.Add(baby);
 }
 Check(outcomes[0].Id==outcomes[1].Id&&outcomes[0].Name!=outcomes[1].Name,"legal divergent branches share child ID but have different maternal names");
 store.Save(store.Load(0).State,3);Check(store.Load(3).State.Creatures.All(c=>!c.Family.HasGravestone),"no-grave control has valid disk roundtrip");
 File.WriteAllText(Path.Combine(output,"result.json"),Json(new{success=true,checks,results,scope="Controlled mature healthy deep-bond fixture; production pairing/request/preparation/full gestation/birth/Hurt/BuryCreature, not natural courtship or user-input death",outcomes=outcomes.Select(c=>new{c.Id,c.Name,c.Family.MotherId,c.Family.BirthEventId,c.Interaction.Injuries.DeathAt,c.Family.MemorializedAt,c.Family.HasGravestone})}));
}catch(Exception e){File.WriteAllText(Path.Combine(output,"result.json"),Json(new{success=false,checks,results,error=e.ToString()}));Console.WriteLine(e);Environment.ExitCode=1;}
