namespace NodeWar.View
{
    /// <summary>Pure swing detection and envelope; no Unity types, no simulation writes.</summary>
    public static class VillagerSwingMath
    {
        /// <summary>Share of the swing spent winding up to the peak; the rest is recovery.</summary>
        public const float AttackFraction = 0.25f;

        /// <summary>
        /// True when the cooldown was reset to its maximum since the previous
        /// observation while in combat: the tick the villager hit or healed.
        /// A decrease, an equal value, or an invalid maximum never swings.
        /// </summary>
        public static bool SwingStarted(int prevRemaining, int currRemaining, int cooldownMax, bool inCombat)
            => inCombat && cooldownMax > 0 && currRemaining == cooldownMax && prevRemaining < cooldownMax;

        /// <summary>
        /// Bounded envelope in [0,1]: zero at rest, rising linearly to one over
        /// AttackFraction of the duration, then falling back to zero.
        /// </summary>
        public static float Pulse(float secondsSinceSwing, float duration)
        {
            if (float.IsNaN(secondsSinceSwing) || float.IsNaN(duration) || duration <= 0f) return 0f;
            if (secondsSinceSwing <= 0f || secondsSinceSwing >= duration) return 0f;
            float t = secondsSinceSwing / duration;
            return t < AttackFraction ? t / AttackFraction : (1f - t) / (1f - AttackFraction);
        }

        /// <summary>
        /// Whether this observation starts a swing, given the previous one. First
        /// observation, respawn (not alive before) and entering combat never swing.
        /// </summary>
        public static bool ShouldSwing(bool hadPrevious, bool prevAlive, bool prevInCombat,
            int prevRemaining, bool currAlive, bool currInCombat, int currRemaining, int cooldownMax)
            => hadPrevious && prevAlive && currAlive && prevInCombat
               && SwingStarted(prevRemaining, currRemaining, cooldownMax, currInCombat);
    }
}
