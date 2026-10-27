namespace NodeWar.View
{
    /// <summary>Pure presentation curve; callers supply unscaled elapsed time.</summary>
    public static class PlinthMotion
    {
        public const float Duration = 0.35f;

        public static float EaseOutCubic(float t01)
        {
            if (t01 <= 0f) return 0f;
            if (t01 >= 1f) return 1f;
            float remaining = 1f - t01;
            return 1f - remaining * remaining * remaining;
        }

        public static float RiseY(float t01, float fromY, float toY)
        {
            return fromY + (toY - fromY) * EaseOutCubic(t01);
        }
    }
}
