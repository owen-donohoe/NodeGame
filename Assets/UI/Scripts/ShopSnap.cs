using System;

namespace NodeWar.Lobby
{
    public static class ShopSnap
    {
        public const float VelocityThreshold = 40f;

        public static float? Target(float scrollY, float velocity, float[] stallTops)
        {
            if (stallTops == null || stallTops.Length == 0 || float.IsNaN(scrollY) ||
                float.IsNaN(velocity) || Math.Abs(velocity) >= VelocityThreshold) return null;
            float best = 0f;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < stallTops.Length; i++)
            {
                float delta = stallTops[i] - scrollY;
                if (velocity > 0 && delta < 0 || velocity < 0 && delta > 0) continue;
                float candidate = Math.Abs(delta);
                if (candidate >= distance) continue;
                distance = candidate;
                best = stallTops[i];
            }
            if (!float.IsPositiveInfinity(distance)) return best;
            return velocity > 0 ? stallTops[stallTops.Length - 1] : stallTops[0];
        }

        public static float Shrink(float scrollY, float stallTop, float stallHeight)
        {
            if (stallHeight <= 0) return 0.6f;
            float distance = scrollY < stallTop ? stallTop - scrollY : Math.Max(0f, scrollY - stallTop - stallHeight);
            float t = Math.Min(1f, distance / stallHeight);
            return 1f - 0.4f * t * t * (3f - 2f * t);
        }
    }
}
