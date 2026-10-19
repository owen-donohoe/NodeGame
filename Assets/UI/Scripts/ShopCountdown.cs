using System;
using System.Globalization;

namespace NodeWar.Lobby
{
    public static class ShopCountdown
    {
        public static string Format(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero) return "0s";
            if (remaining.TotalDays >= 1) return remaining.Days.ToString(CultureInfo.InvariantCulture) + "d " + remaining.Hours.ToString(CultureInfo.InvariantCulture) + "h";
            if (remaining.TotalHours >= 1) return remaining.Hours.ToString(CultureInfo.InvariantCulture) + "h " + remaining.Minutes.ToString(CultureInfo.InvariantCulture) + "m";
            if (remaining.TotalMinutes >= 1) return remaining.Minutes.ToString(CultureInfo.InvariantCulture) + "m " + remaining.Seconds.ToString(CultureInfo.InvariantCulture) + "s";
            return remaining.Seconds.ToString(CultureInfo.InvariantCulture) + "s";
        }
    }
}
