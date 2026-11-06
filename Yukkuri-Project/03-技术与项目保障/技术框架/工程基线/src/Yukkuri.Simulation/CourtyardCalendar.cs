using System.Globalization;
using System.Numerics;

namespace Yukkuri.Simulation;

public enum CourtyardSeason { Spring, Summer, Autumn, Winter }

/// <summary>A read-only projection of simulated seconds; never advances the world or changes save data.</summary>
public readonly record struct CourtyardDate(BigInteger Year, CourtyardSeason Season, int Day, int Hour, int Minute)
{
    public int SunriseMinute => Season switch { CourtyardSeason.Summer => 330, CourtyardSeason.Winter => 390, _ => 360 };
    public int SunsetMinute => Season switch { CourtyardSeason.Summer => 1350, CourtyardSeason.Winter => 1290, _ => 1320 };
    public bool IsDaytime => Hour * 60 + Minute >= SunriseMinute && Hour * 60 + Minute < SunsetMinute;
    public string SeasonName => Season switch { CourtyardSeason.Spring => "春", CourtyardSeason.Summer => "夏", CourtyardSeason.Autumn => "秋", _ => "冬" };
    public string PhaseName => IsDaytime ? "日间" : "夜间";
    public string Clock => FormattableString.Invariant($"{Hour:00}:{Minute:00}");

    // SaveStore accepts every finite nonnegative double. Compact only the displayed year,
    // so even those extreme timestamps fit the existing clock label; Year remains exact.
    public string DisplayDate
    {
        get
        {
            string year = Year.ToString(CultureInfo.InvariantCulture);
            if (year.Length > 4) year = "约" + year[0] + "." + year.Substring(1, 2) + "e" + (year.Length - 1).ToString(CultureInfo.InvariantCulture);
            return $"{year}年{SeasonName}{Day}日 {Clock}";
        }
    }
}

public static class CourtyardCalendar
{
    public const int SecondsPerDay = 600;
    public const int DaysPerSeason = 7;
    public const int DaysPerYear = 28;

    public static CourtyardDate FromWorldSeconds(double worldSeconds)
    {
        if (!double.IsFinite(worldSeconds) || worldSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(worldSeconds), "World time must be finite and nonnegative.");

        // Convert the exact binary value before flooring to game minutes. Floating point
        // multiplication/addition can otherwise round the instant just before midnight
        // into the next day. BigInteger also covers the full existing save-valid range.
        ulong bits = (ulong)BitConverter.DoubleToInt64Bits(worldSeconds);
        int exponent = (int)((bits >> 52) & 0x7ff);
        BigInteger numerator = bits & 0x000fffffffffffffUL;
        if (exponent != 0) numerator += BigInteger.One << 52;
        int shift = exponent == 0 ? -1074 : exponent - 1023 - 52;
        BigInteger denominator = BigInteger.One;
        if (shift >= 0) numerator <<= shift; else denominator <<= -shift;
        BigInteger minutes = numerator * 12 / (denominator * 5) + 8 * 60;
        BigInteger dayIndex = BigInteger.DivRem(minutes, 24 * 60, out var minuteOfDay);
        BigInteger yearIndex = BigInteger.DivRem(dayIndex, DaysPerYear, out var dayOfYear);
        int day = (int)dayOfYear;
        return new(yearIndex + 1, (CourtyardSeason)(day / DaysPerSeason), day % DaysPerSeason + 1,
            (int)minuteOfDay / 60, (int)minuteOfDay % 60);
    }
}