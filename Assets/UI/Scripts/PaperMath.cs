using System;

namespace NodeWar.Lobby
{
    public readonly struct PaperPoint
    {
        public readonly float X, Y;
        public PaperPoint(float x, float y) { X = x; Y = y; }
    }

    /// <summary>Stable paper edges and pen drift, independent of Unity and runtime hash salts.</summary>
    public static class PaperMath
    {
        public static uint Seed(string name)
        {
            uint hash = 2166136261;
            if (name != null) foreach (char c in name) hash = unchecked((hash ^ c) * 16777619);
            return hash;
        }

        public static float Noise(uint seed, float phase)
        {
            double offset = (seed % 65521) * (Math.PI * 2 / 65521);
            return (float)(0.65 * Math.Sin(phase * 2 + offset) +
                0.25 * Math.Sin(phase * 3 + offset * 2) + 0.1 * Math.Sin(phase * 5 + offset * 3));
        }

        public static float Rotation(uint seed) => Noise(seed, 0.7f);
        public static PaperPoint InkPoint(uint seed, float x, float y, float amplitude = 0.22f)
            => new PaperPoint(x + amplitude * Noise(seed, x * 0.22f + y * 0.15f),
                y + amplitude * Noise(seed ^ 7919u, y * 0.2f + x * 0.13f));

        public static PaperPoint[] Outline(float width, float height, float radius, float amplitude, uint seed)
        {
            const int steps = 8;
            var points = new PaperPoint[steps * 8 + 1];
            radius = Math.Max(0, Math.Min(radius, Math.Min(width, height) * 0.5f));
            amplitude = Math.Max(0, Math.Min(amplitude, Math.Min(width, height) * 0.025f));
            int index = 0;
            for (int corner = 0; corner < 4; corner++)
            {
                float cx = corner < 2 ? width - radius : radius;
                float cy = corner == 0 || corner == 3 ? radius : height - radius;
                for (int step = 0; step < steps; step++)
                {
                    double angle = (-90 + corner * 90 + step * 90.0 / steps) * Math.PI / 180;
                    Add(cx + radius * (float)Math.Cos(angle), cy + radius * (float)Math.Sin(angle),
                        (float)Math.Cos(angle), (float)Math.Sin(angle));
                }
                float sx = corner == 0 ? width : corner == 2 ? 0 : cx;
                float sy = corner == 1 ? height : corner == 3 ? 0 : cy;
                float ex = corner == 0 ? width : corner == 1 ? radius : corner == 2 ? 0 : width - radius;
                float ey = corner == 0 ? height - radius : corner == 1 ? height : corner == 2 ? radius : 0;
                for (int step = 0; step < steps; step++)
                {
                    float t = step / (float)steps;
                    Add(sx + (ex - sx) * t, sy + (ey - sy) * t,
                        corner == 0 ? 1 : corner == 2 ? -1 : 0,
                        corner == 1 ? 1 : corner == 3 ? -1 : 0);
                }
            }
            points[index] = points[0];
            return points;

            void Add(float x, float y, float nx, float ny)
            {
                float drift = amplitude * Noise(seed, index * (float)(Math.PI * 2 / (steps * 8)));
                points[index++] = new PaperPoint(x + nx * drift, y + ny * drift);
            }
        }
    }
}
