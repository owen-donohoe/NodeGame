using System;

namespace NodeWar.View
{
    /// <summary>World sprite pitch in degrees from vertical; independent of camera pitch.</summary>
    public static class VillagerTiltMath
    {
        public const float DefaultPitch = 20f;

        public static float Pitch(float fromVerticalDegrees)
            => float.IsNaN(fromVerticalDegrees) ? DefaultPitch : Math.Max(0f, Math.Min(89f, fromVerticalDegrees));

        public static (float Pitch, float Yaw, float Roll) Facing(
            float cameraPitch, float yaw, float roll, bool upright, float pitch)
            => upright ? (Pitch(pitch), yaw, 0f) : (cameraPitch, yaw, roll);

        public static float VerticalExtent(float height, float pitch)
            => ValidHeight(height) * (float)Math.Cos(Pitch(pitch) * Math.PI / 180.0);

        public static float ProjectedHeight(float height, float pitch, float cameraPitch)
        {
            if (float.IsNaN(cameraPitch) || float.IsInfinity(cameraPitch)) cameraPitch = DefaultPitch;
            return ValidHeight(height) * (float)Math.Abs(Math.Cos((cameraPitch - Pitch(pitch)) * Math.PI / 180.0));
        }

        private static float ValidHeight(float height)
            => float.IsNaN(height) || float.IsInfinity(height) || height < 0f ? 0f : height;
    }
}
