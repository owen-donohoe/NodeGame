using System;

namespace NodeWar.View
{
    /// <summary>Which form an indicator takes: its in-view form, or its edge form.</summary>
    public enum IndicatorZone { Inner, Edge }

    /// <summary>
    /// A rectangle in panel space, y down. The part of the screen the board is
    /// actually visible through, once the HUD's readouts and docks are taken off.
    /// </summary>
    public struct PlacementRect
    {
        public float xMin;
        public float yMin;
        public float xMax;
        public float yMax;

        public PlacementRect(float xMin, float yMin, float xMax, float yMax)
        {
            this.xMin = xMin;
            this.yMin = yMin;
            this.xMax = xMax;
            this.yMax = yMax;
        }

        public float Width => xMax - xMin;
        public float Height => yMax - yMin;
        public float CentreX => (xMin + xMax) * 0.5f;
        public float CentreY => (yMin + yMax) * 0.5f;
    }

    /// <summary>
    /// Where an indicator goes and which form it takes, as plain maths.
    ///
    /// Free of UnityEngine so dotnet/NodeWar.View.Tests can run it, like
    /// ViewSide. Everything is in panel space: x right, y down, in the
    /// PanelSettings reference units the HUD is laid out in.
    ///
    /// The rules, which are the whole design:
    ///
    /// - The centre of the board rect is where the player is looking. A subject
    ///   there takes its in-view form. Anywhere else, including on screen but
    ///   near the edge, it takes its edge form. The two boundaries differ
    ///   (enter at one fraction, leave at a larger one) so a subject sitting on
    ///   the line does not flicker between the forms every frame.
    /// - The edge form sits on the subject, clamped inside the rect. A subject
    ///   off screen therefore lands at the nearest point on the rect, and slides
    ///   in continuously as the camera pans toward it rather than jumping.
    /// - A subject behind the camera projects mirrored through the centre. It
    ///   is flipped back before clamping, or the arrow would point away from it.
    /// </summary>
    public static class IndicatorPlacement
    {
        /// <summary>
        /// Far enough out that a flipped behind-camera point always clamps to
        /// the rect's edge rather than landing inside it.
        /// </summary>
        private const float BehindPush = 10000f;

        /// <summary>True if the point is inside the centred box a fraction of the rect's size.</summary>
        public static bool InsideCentred(float x, float y, PlacementRect rect, float fraction)
        {
            float halfW = rect.Width * fraction * 0.5f;
            float halfH = rect.Height * fraction * 0.5f;

            return Math.Abs(x - rect.CentreX) <= halfW &&
                   Math.Abs(y - rect.CentreY) <= halfH;
        }

        /// <summary>
        /// Inner or Edge, with hysteresis. Something already Inner stays Inner
        /// until it leaves the larger exitFraction box; something at the Edge
        /// becomes Inner only once inside the smaller enterFraction box. A point
        /// behind the camera is always Edge.
        /// </summary>
        public static IndicatorZone Classify(float x, float y, bool behind, PlacementRect rect,
                                             float enterFraction, float exitFraction,
                                             IndicatorZone previous)
        {
            if (behind) return IndicatorZone.Edge;

            float fraction = previous == IndicatorZone.Inner
                ? Math.Max(enterFraction, exitFraction)
                : enterFraction;

            return InsideCentred(x, y, rect, fraction) ? IndicatorZone.Inner : IndicatorZone.Edge;
        }

        /// <summary>
        /// The edge form's position: the subject, clamped into the rect inset by
        /// margin, which is half the indicator's size so it never hangs off.
        ///
        /// clamped is true when the subject is outside that inset rect, which is
        /// when the indicator is not on top of it and needs an arrow saying where
        /// it is. angleDegrees is that arrow's direction, 0 pointing right and 90
        /// pointing down, matching a UI Toolkit rotation in a y-down panel.
        /// </summary>
        public static void EdgePosition(float x, float y, bool behind, PlacementRect rect, float margin,
                                        out float px, out float py, out bool clamped, out float angleDegrees)
        {
            float cx = rect.CentreX;
            float cy = rect.CentreY;

            if (behind)
            {
                // Mirror back through the centre, then push far out along that
                // direction so it clamps onto the edge. A point dead on the
                // centre has no direction at all; behind the camera is below
                // it on screen, so send it down.
                float dx = cx - x;
                float dy = cy - y;
                float length = (float)Math.Sqrt(dx * dx + dy * dy);

                if (length < 0.0001f)
                {
                    dx = 0f;
                    dy = 1f;
                    length = 1f;
                }

                x = cx + dx / length * BehindPush;
                y = cy + dy / length * BehindPush;
            }

            float left = rect.xMin + margin;
            float right = rect.xMax - margin;
            float top = rect.yMin + margin;
            float bottom = rect.yMax - margin;

            // A rect too small for the margin collapses to its centre line
            // rather than inverting.
            if (left > right) left = right = cx;
            if (top > bottom) top = bottom = cy;

            px = Clamp(x, left, right);
            py = Clamp(y, top, bottom);

            clamped = behind || px != x || py != y;
            angleDegrees = clamped
                ? Direction(px, py, x, y)
                : 0f;
        }

        /// <summary>Direction from the drawn icon to its target in a y-down panel.</summary>
        public static float Direction(float iconX, float iconY, float targetX, float targetY)
        {
            return (float)(Math.Atan2(targetY - iconY, targetX - iconX) * (180.0 / Math.PI));
        }

        /// <summary>
        /// Merges edge indicators that would overlap and caps how many show.
        ///
        /// Highest priority places first, ties broken by lower index, so the
        /// result never depends on list order. Each one either becomes a leader
        /// (leaderOf[i] == i), merges into a leader already placed within
        /// mergeRadius (leaderOf[i] == that leader), or is dropped because
        /// maxShown leaders already exist (leaderOf[i] == -1). A merge always
        /// lands on the higher-priority one, so the icon that stays is the one
        /// that matters most.
        ///
        /// order is scratch space of at least count. Returns the number of leaders.
        /// </summary>
        public static int Cluster(float[] xs, float[] ys, int[] priority, int count,
                                  float mergeRadius, int maxShown, int[] order, int[] leaderOf)
        {
            for (int i = 0; i < count; i++) order[i] = i;

            // Insertion sort: a handful of items, and a total order by
            // construction, so the same inputs always cluster the same way.
            for (int i = 1; i < count; i++)
            {
                int item = order[i];
                int j = i - 1;
                while (j >= 0 && Before(item, order[j], priority))
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = item;
            }

            float radiusSq = mergeRadius * mergeRadius;
            int leaders = 0;

            for (int k = 0; k < count; k++)
            {
                int i = order[k];
                leaderOf[i] = -1;

                for (int m = 0; m < k; m++)
                {
                    int other = order[m];
                    if (leaderOf[other] != other) continue;

                    float dx = xs[i] - xs[other];
                    float dy = ys[i] - ys[other];
                    if (dx * dx + dy * dy <= radiusSq)
                    {
                        leaderOf[i] = other;
                        break;
                    }
                }

                if (leaderOf[i] >= 0) continue;

                if (leaders < maxShown)
                {
                    leaderOf[i] = i;
                    leaders++;
                }
            }

            return leaders;
        }

        private static bool Before(int a, int b, int[] priority)
        {
            if (priority[a] != priority[b]) return priority[a] > priority[b];
            return a < b;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
