namespace NodeWar.UI
{
    /// <summary>Finite, bounded geometry for Inspector/USS-authored UI skins.</summary>
    public static class UiArtMath
    {
        public static float Geometry(float value, float fallback, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
