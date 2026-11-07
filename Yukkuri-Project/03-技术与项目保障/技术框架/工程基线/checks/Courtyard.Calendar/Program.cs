using System.Text.Json;
using Yukkuri.Simulation;

int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action run){try{run();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
double Seconds(int day,double hour)=>(day+hour/24-8.0/24)*600;
Check("600 seconds per day and precise clock across minute/day boundaries",()=>{
    for(int day=1;day<=28;day++)
    for(int minute=0;minute<1440;minute++){
        var t=CourtyardCalendar.At(Seconds(day,minute/60.0));
        Require(t.Clock==$"{minute/60:00}:{minute%60:00}","clock "+day+"/"+minute+" got "+t.Clock);
    }
    Require(CourtyardCalendar.At(0).Clock=="08:00","start time");
    Require(CourtyardCalendar.At(600).DayOfSeason==2&&CourtyardCalendar.At(600).Clock=="08:00","day length");
    Require(CourtyardCalendar.At(400-.001).Clock=="23:59"&&CourtyardCalendar.At(400+.001).Clock=="00:00","midnight");
});
Check("seven days per season, four seasons per year",()=>{
    for(int s=0;s<4;s++){
        var start=CourtyardCalendar.At(Seconds(s*7,0)+.00001 <0?0:Seconds(s*7,0)+.00001);
        Require((int)start.Season==s&&start.DayOfSeason==1,"season start");
        var last=CourtyardCalendar.At(Seconds((s+1)*7,0)-.00001);
        Require((int)last.Season==s&&last.DayOfSeason==7,"season end");
    }
    var next=CourtyardCalendar.At(Seconds(28,0)+.00001);
    Require(next.Year==2&&next.Season==CourtyardSeason.Spring&&next.DayOfSeason==1,"new year");
});
Check("spring/autumn 8h, summer 7h, winter 9h nights and smooth light",()=>{
    int[] expected={480,420,480,540};
    for(int s=0;s<4;s++){
        int nights=0;
        for(int minute=0;minute<1440;minute++){
            var t=CourtyardCalendar.At(Seconds(s*7+1,(minute+.5)/60));
            if(t.IsNight)nights++;
            Require(t.Daylight>=0&&t.Daylight<=1,"light range");
        }
        Require(nights==expected[s],"night minutes "+s+": "+nights);
        var noon=CourtyardCalendar.At(Seconds(s*7+1,12));
        Require(noon.Daylight==1&&CourtyardCalendar.At(Seconds(s*7+1,2)).Daylight==0,"day/night levels");
        foreach(double h in new[]{noon.Sunrise-.5,noon.Sunrise,noon.Sunrise+.5,noon.Sunset-.5,noon.Sunset,noon.Sunset+.5}){
            double a=CourtyardCalendar.At(Seconds(s*7+1,h-.0001)).Daylight;
            double b=CourtyardCalendar.At(Seconds(s*7+1,h+.0001)).Daylight;
            Require(Math.Abs(a-b)<.001,"lighting jumps");
        }
    }
});
Check("calendar follows simulation pause/close-up and save round-trip without mutating state",()=>{
    var sim=WorldSimulation.CreateCourtyardEconomy();
    sim.Advance(1);double before=sim.State.WorldSeconds;
    Require(before>0,"running clock");
    sim.State.Settings.Pause=true;sim.Advance(1);Require(sim.State.WorldSeconds==before,"pause");
    sim.State.Settings.Pause=false;sim.State.Mode=PlayMode.CloseUp;sim.Advance(1);Require(sim.State.WorldSeconds==before,"close-up");
    sim.State.Mode=PlayMode.Management;sim.Advance(1);Require(sim.State.WorldSeconds>before,"resume");
    sim.State.WorldSeconds=Seconds(22,21.75);
    string json=JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
    var date=CourtyardCalendar.At(sim.State.WorldSeconds);
    for(int i=0;i<100;i++)CourtyardCalendar.At(sim.State.WorldSeconds);
    Require(json==JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),"calendar altered state/RNG");
    var restored=JsonSerializer.Deserialize<WorldState>(json,SaveStore.JsonOptions)!;
    SaveStore.Validate(restored);
    Require(CourtyardCalendar.At(restored.WorldSeconds)==date,"save round-trip");
});
Environment.ExitCode=failures==0?0:1;
