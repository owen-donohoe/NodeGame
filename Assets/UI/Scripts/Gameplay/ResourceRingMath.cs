namespace NodeWar.UI
{
    /// <summary>
    /// Pure value -> ring presentation maths for the resource rings: how many
    /// of each ring's 10 segments are lit, and which named colour stop (plus
    /// the blend fraction, in the one range that blends) the current value
    /// falls in. Then the progress maths both animations are built from:
    /// how much of one segment a fractional value covers, the spend ghost's
    /// catch-up curve, and the gain sweep's duration and ease.
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

        /// <summary>
        /// How much of one segment a value covers: 0 for untouched, 1 for
        /// whole, a fraction for the one segment the value is partway
        /// through. Ring r's segment s spans values r*10+s .. r*10+s+1, so
        /// 4.3 fills the outer ring's segments 0-3 whole and segment 4 by
        /// 0.3.
        ///
        /// This is the ring's whole notion of "progress", and both animations
        /// are built out of it: the lit arc runs 0 -> SegmentFraction(shown),
        /// and the white ghost left by a spend runs SegmentFraction(real) ->
        /// SegmentFraction(ghost). Because it is asked per segment rather
        /// than per ring, the gaps between segments survive - a gain of two
        /// sweeps in as two separate sections rather than one merged arc.
        /// </summary>
        public static float SegmentFraction(float value, int ring, int segment)
        {
            float local = value - ring * SegmentsPerRing - segment;
            if (local <= 0f) return 0f;
            if (local >= 1f) return 1f;
            return local;
        }

        /// <summary>
        /// The spend ghost creeps this far into the gap - 6% - over
        /// <see cref="GhostHoldSeconds"/>, then covers the rest over
        /// <see cref="GhostCatchSeconds"/>. Not zero, because a ghost frozen
        /// dead still for six tenths of a second reads as a rendering fault
        /// rather than a beat.
        /// </summary>
        public const float GhostHoldFraction = 0.06f;
        public const float GhostHoldSeconds = 0.6f;
        public const float GhostCatchSeconds = 0.55f;
        public const float GhostTotalSeconds = GhostHoldSeconds + GhostCatchSeconds;

        /// <summary>
        /// How far the white ghost has closed on the real value, 0 at the
        /// moment of the spend to 1 once it has caught up. The shape is the
        /// breach wall's, in a curve rather than a USS transition because
        /// Painter2D cannot be transitioned: a near-still hold, then fast
        /// to slow (ease-out cubic) into the real value.
        /// </summary>
        public static float GhostFraction(float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f) return 0f;

            if (elapsedSeconds < GhostHoldSeconds)
                return GhostHoldFraction * (elapsedSeconds / GhostHoldSeconds);

            float t = (elapsedSeconds - GhostHoldSeconds) / GhostCatchSeconds;
            if (t >= 1f) return 1f;

            float inverse = 1f - t;
            float eased = 1f - inverse * inverse * inverse;
            return GhostHoldFraction + (1f - GhostHoldFraction) * eased;
        }

        /// <summary>
        /// How long a gain sweeps for. Proportional to the units gained so a
        /// single point is a flick and a run of five reads as five, but
        /// clamped at both ends: below the floor the sweep is a pop, and
        /// above the ceiling a windfall of twenty would hold the eye longer
        /// than the thing it is reporting is worth.
        /// </summary>
        public const float FillSecondsPerUnit = 0.11f;
        public const float FillSecondsMin = 0.16f;
        public const float FillSecondsMax = 0.45f;

        public static float FillSeconds(float units)
        {
            float seconds = units * FillSecondsPerUnit;
            if (seconds < FillSecondsMin) return FillSecondsMin;
            if (seconds > FillSecondsMax) return FillSecondsMax;
            return seconds;
        }

        /// <summary>Ease-out quad, 0 to 1, for the gain sweep.</summary>
        public static float FillEase(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            float inverse = 1f - t;
            return 1f - inverse * inverse;
        }
    }
}
