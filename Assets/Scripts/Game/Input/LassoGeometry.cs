using System.Collections.Generic;
using UnityEngine;

namespace NodeWar.Input
{
    /// <summary>
    /// Screen-space polygon maths for freeform lasso selection.
    ///
    /// Pure and static -- no Unity lifecycle, no scene dependency -- so it can be
    /// exercised from the in-editor debug overlay without entering play mode.
    ///
    /// Screen space, not the ground plane, is deliberate. The old circle
    /// projected its centre and radius onto Y=0, which is only coherent for a
    /// circle. A freeform stroke drawn under a tilted perspective camera does not
    /// map to a well-formed ground polygon -- near the horizon a few pixels of
    /// stroke cover unbounded world distance, and the projected shape can
    /// self-intersect even when the stroke did not. Villagers are billboarded
    /// sprites, so testing where they *appear* is also what the player means.
    /// </summary>
    public static class LassoGeometry
    {
        /// <summary>
        /// Appends a point if it is far enough from the last one.
        /// Returns true if the point was recorded.
        ///
        /// Decimating on the way in keeps the polygon bounded without a
        /// post-pass; a slow careful stroke would otherwise record one vertex
        /// per frame and reach thousands.
        /// </summary>
        public static bool TryAppend(List<Vector2> points, Vector2 candidate,
                                     float minSpacingPx, int maxPoints)
        {
            if (points == null) return false;
            if (points.Count >= maxPoints) return false;

            if (points.Count > 0)
            {
                Vector2 last = points[points.Count - 1];
                if ((candidate - last).sqrMagnitude < minSpacingPx * minSpacingPx)
                    return false;
            }

            points.Add(candidate);
            return true;
        }

        /// <summary>
        /// Unsigned area of the polygon via the shoelace formula.
        ///
        /// Unsigned because a lasso drawn clockwise and one drawn
        /// counter-clockwise are the same intent; only the magnitude is a
        /// meaningful "is this a real shape" test.
        ///
        /// This is an approximation, and knowingly so. A stroke that loops
        /// inside itself -- a circle, then a smaller circle within it in the
        /// same direction -- accumulates both loops here, reporting more area
        /// than the region it encloses. That is the safe direction for the only
        /// thing this value is used for: a gate that rejects accidental
        /// scribbles. Over-reporting lets a large deliberate stroke through,
        /// which is what should happen anyway.
        ///
        /// Do not use it as a measure of what will actually be selected --
        /// Contains is the authority on that, and under nonzero winding an
        /// inner loop adds nothing to the selected region even though it adds
        /// to this figure.
        /// </summary>
        public static float Area(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 3) return 0f;

            float sum = 0f;
            int count = points.Count;

            for (int i = 0; i < count; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % count];   // implicit closing edge
                sum += (a.x * b.y) - (b.x * a.y);
            }

            return Mathf.Abs(sum) * 0.5f;
        }

        /// <summary>
        /// True if the stroke encloses enough area to be treated as a selection
        /// shape. A stroke that fails this must leave the selection untouched
        /// rather than clearing it -- a long press on nothing is a no-op.
        /// </summary>
        public static bool IsValid(IReadOnlyList<Vector2> points, float minAreaSqPx)
        {
            if (points == null || points.Count < 3) return false;
            return Area(points) >= minAreaSqPx;
        }

        /// <summary>
        /// Nonzero-winding point-in-polygon test.
        ///
        /// Nonzero rather than even-odd, deliberately. Under even-odd, a stroke
        /// that loops inside itself -- draw a circle, then a smaller circle
        /// within it -- punches the inner disc back out of the selection. The
        /// enclosed area grows while the selection shrinks, which is the exact
        /// opposite of what the player just did with their finger.
        ///
        /// Under nonzero winding an inner loop in the same direction winds to 2
        /// and stays selected, so re-tracing inside a shape is harmless. That is
        /// the behaviour the "draw a bigger lasso if you want a bigger
        /// selection" model needs: strokes only ever add.
        ///
        /// A deliberate figure-eight has lobes of opposite winding (+1 and -1);
        /// both are nonzero, so both select. Only the exact zero case -- a lobe
        /// retraced backwards over itself -- drops out, which is a stroke no
        /// player makes on purpose.
        ///
        /// The polygon is treated as implicitly closed; no duplicate final point
        /// is required.
        /// </summary>
        public static bool Contains(IReadOnlyList<Vector2> points, Vector2 p)
        {
            if (points == null || points.Count < 3) return false;

            int winding = 0;
            int count = points.Count;

            for (int i = 0; i < count; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % count];   // implicit closing edge

                if (a.y <= p.y)
                {
                    // Upward crossing with p strictly left of the edge.
                    if (b.y > p.y && IsLeft(a, b, p) > 0f)
                        winding++;
                }
                else
                {
                    // Downward crossing with p strictly right of the edge.
                    if (b.y <= p.y && IsLeft(a, b, p) < 0f)
                        winding--;
                }
            }

            return winding != 0;
        }

        /// <summary>
        /// &gt;0 if p is left of the directed line a-&gt;b, &lt;0 if right, 0 if collinear.
        /// The half-open comparisons in Contains keep a vertex lying exactly on
        /// the test ray from being counted twice.
        /// </summary>
        private static float IsLeft(Vector2 a, Vector2 b, Vector2 p)
        {
            return (b.x - a.x) * (p.y - a.y) - (p.x - a.x) * (b.y - a.y);
        }

        /// <summary>
        /// Screen-space axis-aligned bounds of the stroke. Used to reject
        /// candidates cheaply before the full containment test.
        /// </summary>
        public static Rect Bounds(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0) return Rect.zero;

            float minX = points[0].x, maxX = points[0].x;
            float minY = points[0].y, maxY = points[0].y;

            for (int i = 1; i < points.Count; i++)
            {
                Vector2 p = points[i];
                if (p.x < minX) minX = p.x; else if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y; else if (p.y > maxY) maxY = p.y;
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
