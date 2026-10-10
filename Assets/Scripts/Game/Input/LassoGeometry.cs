using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NodeWar.Input
{
    /// <summary>Compatibility adapter for the recorded screen-space paint stroke.</summary>
    public static class LassoGeometry
    {
        public static bool TryAppend(List<Vector2> points, Vector2 candidate,
                                     float minSpacingPx, int maxPoints)
        {
            if (points == null || points.Count >= maxPoints) return false;
            if (points.Count > 0 && (candidate - points[points.Count - 1]).sqrMagnitude
                < minSpacingPx * minSpacingPx) return false;
            points.Add(candidate);
            return true;
        }

        public static bool Contains(IReadOnlyList<Vector2> points, Vector2 p, float radiusPx) =>
            PaintGeometry.Contains(new PointAdapter(points), new GesturePoint(p.x, p.y), radiusPx);

        public static PaintBounds Bounds(IReadOnlyList<Vector2> points, float radiusPx) =>
            PaintGeometry.Bounds(new PointAdapter(points), radiusPx);

        public static bool IsValid(IReadOnlyList<Vector2> points, float radiusPx) =>
            Bounds(points, radiusPx).IsValid;

        // No copied or smoothed points: membership sees the capture verbatim.
        private readonly struct PointAdapter : IReadOnlyList<GesturePoint>
        {
            private readonly IReadOnlyList<Vector2> points;
            public PointAdapter(IReadOnlyList<Vector2> points) { this.points = points; }
            public int Count => points == null ? 0 : points.Count;
            public GesturePoint this[int index] => new GesturePoint(points[index].x, points[index].y);
            public IEnumerator<GesturePoint> GetEnumerator()
            {
                for (int i = 0; i < Count; i++) yield return this[i];
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
