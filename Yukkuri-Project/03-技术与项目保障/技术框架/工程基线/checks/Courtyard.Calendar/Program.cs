using System.Numerics;
using System.Text.Json;
using Yukkuri.Simulation;

int passed=0;
void Require(bool ok,string message) { if(!ok)throw new Exception(message); }
void Check(string name,Action body) { body();passed++;Console.WriteLine("PASS "+name); }
void Date(double seconds,int year,CourtyardSeason season,int day,int hour,int minute)
{
    var actual=CourtyardCalendar.FromWorldSeconds(seconds);
    Require(actual==new CourtyardDate(year,season,day,hour,minute),$"Wrong date at {seconds:R}: {actual}");
}
string Snapshot(WorldState state)=>JsonSerializer.Serialize(state,SaveStore.JsonOptions);
Check("approved start and midnight/season/year offsets",()=>{
    Date(0,1,CourtyardSeason.Spring,1,8,0);
    Date(400,1,CourtyardSeason.Spring,2,0,0);
    Date(4000,1,CourtyardSeason.Summer,1,0,0);
    Date(8200,1,CourtyardSeason.Autumn,1,0,0);
    Date(12400,1,CourtyardSeason.Winter,1,0,0);
    Date(16600,2,CourtyardSeason.Spring,1,0,0);
});
Check("adjacent binary values never round into next minute/day/season/year",()=>{
    foreach(double boundary in new[]{5.0,25,100,400,4000,8200,12400,16600,33400})
    {
        var before=CourtyardCalendar.FromWorldSeconds(Math.BitDecrement(boundary));
        var at=CourtyardCalendar.FromWorldSeconds(boundary);
        var after=CourtyardCalendar.FromWorldSeconds(Math.BitIncrement(boundary));
        Require(before!=at&&after==at,$"Boundary {boundary:R}");
    }
    Date(double.Epsilon,1,CourtyardSeason.Spring,1,8,0);
    Date(0.4,1,CourtyardSeason.Spring,1,8,0);
    Date(0.5,1,CourtyardSeason.Spring,1,8,1);
    Date(399.999999,1,CourtyardSeason.Spring,1,23,59);
});
Check("all dates in four years and exact midnight neighbors",()=>{
    for(int day=1;day<112;day++)
    {
        double seconds=day*600-200;
        Date(seconds,day/28+1,(CourtyardSeason)(day%28/7),day%7+1,0,0);
        int previous=day-1;
        Date(Math.BitDecrement(seconds),previous/28+1,(CourtyardSeason)(previous%28/7),previous%7+1,23,59);
    }
});
Check("approved seasonal sunrise and sunset exact boundaries",()=>{
    for(int season=0;season<4;season++)
    {
        int rise=season==1?330:season==3?390:360, set=season==1?1350:season==3?1290:1320;
        // Second day of each season ensures all test times are nonnegative.
        double midnight=(season*7+1)*600-200;
        foreach(var pair in new[]{(rise,true),(set,false)})
        {
            double seconds=midnight+pair.Item1*5.0/12;
            Require(CourtyardCalendar.FromWorldSeconds(seconds).IsDaytime==pair.Item2,"exact phase boundary");
            Require(CourtyardCalendar.FromWorldSeconds(Math.BitDecrement(seconds)).IsDaytime!=pair.Item2,"before phase boundary");
            Require(CourtyardCalendar.FromWorldSeconds(Math.BitIncrement(seconds)).IsDaytime==pair.Item2,"after phase boundary");
        }
        var sample=CourtyardCalendar.FromWorldSeconds(midnight);
        Require(sample.SunriseMinute==rise&&sample.SunsetMinute==set,"season timings");
    }
});
Check("invalid inputs rejected without changing save policy",()=>{
    foreach(double value in new[]{-1.0,double.NaN,double.PositiveInfinity,double.NegativeInfinity})
    {
        bool rejected=false;try{CourtyardCalendar.FromWorldSeconds(value);}catch(ArgumentOutOfRangeException){rejected=true;}
        Require(rejected,"invalid input accepted");
    }
    Date(-0.0,1,CourtyardSeason.Spring,1,8,0);
});
Check("entire save-valid range including maximum double has exact year",()=>{
    var state=WorldSimulation.CreateCourtyardEconomy().State;
    foreach(double seconds in new[]{0.0,1e12,1e100,double.MaxValue})
    {
        state.WorldSeconds=seconds;SaveStore.Validate(state);
        var date=CourtyardCalendar.FromWorldSeconds(seconds);
        var expectedYear=(new BigInteger(seconds)+200)/16800+1;
        Require(date.Year==expectedYear,"large exact year");
        Require(date.Day is >=1 and <=7&&date.Hour is >=0 and <24&&date.Minute is >=0 and <60,"large date bounds");
        Require(date.DisplayDate.Length<=29,"unbounded display");
    }
    Require(CourtyardCalendar.FromWorldSeconds(double.MaxValue).DisplayDate.Contains("约"),"large year must be explicitly approximate display");
});
Check("projection and formatting leave complete world and RNG unchanged",()=>{
    var sim=WorldSimulation.CreateCourtyardEconomy();sim.State.WorldSeconds=16600.25;
    string before=Snapshot(sim.State);
    for(int i=0;i<100;i++){var d=CourtyardCalendar.FromWorldSeconds(sim.State.WorldSeconds);_ = d.DisplayDate+d.PhaseName;}
    Require(before==Snapshot(sim.State),"projection mutated world");
});
Check("pause and closeup preserve clock; normal advance remains simulation seconds",()=>{
    var sim=WorldSimulation.CreateCourtyardEconomy();sim.State.WorldSeconds=399;
    foreach(var mode in new[]{PlayMode.CloseUp,PlayMode.Management})
    {
        sim.State.Mode=mode;sim.State.Settings.Pause=mode!=PlayMode.CloseUp;
        double before=sim.State.WorldSeconds;var date=CourtyardCalendar.FromWorldSeconds(before);
        sim.Advance(30);Require(sim.State.WorldSeconds==before&&CourtyardCalendar.FromWorldSeconds(before)==date,"frozen clock changed");
    }
    sim.State.Settings.Pause=false;sim.State.Mode=PlayMode.Management;
    sim.Advance(1);Require(Math.Abs(sim.State.WorldSeconds-400)<1e-8,"calendar altered advance rate");
});
Check("existing save roundtrip preserves clock and date without offline growth",()=>{
    var sim=WorldSimulation.CreateCourtyardEconomy();sim.State.WorldSeconds=16600.25;
    string before=Snapshot(sim.State);
    var state=JsonSerializer.Deserialize<WorldState>(before,SaveStore.JsonOptions)!;SaveStore.Validate(state);
    var loaded=new WorldSimulation(state);
    Require(loaded.State.WorldSeconds==16600.25,"load changed time");
    Require(CourtyardCalendar.FromWorldSeconds(loaded.State.WorldSeconds)==CourtyardCalendar.FromWorldSeconds(sim.State.WorldSeconds),"load changed calendar");
    Require(!before.Contains("SunriseMinute")&&!before.Contains("CourtyardDate"),"calendar leaked into saves");
});
Console.WriteLine($"CALENDAR_CHECKS_PASSED: {passed}");