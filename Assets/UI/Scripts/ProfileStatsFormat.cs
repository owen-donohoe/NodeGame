using System;
using System.Globalization;
using NodeWar.Backend;

namespace NodeWar.Lobby
{
    public static class ProfileStatsFormat
    {
        public const string Unknown = "—";

        public static string WinRate(PlayerStats stats)
        {
            if (stats == null) return Unknown;
            if (stats.Matches <= 0) return "0%";
            long wins = Math.Max(0, Math.Min(stats.Wins, stats.Matches));
            long percent = (wins * 100 + stats.Matches / 2) / stats.Matches;
            return percent.ToString(CultureInfo.InvariantCulture) + "%";
        }

        public static string[] Values(PlayerStats stats) => stats == null
            ? new[] { Unknown, Unknown, Unknown, Unknown, Unknown, Unknown }
            : new[] { Number(stats.Matches), Number(stats.Wins), Number(stats.Losses), WinRate(stats), Number(stats.Streak), Number(stats.Best) };

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
