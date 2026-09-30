using System;
using System.Collections.Generic;
using System.Linq;

namespace NodeWar.Progression
{
    public struct DisconnectPenaltyState
    {
        public int Level;
        public long LastStrikeUnixSeconds;
        public long LastDecayUnixSeconds;
        public long BlockedUntilUnixSeconds;
        public List<long> NonReports;
    }

    public static class DisconnectPenalty
    {
        public const long DecayPeriodSeconds = 16 * 60 * 60;
        public const long NonReportWindowSeconds = 7 * 24 * 60 * 60;

        public static DisconnectPenaltyState Decay(DisconnectPenaltyState state, long now)
        {
            long since = Math.Max(state.LastStrikeUnixSeconds, state.LastDecayUnixSeconds);
            long periods = Math.Max(0, now - since) / DecayPeriodSeconds;
            if (periods > 0)
            {
                state.Level = (int)Math.Max(0, state.Level - periods);
                state.LastDecayUnixSeconds = since + periods * DecayPeriodSeconds;
            }
            return state;
        }

        public static DisconnectPenaltyState Strike(DisconnectPenaltyState state, long now)
        {
            state = Decay(state, now);
            state.Level++;
            state.LastStrikeUnixSeconds = now;
            long duration = state.Level <= 2 ? 0 : state.Level <= 4 ? 120 :
                state.Level == 5 ? 3600 : state.Level == 6 ? 86400 : 172800;
            state.BlockedUntilUnixSeconds = now + duration;
            return state;
        }

        public static DisconnectPenaltyState NonReport(DisconnectPenaltyState state, long now)
        {
            state.NonReports = (state.NonReports ?? new List<long>())
                .Where(time => time >= now - NonReportWindowSeconds).ToList();
            state.NonReports.Add(now);
            return state.NonReports.Count >= 2 ? Strike(state, now) : state;
        }
    }
}
