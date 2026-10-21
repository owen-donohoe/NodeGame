using System;

namespace NodeWar.Lobby
{
    /// <summary>Continuous presentation offsets, in body heights and degrees. No RNG.</summary>
    public static class SuitReplacementMotion
    {
        public const float RiseSeconds = 0.25f, LowerSeconds = 0.20f;
        // Disjoint horizontal lanes include the widest replacement pop (0.7 / 0.78).
        public static float LaneX(int index, int count) => count <= 1 ? 0 : -0.95f + 1.9f * index / (count - 1);
        public static float WanderX(int index, int count, float sample) =>
            LaneX(index, count) + 0.02f * Math.Max(-1, Math.Min(1, sample));
        public static float Lift(float seconds, int index, int count, bool reduced = false)
        {
            double t = Math.Max(0, seconds);
            double ease = 1 - Math.Pow(1 - Math.Min(1, t / RiseSeconds), 3);
            double phase = 2 * Math.PI * index / Math.Max(1, count);
            if (reduced) return (float)(ease * 0.35); // steady lift, no bob
            return (float)(ease * (0.35 + 0.06 * Math.Sin(t * 2 * Math.PI / 1.4 + phase)));
        }
        public static float Shake(float seconds, int index, int count, bool reduced = false)
        {
            if (reduced) return 0f;
            double t = Math.Max(0, seconds);
            double ease = 1 - Math.Pow(1 - Math.Min(1, t / RiseSeconds), 3);
            return (float)(ease * 3 * Math.Sin(t * 2 * Math.PI * 2.5 + 2 * Math.PI * index / Math.Max(1, count)));
        }
        public static float Lower(float seconds) => (float)Math.Pow(1 - Math.Min(1, Math.Max(0, seconds) / LowerSeconds), 3);
        public static float Pop(float seconds)
        {
            double t = Math.Max(0, Math.Min(1, seconds / 0.28));
            return (float)(1 - 0.22 * Math.Cos(t * 3 * Math.PI) * Math.Sin(t * Math.PI));
        }
    }
}
