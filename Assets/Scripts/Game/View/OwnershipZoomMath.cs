namespace NodeWar.View
{
    /// <summary>Pure current-zoom normalization and ownership alpha curve; no Unity types.</summary>
    public static class OwnershipZoomMath
    {
        /// <summary>
        /// 0 = close, 1 = far, clamped to the gameplay distance range.
        /// Invalid ranges or any nonfinite distance input return close zoom.
        /// </summary>
        public static float Normalize(float distance, float minDistance, float maxDistance)
        {
            if (!IsFinite(distance) || !IsFinite(minDistance) || !IsFinite(maxDistance)
                || maxDistance <= minDistance) return 0f;

            // Widen before subtracting so extreme finite distances cannot overflow.
            double normalized = ((double)distance - minDistance) / ((double)maxDistance - minDistance);
            return (float)System.Math.Max(0d, System.Math.Min(1d, normalized));
        }

        /// <summary>
        /// Bounded smoothstep fade with far alpha at least near alpha. Zoom and
        /// all endpoints clamp to [0,1]; NaN and negative infinity become 0,
        /// positive infinity becomes 1. End <= start steps at the clamped start.
        /// </summary>
        public static float Alpha(float normalizedZoom, float fadeStart, float fadeEnd,
            float nearAlpha, float farAlpha)
        {
            float zoom = Clamp01(normalizedZoom);
            float start = Clamp01(fadeStart);
            float end = Clamp01(fadeEnd);
            float near = Clamp01(nearAlpha);
            float far = System.Math.Max(near, Clamp01(farAlpha));
            if (end <= start) return zoom < start ? near : far;

            float t = Clamp01((zoom - start) / (end - start));
            float smooth = t * t * (3f - 2f * t);
            return Clamp01(near + (far - near) * smooth);
        }

        private static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);

        private static float Clamp01(float value)
            => float.IsNaN(value) || value <= 0f ? 0f : value >= 1f ? 1f : value;
    }
}
