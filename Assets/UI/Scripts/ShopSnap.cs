using System;

namespace NodeWar.Lobby
{
    /// <summary>Stall-unit motion independent of frame rate, layout and Unity.</summary>
    public sealed class ShopSnap
    {
        public const int StallCount = 5;
        public const float VelocityThreshold = 0.18f;
        private const float Decay = 5f;
        public float Position { get; private set; }
        public float Velocity { get; private set; }
        public bool Dragging { get; private set; }
        public bool Moving { get; private set; }
        private float direction;
        private float target;

        public static int StallIndex(string name)
        {
            switch (name?.ToLowerInvariant())
            {
                case "daily": return 0;
                case "crates": return 1;
                case "weekly": return 2;
                case "bundle": return 3;
                case "bank":
                case "coins":
                case "laurels": return 4;
                default: return -1;
            }
        }

        public static float ScreenAnchor(int stall) => stall == 0 ? 0f : 0.5f;
        public static float Target(float position, float direction)
        {
            float nearest = direction > 0f ? (float)Math.Ceiling(position - 0.001f)
                : direction < 0f ? (float)Math.Floor(position + 0.001f)
                : (float)Math.Floor(position + 0.5f);
            return Clamp(nearest);
        }

        public void BeginDrag()
        {
            Dragging = true;
            Moving = false;
            Velocity = direction = 0f;
        }

        public void Drag(float deltaStalls, float seconds)
        {
            if (!Dragging || seconds <= 0f) return;
            float before = Position;
            Position = Clamp(Position + deltaStalls);
            Velocity = (Position - before) / seconds;
            if (Math.Abs(Velocity) > 0.001f) direction = Math.Sign(Velocity);
        }

        public void Release()
        {
            if (!Dragging) return;
            Dragging = false;
            Moving = true;
            target = Target(Position, direction);
        }

        public void ScrollTo(float stall)
        {
            Dragging = false;
            Moving = true;
            Velocity = direction = 0f;
            target = Clamp(stall);
        }

        public void Stop()
        {
            Dragging = Moving = false;
            Velocity = 0f;
        }

        public void Step(float seconds)
        {
            if (Dragging || !Moving || seconds <= 0f) return;
            if (Math.Abs(Velocity) >= VelocityThreshold)
            {
                float decay = (float)Math.Exp(-Decay * seconds);
                Position = Clamp(Position + Velocity * (1f - decay) / Decay);
                Velocity *= decay;
                if (Position <= 0f || Position >= StallCount - 1) Velocity = 0f;
                target = Target(Position, direction);
            }
            else
            {
                Velocity = 0f;
                Position += (target - Position) * (1f - (float)Math.Exp(-12f * seconds));
                if (Math.Abs(target - Position) < 0.001f)
                {
                    Position = target;
                    Moving = false;
                }
            }
        }

        private static float Clamp(float value) => Math.Max(0f, Math.Min(StallCount - 1, value));
    }
}
