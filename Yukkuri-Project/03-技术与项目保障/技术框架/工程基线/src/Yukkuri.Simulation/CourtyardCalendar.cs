namespace Yukkuri.Simulation;

public enum CourtyardSeason { Spring, Summer, Autumn, Winter }
public sealed record CourtyardDate(long Year,int DayOfSeason,int DayOfYear,CourtyardSeason Season,
    double Hour,double Sunrise,double Sunset,double Daylight,bool IsNight)
{
    public string SeasonName=>Season switch {CourtyardSeason.Spring=>"春",CourtyardSeason.Summer=>"夏",CourtyardSeason.Autumn=>"秋",_=>"冬"};
    public string Clock
    {
        get { int minutes=(int)Math.Floor(Hour*60+1e-7)%1440;return $"{minutes/60:00}:{minutes%60:00}"; }
    }
    public string Phase=>Hour>=Sunrise-.5&&Hour<Sunrise+.5?"清晨":Hour>=Sunset-.5&&Hour<Sunset+.5?"黄昏":IsNight?"夜晚":"白天";
}

// A read-only calendar on the existing simulation clock. It adds no second timer,
// RNG consumption, offline settlement, or aging multiplier to old saves.
public static class CourtyardCalendar
{
    public const double SecondsPerDay=600,StartHour=8;
    public const int DaysPerSeason=7,DaysPerYear=DaysPerSeason*4;
    public static CourtyardDate At(double worldSeconds)
    {
        if(!double.IsFinite(worldSeconds)||worldSeconds<0)throw new ArgumentOutOfRangeException(nameof(worldSeconds));
        double days=worldSeconds/SecondsPerDay+StartHour/24;
        double elapsedDays=Math.Floor(days);
        int dayOfYear=(int)(elapsedDays%DaysPerYear);
        var season=(CourtyardSeason)(dayOfYear/DaysPerSeason);
        double hour=(days-elapsedDays)*24;
        // Spring/autumn: eight hours; summer: seven; winter: nine.
        double sunrise=season switch {CourtyardSeason.Summer=>5.5,CourtyardSeason.Winter=>6.5,_=>6};
        double sunset=season switch {CourtyardSeason.Summer=>22.5,CourtyardSeason.Winter=>21.5,_=>22};
        static double Smooth(double t){t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
        double daylight=Smooth(hour-sunrise+.5)*(1-Smooth(hour-sunset+.5));
        long year=(long)Math.Min(long.MaxValue-1024,Math.Floor(elapsedDays/DaysPerYear)+1);
        return new(year,dayOfYear%DaysPerSeason+1,dayOfYear+1,season,hour,sunrise,sunset,daylight,hour<sunrise||hour>=sunset);
    }
}
