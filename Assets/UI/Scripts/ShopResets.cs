using System;

namespace NodeWar.Lobby
{
    public enum StallKind { Daily, Crates, Weekly, Bundle, Coins, Laurels }

    public static class ShopResets
    {
        public static DateTime Next(DateTime localNow, StallKind kind) => Next(localNow, kind, localNow);

        public static DateTime Next(DateTime localNow, StallKind kind, DateTime bundleStart)
        {
            if (kind == StallKind.Bundle) return bundleStart.AddDays(2);
            if (kind != StallKind.Weekly) return localNow.Date.AddDays(1);
            int days = ((int)DayOfWeek.Monday - (int)localNow.DayOfWeek + 7) % 7;
            return localNow.Date.AddDays(days == 0 ? 7 : days);
        }
    }
}
