using System;
using System.Collections.Generic;

namespace NodeWar.Core
{
    /// <summary>Rolling windows over accepted emotes, using a caller's monotonic seconds.</summary>
    public class EmoteRateLimiter
    {
        private readonly List<double> accepted = new List<double>(10);

        public bool TryAccept(double now)
        {
            if (NextAllowedTime(now) > now) return false;
            accepted.Add(now);
            return true;
        }

        /// <summary>Returns now when ready, otherwise the first time both caps allow a send.</summary>
        public double NextAllowedTime(double now)
        {
            // Oldest first, so expired entries are a prefix. A plain loop rather
            // than RemoveAll: the HUD asks this every 50 ms for the cooldown
            // state, and a capturing lambda per call is garbage a phone collects.
            int expired = 0;
            while (expired < accepted.Count && accepted[expired] + 5.0 <= now) expired++;
            if (expired > 0) accepted.RemoveRange(0, expired);

            double next = now;
            if (accepted.Count >= 5)
                next = Math.Max(next, accepted[accepted.Count - 5] + 1.0);
            if (accepted.Count >= 10)
                next = Math.Max(next, accepted[accepted.Count - 10] + 5.0);
            return next;
        }
    }
}
