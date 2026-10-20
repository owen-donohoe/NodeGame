using System;

namespace NodeWar.Lobby
{
    public readonly struct StageClusterFrame
    {
        public readonly float Distance, UpOffset, Pitch;
        public StageClusterFrame(float distance, float upOffset, float pitch)
        { Distance = distance; UpOffset = upOffset; Pitch = pitch; }
    }

    public readonly struct StageSuitEnvelope
    {
        public readonly float HalfWidth, HalfHeight, HalfDepth, CentreY, CentreZ;
        public StageSuitEnvelope(float halfWidth, float halfHeight, float halfDepth, float centreY, float centreZ)
        { HalfWidth = halfWidth; HalfHeight = halfHeight; HalfDepth = halfDepth; CentreY = centreY; CentreZ = centreZ; }
    }

    /// <summary>Fits the whole separated cluster into a band without changing the camera lens.</summary>
    public static class StageClusterFraming
    {
        public const float FieldOfView = 42f;
        public const float HorizontalInset = 0.04f;
        public const float SuitHeightFraction = 0.84f; // 8% clear at each edge of the window.
        public static float SuitTop(float top, float bottom) =>
            (top + bottom) * 0.5f - (bottom - top) * SuitHeightFraction * 0.5f;
        public static float SuitBottom(float top, float bottom) =>
            (top + bottom) * 0.5f + (bottom - top) * SuitHeightFraction * 0.5f;

        /// <summary>Sweep measured resting mesh bounds across the walking area, billboard yaw,
        /// lift, +/-3 degree roll and pop. Includes the plot's 0.65 separation during settling.</summary>
        public static StageSuitEnvelope SweepSuit(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
        {
            float x = Math.Max(Math.Abs(minX), Math.Abs(maxX)) / 0.78f;
            float z = Math.Max(Math.Abs(minZ), Math.Abs(maxZ));
            float height = maxY - minY;
            float radius = (float)Math.Sqrt(x * x + z * z) + height * 0.06f;
            float bottom = minY - height * 0.06f;
            float top = maxY + height * (0.41f + 0.22f + 0.06f);
            return new StageSuitEnvelope(1 + radius, (top - bottom) / 2,
                0.675f + radius, (top + bottom) / 2, -0.975f);
        }

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
                distance = Math.Max(distance, Math.Abs(x) / ((1 - 2 * (suits ? 0.08f : HorizontalInset)) * tan * aspect) - forward);
                distance = Math.Max(distance, (up - high * tan * forward) / ((high - centre) * tan));
                distance = Math.Max(distance, (low * tan * forward - up) / ((centre - low) * tan));
                distance = Math.Max(distance, 0.2 - forward);
            }
            return new StageClusterFrame((float)distance, (float)(centre * tan * distance), pitch);
        }
    }
}
