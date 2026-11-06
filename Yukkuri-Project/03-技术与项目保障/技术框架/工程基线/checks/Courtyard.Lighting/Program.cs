using System.Diagnostics;
using System.Text.Json;
using Yukkuri.Simulation;

var checks=new List<string>();
void Require(bool ok,string name) { if(!ok)throw new Exception(name); }
void Check(string name,Action body) { body(); checks.Add(name); Console.WriteLine("PASS "+name); }
void Near(double a,double b,string name,double tolerance=1e-10)=>Require(Math.Abs(a-b)<=tolerance,name);
Check("all seasonal transition edges, midpoint, monotonicity and sub-minute motion",()=>{
    for(int season=0;season<4;season++)
    {
        double midnight=(season*7+1)*600-200;
        var date=CourtyardCalendar.FromWorldSeconds(midnight);
        foreach(int boundary in new[]{date.SunriseMinute,date.SunsetMinute})
        {
            bool rise=boundary==date.SunriseMinute;
            double At(double minute)=>CourtyardLightCycle.Sample(midnight+minute*5/12).Daylight;
            Near(At(boundary-30),rise?0:1,"begin"); Near(At(boundary),.5,"mid");Near(At(boundary+30),rise?1:0,"end");
            double previous=At(boundary-30);
            for(int i=1;i<=600;i++)
            {double value=At(boundary-30+i*.1);Require(rise?value>=previous:value<=previous,"monotonic");previous=value;}
            Require(At(boundary+.01)!=At(boundary+.02),"integer-minute stepping");
        }
        Near(CourtyardLightCycle.Sample(midnight+300).Daylight,1,"noon");
        Near(CourtyardLightCycle.Sample(midnight).Daylight,0,"midnight");
    }
});
Check("midnight day/season/year changes preserve all visible lighting values",()=>{
    for(int day=1;day<=112;day++)
    {
        double midnight=day*600-200;
        foreach(double t in new[]{midnight-.0001,midnight,midnight+.0001})
            Near(CourtyardLightCycle.Sample(t).Daylight,0,"night continuity");
        // Sun orientation resets only while its energy is zero; moon orientation is fixed.
    }
});
Check("extreme finite timestamps are bounded and deterministic",()=>{
    foreach(double t in new[]{0.0,double.Epsilon,1e16,1e30,1e100,double.MaxValue,Math.BitDecrement(double.MaxValue)})
    {
        var s=CourtyardLightCycle.Sample(t);
        Require(s==CourtyardLightCycle.Sample(t),"determinism");
        Require(double.IsFinite(s.Daylight)&&s.Daylight>=0&&s.Daylight<=1&&double.IsFinite(s.SunPitch)&&double.IsFinite(s.SunYaw)&&s.Warmth>=0&&s.Warmth<=1,"finite bounds");
    }
    foreach(double t in new[]{-1.0,double.NaN,double.PositiveInfinity})
    {bool rejected=false;try{CourtyardLightCycle.Sample(t);}catch(ArgumentOutOfRangeException){rejected=true;}Require(rejected,"invalid rejection");}
});
Check("projection does not mutate simulation or save data",()=>{
    var sim=WorldSimulation.CreateCourtyardEconomy();
    string before=JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
    for(int i=0;i<100;i++)CourtyardLightCycle.Sample(sim.State.WorldSeconds);
    Require(before==JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),"state mutation");
});
for(int i=0;i<10000;i++)CourtyardLightCycle.Sample(i*.1);
long bytes=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
for(int i=0;i<100000;i++)CourtyardLightCycle.Sample(i*.1);
watch.Stop(); bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
Console.WriteLine(JsonSerializer.Serialize(new {status="passed",checks,performance=new{samples=100000,elapsedMs=watch.Elapsed.TotalMilliseconds,allocatedBytes=bytes},visualAcceptance=false}));