using System;
using Yukkuri.Simulation;

// Presentation-only engineering tuning, not additional gameplay rules.
internal readonly record struct CourtyardLightSample(double Daylight, double SunPitch, double SunYaw, double Warmth);
internal static class CourtyardLightCycle
{
    public static CourtyardLightSample Sample(double worldSeconds)
    {
        var date=CourtyardCalendar.FromWorldSeconds(worldSeconds);
        // Reduce before scaling to support all finite save-valid doubles; keep sub-minute precision.
        double minute=((worldSeconds%CourtyardCalendar.SecondsPerDay+200)%CourtyardCalendar.SecondsPerDay)*2.4;
        double rise=date.SunriseMinute, sunset=date.SunsetMinute;
        double day=Smooth((minute-(rise-30))/60)*(1-Smooth((minute-(sunset-30))/60));
        double progress=Math.Clamp((minute-(rise-30))/(sunset-rise+60),0,1);
        double arc=Math.Sin(progress*Math.PI);
        return new(day,-12-36*arc,-88+120*progress,1-arc);
    }
    private static double Smooth(double t) { t=Math.Clamp(t,0,1);return t*t*(3-2*t); }
}