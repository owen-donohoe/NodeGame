using System;

namespace NodeWar.Lobby
{
    public enum SwipeAxis { Pending, Horizontal, Vertical }

    public static class SwipeDecision
    {
        public static SwipeAxis Lock(float dx, float dy)
        {
            if (!LobbyDragMath.PassedThreshold(dx, dy)) return SwipeAxis.Pending;
            return Math.Abs(dx) > 1.5f * Math.Abs(dy) ? SwipeAxis.Horizontal : SwipeAxis.Vertical;
        }

        /// <summary>Returns the adjacent page offset, or zero. Flicks need 20px at 600px/s within 250ms.</summary>
        public static int Decide(float dx, float dy, long elapsedMs, bool overlayOpen, bool noSwipe,
            SwipeAxis axis = SwipeAxis.Pending)
        {
            if (overlayOpen || noSwipe || float.IsNaN(dx) || float.IsNaN(dy) || float.IsInfinity(dx) || float.IsInfinity(dy)) return 0;
            if (axis == SwipeAxis.Pending) axis = Lock(dx, dy);
            if (axis != SwipeAxis.Horizontal || Math.Abs(dx) <= 1.5f * Math.Abs(dy)) return 0;
            bool flick = Math.Abs(dx) >= 20f && elapsedMs > 0 && elapsedMs <= 250 && Math.Abs(dx) * 1000 >= 600 * elapsedMs;
            if (Math.Abs(dx) < 60f && !flick) return 0;
            return dx < 0 ? 1 : -1;
        }
    }
}
