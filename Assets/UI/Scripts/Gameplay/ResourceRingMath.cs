namespace NodeWar.UI
{
    /// <summary>
    /// Pure value -> ring presentation maths for the resource rings: how many
    /// of each ring's 10 segments are lit, and which named colour stop (plus
    /// the blend fraction, in the one range that blends) the current value
    /// falls in.
    ///
    /// No UnityEngine types, so dotnet/NodeWar.View.Tests can run it directly,
    /// the same reason ViewSide.cs lives where it does. The HSV shade applied
    /// per ring (middle darker/more saturated, inner darker/more saturated
    /// again) is a rendering detail and stays in ResourceRing.
    /// </summary>
    public static class ResourceRingMath
    {
        /// <summary>Segments per ring, and units per ring: outer 1-10, middle 11-20, inner 21-30.</summary>
        public const int SegmentsPerRing = 10;
        public const int RingCount = 3;

        /// <summary>
        /// Degrees a ring's ten segments sweep across. The rings are the upper
        /// half of a circle, flat side down, so this is 180 rather than a full
        /// circle's 360 - half the segments' angular width, same segment count.
        /// </summary>
        public const float SweepDegrees = 180f;

        /// <summary>
        /// Painter2D start angle for segment 0: the left end of the flat base
        /// (9 o'clock in the 0=right/90=down/180=left/270=up convention the
        /// ring is drawn in), so the sweep crosses the top (12 o'clock) at its
        /// midpoint and ends at the flat base's right end (3 o'clock).
        /// </summary>
        public const float StartDegrees = -180f;

        public const int StopCritical = 0; // v < 3, dark red
        public const int StopLow = 1;      // v < 5, red
        public const int StopWarn = 2;     // v < 8, yellow
        public const int StopOk = 3;       // v < 10, yellow-green
        public const int StopGood = 4;     // 10 <= v < 20, blends ok -> good
        public const int StopRich = 5;     // v >= 20, teal

        /// <summary>
        /// Lit segment count (0-10) for ring r, 0 = outer. Ring r covers units
        /// r*10+1 .. r*10+10; below that range it is unlit, above it every
        /// segment is lit and the value keeps counting past it.
        /// </summary>
        public static int LitSegments(int value, int ring)
        {
            int lit = value - ring * SegmentsPerRing;
            if (lit < 0) return 0;
            if (lit > SegmentsPerRing) return SegmentsPerRing;
            return lit;
        }

        /// <summary>
        /// Which named colour stop the current value falls in. StopGood spans
        /// values 10-19 and blends internally toward StopRich's colour; see
        /// <see cref="GoodBlendFraction"/>.
        /// </summary>
        public static int ColorStopIndex(int value)
        {
            if (value < 3) return StopCritical;
            if (value < 5) return StopLow;
            if (value < 8) return StopWarn;
            if (value < 10) return StopOk;
            if (value < 20) return StopGood;
            return StopRich;
        }

        /// <summary>
        /// 0 at value 10 (pure StopOk colour, continuous with the flat zone
        /// just below it) to 1 at value 19 (pure StopGood colour). Only
        /// meaningful while ColorStopIndex reports StopGood; clamped outside
        /// that range so a caller cannot misuse it.
        /// </summary>
        public static float GoodBlendFraction(int value)
        {
            if (value <= 10) return 0f;
            if (value >= 19) return 1f;
            return (value - 10) / 9f;
        }
    }
}
