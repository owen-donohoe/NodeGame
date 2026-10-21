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
            return LobbyMotion.RubberBand(position, 0, Math.Max(0, max), viewport);
        }

        public static float Velocity(float delta, long elapsedMs) => elapsedMs <= 0 ? 0 :
            Math.Max(-5000, Math.Min(5000, delta * 1000 / elapsedMs));
        public static float ReleaseVelocity(float velocity, long idleMs) => idleMs > 100 ? 0 : velocity;

        public static void Step(ref float position, ref float velocity, float max, float seconds)
        {
            if (seconds <= 0) return;
            max = Math.Max(0, max);
            float bound = Math.Max(0, Math.Min(max, position));
            if (LobbyMotion.Reduced)
            { position = bound; velocity = 0; return; }
            if (position != bound)
                LobbyMotion.ReturnToEdge(ref position, ref velocity, bound, seconds, 0.1f);
            else
            {
                const float friction = 5;
                float decay = (float)Math.Exp(-friction * seconds);
                float next = position + velocity * (1 - decay) / friction;
                if (next < 0 || next > max)
                {
                    // Integrate exactly to the crossing before dissipating the edge impact.
                    float edge = next < 0 ? 0 : max;
                    float hit = (float)(-Math.Log(1 - friction * (edge - position) / velocity) / friction);
                    velocity *= (float)Math.Exp(-friction * hit) * 0.15f;
                    position = edge;
                    LobbyMotion.ReturnToEdge(ref position, ref velocity, edge, Math.Max(0, seconds - hit), 0.1f);
                }
                else
                { position = next; velocity *= decay; if (Math.Abs(velocity) < 1) velocity = 0; }
            }
        }
    }
}
