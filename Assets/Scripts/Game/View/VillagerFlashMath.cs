namespace NodeWar.View
{
    /// <summary>Pure flash envelope and damage detection; no Unity types.</summary>
    public static class VillagerFlashMath
    {
        /// <summary>Linear 1 to 0 over duration; a non-positive duration is already finished.</summary>
        public static float Amount(float elapsed, float duration)
        {
            if (duration <= 0f || float.IsNaN(elapsed) || float.IsNaN(duration)) return 0f;
            float t = elapsed / duration;
            if (t <= 0f) return 1f;
            if (t >= 1f) return 0f;
            return 1f - t;
        }

        /// <summary>A flash-worthy hit is an HP decrease inside one life.</summary>
        public static bool TookDamage(int previousHP, int currentHP, bool sameLife)
            => sameLife && currentHP < previousHP;

        /// <summary>
        /// Both observations must exist and both be alive; first observation,
        /// death, consumption and respawn therefore never read as damage.
        /// </summary>
        public static bool SameLife(bool hadPrevious, bool previousAlive, bool currentAlive)
            => hadPrevious && previousAlive && currentAlive;
    }
}
