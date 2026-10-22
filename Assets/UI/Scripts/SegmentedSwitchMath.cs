using System;

namespace NodeWar.Lobby
{
    /// <summary>Measured face bounds in track coordinates, independent of panel scale.</summary>
    public static class SegmentedSwitchMath
    {
        public readonly struct Target
        {
            public readonly float X, Width;
            public Target(float x, float width) { X = x; Width = width; }
        }

        public static Target ThumbTarget(float faceX, float faceWidth, float trackWidth)
        {
            float width = Math.Max(0, Math.Min(faceWidth, trackWidth));
            return new Target(Math.Max(0, Math.Min(faceX, trackWidth - width)), width);
        }
    }
}
