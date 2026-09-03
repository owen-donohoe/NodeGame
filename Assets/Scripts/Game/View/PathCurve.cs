using System.Collections.Generic;
using UnityEngine;

namespace NodeWar.View
{
    /// <summary>
    /// Turns a villager node path into the rounded curve that both the sprite and
    /// the drawn route follow.
    ///
    /// The board is a 4-neighbour grid, so a path is a staircase of axis-aligned
    /// steps and a straight line along it says nothing about the next turn until
    /// the villager reaches the corner. Rounding the corners is what makes the
    /// direction of travel read early.
    ///
    /// Corner rounding rather than Chaikin, deliberately. Chaikin -- which
    /// LassoGeometry uses on the closed lasso polygon -- discards the waypoints
    /// entirely, and the waypoints are load-bearing here: the simulation crosses
    /// each leg in exactly edgeWeight * moveSpeedTicks ticks, so the curve has to
    /// be parameterised per leg or the sprite drifts off the tick it is supposed
    /// to arrive on. Replacing only the corner with a quadratic Bezier keeps
    /// every node on the curve as a leg boundary while still banking the turn.
    ///
    /// The two ends are pinned. A path starts where the villager is and ends on
    /// the destination node, and neither may be rounded away.
    ///
    /// Static scratch buffers, like LassoGeometry: this is called per villager
    /// per frame and must not allocate. Unity main thread only, not reentrant --
    /// build, then read, before building again.
    /// </summary>
    public static class PathCurve
    {
        private static readonly List<Vector3> points = new List<Vector3>();
        private static readonly List<int> legStarts = new List<int>();

        public static int PointCount => points.Count;
        public static Vector3 GetPoint(int index) => points[index];

        /// <summary>Number of legs, one per edge of the original path.</summary>
        public static int LegCount => legStarts.Count;

        /// <summary>
        /// Builds the curve through a run of waypoints.
        ///
        /// cornerRadius is in world units and is clamped per corner to half the
        /// shorter adjacent leg, so a tight path rounds less rather than
        /// overshooting into the neighbouring corner.
        ///
        /// cornerSegments is rounded up to an even number so every corner has an
        /// exact midpoint vertex. That vertex is the leg boundary, and the sprite
        /// has to be able to land on it precisely.
        /// </summary>
        public static void Build(IReadOnlyList<Vector3> waypoints, float cornerRadius, int cornerSegments)
        {
            points.Clear();
            legStarts.Clear();

            if (waypoints == null || waypoints.Count == 0) return;

            if (waypoints.Count == 1)
            {
                points.Add(waypoints[0]);
                return;
            }

            if (cornerSegments < 2) cornerSegments = 2;
            if ((cornerSegments & 1) == 1) cornerSegments++;
            int half = cornerSegments / 2;

            int last = waypoints.Count - 1;

            points.Add(waypoints[0]);
            legStarts.Add(0);   // leg 0 begins at the pinned start

            for (int i = 1; i < last; i++)
            {
                Vector3 previous = waypoints[i - 1];
                Vector3 corner = waypoints[i];
                Vector3 next = waypoints[i + 1];

                Vector3 toPrevious = previous - corner;
                Vector3 toNext = next - corner;

                float inLength = toPrevious.magnitude;
                float outLength = toNext.magnitude;

                // Degenerate waypoints: nothing to round, and normalising would
                // divide by zero. Treat the node as a plain vertex.
                if (inLength < 0.0001f || outLength < 0.0001f)
                {
                    legStarts.Add(points.Count);
                    points.Add(corner);
                    continue;
                }

                float radius = cornerRadius;
                if (radius > inLength * 0.5f) radius = inLength * 0.5f;
                if (radius > outLength * 0.5f) radius = outLength * 0.5f;

                Vector3 entry = corner + (toPrevious / inLength) * radius;
                Vector3 exit = corner + (toNext / outLength) * radius;

                // A 180 degree turn -- which a reversal produces when the new
                // route leaves by the opposite neighbour -- puts entry and exit
                // on opposite rays, and the Bezier is then a straight run through
                // the node. That is correct and needs no special case; it only
                // must not be reached by way of a zero-length normalise, which
                // the guard above rules out.
                for (int s = 0; s < half; s++)
                {
                    points.Add(Quadratic(entry, corner, exit, (float)s / cornerSegments));
                }

                // The corner midpoint is the boundary between the two legs, so
                // the sprite is here exactly as one leg completes and the next
                // begins.
                legStarts.Add(points.Count);
                points.Add(Quadratic(entry, corner, exit, 0.5f));

                for (int s = half + 1; s <= cornerSegments; s++)
                {
                    points.Add(Quadratic(entry, corner, exit, (float)s / cornerSegments));
                }
            }

            points.Add(waypoints[last]);
        }

        /// <summary>
        /// Where the sprite sits at fraction t through leg legIndex, measured by
        /// length along that leg so it moves at a steady pace and arrives on the
        /// tick the simulation says it does.
        /// </summary>
        public static Vector3 PositionOnLeg(int legIndex, float t)
        {
            if (points.Count == 0) return Vector3.zero;
            if (legIndex < 0 || legIndex >= legStarts.Count) return points[points.Count - 1];

            int from = legStarts[legIndex];
            int to = LegEnd(legIndex);

            if (to <= from) return points[from];

            t = Mathf.Clamp01(t);

            float total = LegLength(from, to);
            if (total <= 0.0001f) return points[from];

            float wanted = total * t;
            float walked = 0f;

            for (int i = from; i < to; i++)
            {
                float step = (points[i + 1] - points[i]).magnitude;
                if (walked + step >= wanted)
                {
                    float within = step > 0.0001f ? (wanted - walked) / step : 0f;
                    return Vector3.Lerp(points[i], points[i + 1], within);
                }
                walked += step;
            }

            return points[to];
        }

        /// <summary>
        /// Copies the curve from a point part-way along a leg through to the end:
        /// the stretch a villager has left to walk. The first entry is exactly
        /// what PositionOnLeg returns, so the drawn route starts on the sprite
        /// rather than near it.
        /// </summary>
        public static void AppendRemainder(int legIndex, float t, List<Vector3> destination)
        {
            AppendRemainder(legIndex, t, int.MaxValue, destination);
        }

        /// <summary>
        /// The same, stopped after maxLegs legs.
        ///
        /// This is a real truncation, not a fade: nothing past the cut is written
        /// at all. Drawing the whole route and tapering its alpha to zero would
        /// still put the destination on screen, recoverable by anyone who raises
        /// their brightness or levels a screenshot -- so an opponent route that
        /// is meant to withhold its endpoint has to lose the geometry, and the
        /// fade only shapes what remains.
        ///
        /// The cut lands on a leg boundary, which is the midpoint of a rounded
        /// corner, so a truncated route ends banking into its next turn rather
        /// than at a node.
        /// </summary>
        public static void AppendRemainder(int legIndex, float t, int maxLegs, List<Vector3> destination)
        {
            if (destination == null) return;
            destination.Clear();

            if (points.Count == 0) return;
            if (maxLegs < 1) return;

            if (legIndex < 0) legIndex = 0;
            if (legIndex >= legStarts.Count)
            {
                destination.Add(points[points.Count - 1]);
                return;
            }

            destination.Add(PositionOnLeg(legIndex, t));

            int from = legStarts[legIndex];
            int to = LegEnd(legIndex);

            float wanted = LegLength(from, to) * Mathf.Clamp01(t);
            float walked = 0f;
            int firstWhole = to;

            for (int i = from; i < to; i++)
            {
                float step = (points[i + 1] - points[i]).magnitude;
                if (walked + step > wanted)
                {
                    firstWhole = i + 1;
                    break;
                }
                walked += step;
            }

            // At the very end of a leg the walk above lands exactly on the point
            // PositionOnLeg already returned. Skip it rather than emitting a
            // zero-length segment, which a dashed material renders as a blot.
            if (firstWhole < points.Count &&
                (points[firstWhole] - destination[0]).sqrMagnitude < 0.000001f)
            {
                firstWhole++;
            }

            // Where the copy stops. maxLegs counts legs from the one being
            // walked, so revealing "two nodes ahead" is two legs, and the cut
            // lands on the far boundary of the second.
            int lastLeg;
            if (maxLegs >= legStarts.Count - legIndex) lastLeg = legStarts.Count - 1;
            else lastLeg = legIndex + maxLegs - 1;

            int stopAt = LegEnd(lastLeg);

            for (int i = firstWhole; i <= stopAt && i < points.Count; i++)
                destination.Add(points[i]);
        }

        private static float LegLength(int from, int to)
        {
            float total = 0f;
            for (int i = from; i < to; i++)
                total += (points[i + 1] - points[i]).magnitude;
            return total;
        }

        private static int LegEnd(int legIndex)
        {
            return (legIndex + 1 < legStarts.Count) ? legStarts[legIndex + 1] : points.Count - 1;
        }

        private static Vector3 Quadratic(Vector3 a, Vector3 control, Vector3 b, float t)
        {
            float inverse = 1f - t;
            return (inverse * inverse) * a + (2f * inverse * t) * control + (t * t) * b;
        }
    }
}
