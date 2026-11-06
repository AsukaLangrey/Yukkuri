using System.Text.Json;
using Yukkuri.Simulation;

int failures=0,checks=0;
void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
void Check(string name,Action action)
{checks++;try{action();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
WorldSimulation Create(double hydration=100,double health=8,bool recovering=false)
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var c=sim.Selected!;
    sim.State.RoomObjects.Clear();sim.State.Creatures.RemoveAll(other=>other.Id!=c.Id);
    sim.State.Settings.Pause=false;sim.State.Mode=PlayMode.Management;
    c.Condition=recovering?PhysicalCondition.Recovering:PhysicalCondition.Active;
    c.Health=health;c.Fullness=100;c.Energy=100;c.Pain=0;
    c.Interaction.Hydration=hydration;c.Interaction.NextAutonomyAt=10000;
    c.RecoveryRemaining=recovering?.25:0;
    return sim;
}
WorldSimulation Reload(WorldSimulation sim)
{
    SaveStore.Validate(sim.State);
    var copy=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(copy);return new(copy);
}
foreach(double hydration in new[]{0d,10d,20d})
{
    Check($"dehydration {hydration}: ordinary recovery does not heal",()=>
    {var sim=Create(hydration);sim.Advance(.25);Require(sim.Selected!.Health<=8,$"health became {sim.Selected.Health}");});
    Check($"dehydration {hydration}: recovery expiry does not award health",()=>
    {
        var sim=Create(hydration,recovering:true);sim.Advance(.25);var c=sim.Selected!;
        Require(c.RecoveryRemaining==0&&c.Condition==PhysicalCondition.Active,"action recovery did not complete");
        Require(c.Health<=8,$"health became {c.Health}");
    });
}
Check("supported recovery expiry restores action without an instant health award",()=>
{
    var sim=Create(recovering:true);sim.Advance(.25);var c=sim.Selected!;
    Require(c.Condition==PhysicalCondition.Active,"action recovery did not complete");
    Require(Math.Abs(c.Health-8.00625)<1e-8,$"expected gradual recovery, got {c.Health}");
});
Check("supported ordinary healing remains gradual",()=>
{var sim=Create();sim.Advance(.25);Require(Math.Abs(sim.Selected!.Health-8.00625)<1e-8,"supported healing changed");});
Check("rehydration resumes gradual healing without a missed recovery award",()=>
{
    var sim=Create(10,recovering:true);sim.Advance(.25);var c=sim.Selected!;
    Require(c.Health==8,"dehydrated recovery awarded health");c.Interaction.Hydration=100;
    sim.Advance(.25);Require(Math.Abs(c.Health-8.00625)<1e-8,"rehydration failed to resume gradual recovery");
});
Check("digestive symptoms prevent recovery-expiry health award",()=>
{
    var sim=Create(recovering:true);var c=sim.Selected!;
    c.Interaction.Digestion.Severity=50;c.Interaction.Digestion.SymptomsRemaining=150;
    sim.Advance(.25);Require(c.Health<=8,"illness still received a recovery award");
});
Check("food shortage prevents recovery-expiry health award",()=>
{var sim=Create(recovering:true);sim.Selected!.Fullness=10;sim.Selected.RecoveryRemaining=.05;sim.Advance(.25);Require(sim.Selected.Health<=8,"hungry recovery awarded health");});
Check("close-up freezes recovery and health",()=>
{var sim=Create(recovering:true);sim.State.Mode=PlayMode.CloseUp;sim.Advance(10);Require(sim.Selected!.RecoveryRemaining==.25&&sim.Selected.Health==8,"close-up progressed recovery");});
Check("save reload preserves dehydration and recovery boundary",()=>
{var sim=Reload(Create(10,recovering:true));sim.Advance(.25);Require(sim.Selected!.Health==8&&sim.Selected.Condition==PhysicalCondition.Active,"reload bypassed recovery rules");});
Check("healthy overview remains normal",()=>
{Require(Create(100,100).Selected!.HealthSummary=="正常","healthy overview changed");});
Check("symptomatic overview cannot say normal",()=>
{var c=Create(100,100).Selected!;c.Interaction.Digestion.SymptomsRemaining=150;c.Interaction.Digestion.Severity=50;Require(c.HealthSummary=="消化不适","symptoms missing: "+c.HealthSummary);});
Check("injury illness and dehydration coexist in overview",()=>
{var c=Create(10,50).Selected!;c.Interaction.Digestion.SymptomsRemaining=150;Require(c.HealthSummary=="受伤 · 消化不适 · 缺水","combined states missing: "+c.HealthSummary);});
Check("recovering overview retains illness and dehydration",()=>
{var c=Create(10,8,true).Selected!;c.Interaction.Digestion.SymptomsRemaining=150;Require(c.HealthSummary=="恢复中 · 消化不适 · 缺水","recovery hides conditions");});
Check("critical overview retains urgency alongside symptoms",()=>
{var c=Create(10,0).Selected!;c.Condition=PhysicalCondition.Critical;c.Interaction.Digestion.SymptomsRemaining=150;Require(c.HealthSummary=="重伤 · 急需救治 · 消化不适 · 缺水","critical urgency hidden");});
Check("dead overview does not imply treatable symptoms",()=>
{var c=Create(0,0).Selected!;c.Condition=PhysicalCondition.Dead;c.Interaction.Digestion.SymptomsRemaining=150;Require(c.HealthSummary=="死亡","death obscured");});
Check("incubation does not reveal latent illness",()=>
{var c=Create(100,100).Selected!;c.Interaction.Digestion.IncubationRemaining=50;c.Interaction.Digestion.Severity=50;Require(c.HealthSummary=="正常","latent illness revealed");});
Check("overview is derived and does not mutate or persist",()=>
{
    var sim=Create(10,100);var c=sim.Selected!;c.Interaction.Digestion.Severity=50;c.Interaction.Digestion.SymptomsRemaining=150;
    string before=JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),summary=c.HealthSummary;
    Require(before==JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),"reading overview changed state");
    Require(!before.Contains("healthSummary",StringComparison.OrdinalIgnoreCase),"derived text persisted");
    Require(Reload(sim).Selected!.HealthSummary==summary,"reload changed overview");
});
Check("resolved symptoms disappear without changing injury classification",()=>
{var c=Create(100,100).Selected!;c.Interaction.Digestion.SymptomsRemaining=1;Require(c.HealthSummary=="消化不适","symptoms missing");c.Interaction.Digestion.SymptomsRemaining=0;Require(c.HealthSummary=="正常"&&c.HealthStatus==HealthStatus.Healthy,"resolved symptom persisted");});
Check("dead bodies do not heal or complete recovery",()=>
{var sim=Create(recovering:true);var c=sim.Selected!;c.Condition=PhysicalCondition.Dead;c.Health=0;sim.Advance(.25);Require(c.Health==0&&c.Condition==PhysicalCondition.Dead,"dead body recovered");});
Check("critical bodies do not use ordinary recovery",()=>
{var sim=Create(recovering:true);var c=sim.Selected!;c.Condition=PhysicalCondition.Critical;c.Health=0;c.CriticalRemaining=30;sim.Advance(.25);Require(c.Health==0&&c.Condition==PhysicalCondition.Critical&&c.CriticalRemaining<30,"critical care bypassed");});
Check("hydration just above threshold allows gradual recovery",()=>
{var sim=Create(20.1);sim.Advance(.25);Require(Math.Abs(sim.Selected!.Health-8.00625)<1e-8,"hydrated boundary blocked");});
Check("low energy does not allow ordinary healing",()=>
{var sim=Create();sim.Selected!.Energy=30;sim.Advance(.25);Require(sim.Selected.Health==8,"low-energy body healed");});
Check("healing remains capped at maximum health",()=>
{var sim=Create(100,99.999);sim.Advance(.25);Require(sim.Selected!.Health==100,"health cap changed");});
Check("symptom after a real tick still appears despite near-full health",()=>
{var sim=Create(100,100);var c=sim.Selected!;c.Interaction.Digestion.Severity=50;c.Interaction.Digestion.SymptomsRemaining=150;sim.Advance(.25);Require(c.Health>99&&c.Health<100&&c.HealthSummary=="消化不适","tick symptom is hidden");});
Check("dehydration label uses the observable threshold",()=>
{var c=Create(20,100).Selected!;Require(c.HealthSummary=="正常","threshold classified as depleted");c.Interaction.Hydration=19.99;Require(c.HealthSummary=="缺水","dehydration missing");});Console.WriteLine($"RESULT {checks-failures}/{checks} passed; {failures} failed");
return failures==0?0:1;
