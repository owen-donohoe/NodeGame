using System;
using System.Collections.Generic;

namespace NodeWar.Input
{
    /// <summary>Inclusive screen-unit bounds. The default value is empty.</summary>
    public readonly struct PaintBounds
    {
        // Double intermediates keep expansion safe even for extreme finite float inputs.
        public readonly double MinX;
        public readonly double MinY;
        public readonly double MaxX;
        public readonly double MaxY;
        public bool IsValid { get; }

        internal PaintBounds(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            IsValid = true;
        }

        public bool Contains(GesturePoint p) => IsValid && PaintGeometry.IsFinite(p)
            && p.X >= MinX && p.X <= MaxX && p.Y >= MinY && p.Y <= MaxY;
    }

    /// <summary>
    /// An open stroke is the union of adjacent capsules, or one disc for one point.
    /// Points and radius use the same caller-supplied screen units. The caller converts
    /// the fixed 5 mm radius to pixels when needed; camera zoom does not scale it again.
    /// Decimation and point caps belong to capture: the authoritative recorded points
    /// must drive both drawing and selection. Never draw an unsampled tail that selects
    /// differently, close the stroke implicitly, or smooth it only for rendering.
    /// </summary>
    public static class PaintGeometry
    {
        /// <summary>Invalid coordinates return positive infinity.</summary>
        public static float DistanceSquaredToSegment(GesturePoint p, GesturePoint a, GesturePoint b)
        {
            if (!IsFinite(p) || !IsFinite(a) || !IsFinite(b)) return float.PositiveInfinity;
            return (float)SegmentDistanceSquared(p, a, b);
        }

        public static bool Contains(IReadOnlyList<GesturePoint> stroke, GesturePoint p, float radius)
        {
            if (!IsFinite(p) || !ValidStroke(stroke, radius)) return false;
            double radiusSquared = (double)radius * radius;
            if (stroke.Count == 1)
                return SegmentDistanceSquared(p, stroke[0], stroke[0]) <= radiusSquared;
            for (int i = 1; i < stroke.Count; i++)
                if (SegmentDistanceSquared(p, stroke[i - 1], stroke[i]) <= radiusSquared)
                    return true;
            return false;
        }

        /// <summary>Invalid input returns empty bounds that contain no point.</summary>
        public static PaintBounds Bounds(IReadOnlyList<GesturePoint> stroke, float radius)
        {
            if (!ValidStroke(stroke, radius)) return default;
            double minX = stroke[0].X, maxX = minX;
            double minY = stroke[0].Y, maxY = minY;
            for (int i = 1; i < stroke.Count; i++)
            {
                minX = Math.Min(minX, stroke[i].X);
                minY = Math.Min(minY, stroke[i].Y);
                maxX = Math.Max(maxX, stroke[i].X);
                maxY = Math.Max(maxY, stroke[i].Y);
            }
            return new PaintBounds(minX - radius, minY - radius, maxX + radius, maxY + radius);
        }

        internal static bool IsFinite(GesturePoint p) => IsFinite(p.X) && IsFinite(p.Y);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool ValidStroke(IReadOnlyList<GesturePoint> stroke, float radius)
        {
            if (stroke == null || stroke.Count == 0 || !IsFinite(radius) || radius <= 0f) return false;
            // Validate before testing coverage: an early hit cannot hide a bad later sample.
            for (int i = 0; i < stroke.Count; i++)
                if (!IsFinite(stroke[i])) return false;
            return true;
        }

        private static double SegmentDistanceSquared(GesturePoint p, GesturePoint a, GesturePoint b)
        {
            double dx = (double)b.X - a.X, dy = (double)b.Y - a.Y;
            double px = (double)p.X - a.X, py = (double)p.Y - a.Y;
            double lengthSquared = dx * dx + dy * dy;
            double t = lengthSquared == 0d ? 0d : (px * dx + py * dy) / lengthSquared;
            t = Math.Max(0d, Math.Min(1d, t));
            double x = px - t * dx, y = py - t * dy;
            return x * x + y * y;
        }
    }
}
