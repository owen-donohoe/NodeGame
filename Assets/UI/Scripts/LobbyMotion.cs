using System;

namespace NodeWar.Lobby
{
    /// <summary>The lobby's motion contract, shared by the stage and UI.</summary>
    public static class LobbyMotion
    {
        public const float PageSeconds = 0.350f;
        public const float LongSeconds = 0.420f;
        public const float OpenSeconds = 0.300f;
        public const float CloseSeconds = 0.140f;
        public const float PageTravel = 24f;
        public const float ClosedScale = 0.96f;
        public const float OpenScale = 0.92f;
        public const float FadeSeconds = 0.120f;
        public const float PageDamping = 0.9f;
        public const float PopDamping = 0.65f;
        public const float Frequency = 28f;

        // Exact damped oscillator: stable across long frames. Changing Target
        // preserves both position and velocity, including on reversal.
        public struct Spring
        {
            public float Position, Velocity, Target;
            public Spring(float position) { Position = Target = position; Velocity = 0f; }
            public bool Settled => Math.Abs(Position - Target) < 0.001f && Math.Abs(Velocity) < 0.01f;
            public void Step(float seconds, float damping = PageDamping)
            {
                if (seconds <= 0f) return;
                double a = damping * Frequency;
                double b = Frequency * Math.Sqrt(1.0 - damping * damping);
                double displacement = Position - Target;
                double c = (Velocity + a * displacement) / b;
                double sin = Math.Sin(b * seconds), cos = Math.Cos(b * seconds);
                double decay = Math.Exp(-a * seconds);
                Position = Target + (float)(decay * (displacement * cos + c * sin));
                Velocity = (float)(decay * ((-a * displacement + b * c) * cos + (-a * c - b * displacement) * sin));
            }
        }

        /// <summary>Coordinates in page widths; destinations attach beside the live strip.</summary>
        public sealed class PageStrip
        {
            public readonly float[] Slots = { float.NaN, float.NaN, float.NaN, float.NaN, float.NaN };
            public Spring Motion;
            public int Current { get; private set; }
            public bool Dragging { get; private set; }
            public bool Moving { get; private set; }
            private float dragOrigin;
            public void Reset(int page)
            {
                for (int i = 0; i < Slots.Length; i++) Slots[i] = float.NaN;
                Slots[page] = 0f; Current = page; Motion = new Spring(0f); Moving = Dragging = false;
            }
            public void Go(int page)
            {
                if (!Moving && !Dragging) Reset(Current);
                if (float.IsNaN(Slots[page]))
                {
                    int direction = Math.Sign(page - Current);
                    if (direction == 0) return;
                    float edge = direction > 0 ? (float)Math.Ceiling(Motion.Position) : (float)Math.Floor(Motion.Position);
                    if (Math.Abs(edge - Motion.Position) < 0.001f) edge += direction;
                    while (Occupied(edge)) edge += direction;
                    Slots[page] = edge;
                }
                Current = page; Motion.Target = Slots[page]; Dragging = false; Moving = true;
            }
            private bool Occupied(float slot)
            {
                for (int i = 0; i < Slots.Length; i++) if (Slots[i] == slot) return true;
                return false;
            }
            public void BeginDrag()
            {
                if (!Moving) Reset(Current);
                dragOrigin = Motion.Position; Dragging = true; Moving = false;
                for (int d = -1; d <= 1; d += 2)
                {
                    int neighbour = Current + d;
                    float slot = Slots[Current] + d;
                    if (neighbour >= 0 && neighbour < Slots.Length && float.IsNaN(Slots[neighbour]) && !Occupied(slot))
                        Slots[neighbour] = slot;
                }
            }
            public void Drag(float travel, float velocity)
            {
                float destination = dragOrigin + travel;
                float min = Math.Min(dragOrigin, Slots[Current] - (Current > 0 ? 1f : 0f));
                float max = Math.Max(dragOrigin, Slots[Current] + (Current < 4 ? 1f : 0f));
                float bounded = Math.Max(min, Math.Min(max, destination));
                Motion.Position = bounded + (destination - bounded) * 0.35f;
                Motion.Velocity = velocity * (bounded == destination ? 1f : 0.35f);
            }
            public void Release(int offset)
            {
                int page = Math.Max(0, Math.Min(4, Current + offset));
                // An interrupted long jump can occupy the neighbour's usual
                // slot. Attach that destination beyond it rather than dropping
                // the release or replacing a page that is still on screen.
                if (float.IsNaN(Slots[page])) { Go(page); return; }
                Current = page; Motion.Target = Slots[page]; Dragging = false; Moving = true;
            }
            public void Step(float seconds)
            {
                if (!Moving) return;
                Motion.Step(seconds);
                if (!Motion.Settled) return;
                Motion.Position = Motion.Target; Motion.Velocity = 0f; Moving = false;
            }
            public bool Visible(int page) => !float.IsNaN(Slots[page]) &&
                (Moving || Dragging ? Math.Abs(Slots[page] - Motion.Position) <= 1.001f : page == Current);
            public void Pair(out int from, out int to, out float progress)
            {
                from = to = Current;
                float lower = float.NegativeInfinity, upper = float.PositiveInfinity;
                for (int i = 0; i < Slots.Length; i++)
                {
                    float s = Slots[i];
                    if (s <= Motion.Position && s > lower) { lower = s; from = i; }
                    if (s >= Motion.Position && s < upper) { upper = s; to = i; }
                }
                // Extrapolate the edge segment so the camera and pills also
                // follow spring overshoot and the edge-page rubber band.
                if (float.IsNegativeInfinity(lower))
                {
                    from = to;
                    float next = float.PositiveInfinity;
                    for (int i = 0; i < Slots.Length; i++)
                        if (Slots[i] > Slots[from] && Slots[i] < next) { next = Slots[i]; to = i; }
                }
                if (float.IsPositiveInfinity(upper))
                {
                    to = from;
                    float previous = float.NegativeInfinity;
                    for (int i = 0; i < Slots.Length; i++)
                        if (Slots[i] < Slots[to] && Slots[i] > previous) { previous = Slots[i]; from = i; }
                }
                progress = from == to ? 0f : (Motion.Position - Slots[from]) / (Slots[to] - Slots[from]);
            }
        }

        public static float OutCubic(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }
        public static float InCubic(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * t;
        }
        public static int PillIndex(LobbyPageID page)
        {
            switch (page)
            {
                case LobbyPageID.Shop: return 0;
                case LobbyPageID.Workshop: return 1;
                case LobbyPageID.Home: return 2;
                case LobbyPageID.Social: return 3;
                case LobbyPageID.Guild: return 4;
                default: throw new ArgumentOutOfRangeException(nameof(page));
            }
        }
        // Moving right through the pills moves the content left.
        public static int ContentDirection(LobbyPageID from, LobbyPageID to) =>
            Math.Sign(PillIndex(from) - PillIndex(to));
    }
}
