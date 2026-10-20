using System;

namespace NodeWar.Lobby
{
    /// <summary>Pointer travel in panel pixels; velocities in pixels per second.</summary>
    public static class LobbyDragMath
    {
        public const float Threshold = 8f;
        public static bool PassedThreshold(float dx, float dy) =>
            Math.Max(Math.Abs(dx), Math.Abs(dy)) >= Threshold;

        public static float ClampOffset(float offset, float contentSize, float viewportSize) =>
            Math.Max(0, Math.Min(Math.Max(0, contentSize - viewportSize), offset));

        public static float Elastic(float position, float max, float viewport)
        {
            float bound = Math.Max(0, Math.Min(max, position));
            float excess = position - bound;
            float limit = Math.Max(1, viewport * 0.3f);
            return bound + excess / (1 + Math.Abs(excess) / limit);
        }

        public static float Velocity(float delta, long elapsedMs) => elapsedMs <= 0 ? 0 :
            Math.Max(-5000, Math.Min(5000, delta * 1000 / elapsedMs));
        public static float ReleaseVelocity(float velocity, long idleMs) => idleMs > 100 ? 0 : velocity;

        public static void Step(ref float position, ref float velocity, float max, float seconds)
        {
            if (seconds <= 0) return;
            max = Math.Max(0, max);
            float bound = Math.Max(0, Math.Min(max, position));
            if (position != bound)
            {
                // Exact critically damped return, independent of scheduler cadence.
                const float frequency = 14;
                float displacement = position - bound;
                float impulse = velocity + frequency * displacement;
                float decay = (float)Math.Exp(-frequency * seconds);
                position = bound + (displacement + impulse * seconds) * decay;
                velocity = (velocity - frequency * impulse * seconds) * decay;
                if (Math.Abs(position - bound) < 0.1f && Math.Abs(velocity) < 1)
                { position = bound; velocity = 0; }
            }
            else
            {
                const float friction = 5;
                float decay = (float)Math.Exp(-friction * seconds);
                position += velocity * (1 - decay) / friction;
                velocity *= decay;
                if (Math.Abs(velocity) < 1) velocity = 0;
            }
        }
    }
}
