using System;

namespace NodeWar.Lobby
{
    public readonly struct StageClusterFrame
    {
        public readonly float Distance, UpOffset, Pitch;
        public StageClusterFrame(float distance, float upOffset, float pitch)
        { Distance = distance; UpOffset = upOffset; Pitch = pitch; }
    }

    /// <summary>Fits the whole separated cluster into a band without changing the camera lens.</summary>
    public static class StageClusterFraming
    {
        public const float FieldOfView = 42f;
        public const float HorizontalInset = 0.04f;

        public static StageClusterFrame Fit(float aspect, float top, float bottom, bool suits,
            float halfWidth, float halfHeight, float halfDepth)
        {
            aspect = Math.Max(0.1f, aspect);
            top = Math.Max(0, Math.Min(0.8f, top));
            bottom = Math.Max(top + 0.08f, Math.Min(0.98f, bottom));
            float pitch = suits ? 30f : 42f;
            double radians = pitch * Math.PI / 180;
            double sin = Math.Sin(radians), cos = Math.Cos(radians);
            double tan = Math.Tan(FieldOfView * Math.PI / 360);
            double low = 1 - 2 * bottom, high = 1 - 2 * top, centre = (low + high) / 2;
            double distance = 1;
            for (int i = 0; i < 8; i++)
            {
                double x = (i & 1) == 0 ? -halfWidth : halfWidth;
                double y = (i & 2) == 0 ? -halfHeight : halfHeight;
                double z = (i & 4) == 0 ? -halfDepth : halfDepth;
                double up = y * cos + z * sin, forward = -y * sin + z * cos;
                distance = Math.Max(distance, Math.Abs(x) / ((1 - 2 * HorizontalInset) * tan * aspect) - forward);
                distance = Math.Max(distance, (up - high * tan * forward) / ((high - centre) * tan));
                distance = Math.Max(distance, (low * tan * forward - up) / ((centre - low) * tan));
                distance = Math.Max(distance, 0.2 - forward);
            }
            return new StageClusterFrame((float)distance, (float)(centre * tan * distance), pitch);
        }
    }
}
