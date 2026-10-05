using System;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>
    /// The resource HUD's rules and animation maths with no UnityEngine, so
    /// dotnet/NodeWar.View.Tests runs them: when a resource counts as full, the
    /// pulse and the white wave that play while it is, the segmented bar metal is
    /// drawn as, whether metal is shown at all, and how big a semicircle is for the
    /// width it is given. ResourceRing and ResourceBar only draw what this says.
    ///
    /// Presentation only. Nothing here is read back by the simulation.
    /// </summary>
    public static class ResourceHudMath
    {
        // ---- caps

        public const int DefaultFoodCap = 30;
        public const int DefaultMaterialsCap = 30;
        public const int DefaultMetalCap = 10;
        public const int DefaultMagicCap = 10;

        /// <summary>A cap of zero or less is "no cap known"; nothing is ever full against it.</summary>
        public static bool IsFull(int value, int cap)
        {
            return cap > 0 && value >= cap;
        }

        // ---- metal visibility

        /// <summary>
        /// The arena (0-based, as RankRecord.Arena counts) from which metal is part of the
        /// game for the player. A placeholder: it is one number so it can be tuned in one place.
        /// </summary>
        public const int MetalArena = 2;
        public const int MagicArena = 3;

        /// <summary>
        /// Metal is hidden at first and shows once the player's arena reaches
        /// <see cref="MetalArena"/>, or whenever they actually hold some.
        /// </summary>
        public static bool MetalVisible(int arena, int metal, bool debugOverride = false)
        {
            return debugOverride || arena >= MetalArena || metal > 0;
        }

        public static bool MagicVisible(int arena, bool debugOverride = false)
        {
            return debugOverride || arena >= MagicArena;
        }

        /// <summary>Single future-source hook. Magic is currently display-only.</summary>
        public static int DisplayOnlyMagicAmount() { return 0; }

        // ---- the metal bar

        /// <summary>How much of bar segment <paramref name="segment"/> a value covers, 0..1.</summary>
        public static float BarSegmentFraction(float value, int segment)
        {
            float local = value - segment;
            if (local <= 0f) return 0f;
            if (local >= 1f) return 1f;
            return local;
        }

        /// <summary>Overall fill, 0..1, for a value against a cap. A bad cap reads as empty.</summary>
        public static float BarFill(int value, int cap)
        {
            if (cap <= 0 || value <= 0) return 0f;
            if (value >= cap) return 1f;
            return (float)value / cap;
        }

        // ---- semicircle size

        /// <summary>Ring thickness for a semicircle this wide: grows with it, within limits.</summary>
        public static float RingThickness(float width)
        {
            float t = width * 0.07f;
            if (t < 8f) return 8f;
            if (t > 20f) return 20f;
            return t;
        }

        /// <summary>The gap between rings, a fixed share of the thickness.</summary>
        public static float RingGap(float thickness)
        {
            return thickness * 0.35f;
        }

        /// <summary>
        /// Host height for a semicircle of this width: the radius plus half a stroke, since the
        /// centre sits half a stroke above the bottom edge.
        /// </summary>
        public static float HostHeight(float width)
        {
            return width * 0.5f + RingThickness(width) * 0.5f;
        }

        // ---- the full state: pulse and wave

        /// <summary>One shared cycle for the full-state pulse and wave.</summary>
        public const float FullPeriodSeconds = 1.8f;
        public const float WavePeriodSeconds = FullPeriodSeconds;

        /// <summary>Seconds a wave takes to cross, right end to left end. The rest of the period is rest.</summary>
        public const float WaveTravelSeconds = 0.95f;

        /// <summary>Half the wave's width as a share of the arc or bar, so the line is soft-edged.</summary>
        public const float WaveHalfWidth = 0.13f;

        public const float WavePeakAlpha = 0.9f;

        /// <summary>How much of the wave's strength is left when it reaches the left end.</summary>
        public const float WaveEndStrength = 0.15f;

        /// <summary>Seconds for the full-state pulse to go there and back.</summary>
        public const float PulsePeriodSeconds = FullPeriodSeconds;

        /// <summary>Global unscaled time -> cycle phase. Joining full never resets it.</summary>
        public static float FullPhase(double globalSeconds)
        {
            return globalSeconds < 0 ? -1f : (float)((globalSeconds % FullPeriodSeconds) / FullPeriodSeconds);
        }

        private static float WaveAtPhase(float phase)
        {
            if (phase < 0f) return -1f;
            float seconds = phase * FullPeriodSeconds;
            return seconds >= WaveTravelSeconds ? -1f : seconds / WaveTravelSeconds;
        }

        private static float PulseAtPhase(float phase)
        {
            if (phase < 0f) return 0f;
            return (float)(0.5 + 0.5 * Math.Cos(phase * 2.0 * Math.PI));
        }

        /// <summary>
        /// Both effects from exactly one phase: pulse peaks as the wave enters on the right.
        /// Every renderer samples Unity's same frame-stable unscaled time, not time spent full.
        /// </summary>
        public static void FullEffectAt(double globalSeconds, bool reducedMotion, out float whiteMix, out float waveProgress)
        {
            float phase = FullPhase(globalSeconds);
            whiteMix = reducedMotion ? StaticWhiteMix : 0.08f + 0.2f * PulseAtPhase(phase);
            waveProgress = reducedMotion ? -1f : WaveAtPhase(phase);
        }

        /// <summary>
        /// How far through its crossing the wave is: 0 at the right end, 1 at the left, and -1
        /// while resting between waves. <paramref name="elapsed"/> is global unscaled time.
        /// </summary>
        public static float WaveProgress(float elapsed)
        {
            return WaveAtPhase(FullPhase(elapsed));
        }

        /// <summary>
        /// White strength of the wave at one point along the arc or bar, <paramref name="position"/>
        /// 0 at the left end and 1 at the right. The line starts on the right end at full
        /// strength and fades progressively as it travels left.
        /// </summary>
        public static float WaveAlpha(float position, float progress)
        {
            if (progress < 0f || progress > 1f) return 0f;
            float centre = 1f - progress;
            float distance = Math.Abs(position - centre);
            if (distance >= WaveHalfWidth) return 0f;
            float shape = 1f - distance / WaveHalfWidth;
            float strength = 1f - (1f - WaveEndStrength) * progress;
            return WavePeakAlpha * shape * strength;
        }

        /// <summary>
        /// The wave's white strength over one sub-slice of one segment. All three rings use the
        /// same value for the same segment and slice, which is what makes the wave cross them
        /// simultaneously. The arc runs segment 0 (left) to segments-1 (right).
        /// </summary>
        public static float SliceWaveAlpha(int segment, int segments, int slice, int slices, float progress)
        {
            if (segments <= 0 || slices <= 0) return 0f;
            float position = (segment + (slice + 0.5f) / slices) / segments;
            return WaveAlpha(position, progress);
        }

        /// <summary>Segment-level alpha: the strongest of its slices. What a coarse draw would use.</summary>
        public static float SegmentWaveAlpha(int segment, int segments, float progress, int slices = 3)
        {
            float best = 0f;
            for (int s = 0; s < slices; s++)
            {
                float a = SliceWaveAlpha(segment, segments, s, slices, progress);
                if (a > best) best = a;
            }
            return best;
        }

        /// <summary>One pulse per shared cycle, peaking at wave entry (phase zero).</summary>
        public static float PulseAmount(float elapsed)
        {
            return PulseAtPhase(FullPhase(elapsed));
        }

        /// <summary>The steady brighter tint used instead of the pulse under reduced motion.</summary>
        public const float StaticWhiteMix = 0.22f;

        /// <summary>
        /// How much white a full resource's colour is mixed with. Pulses between a little and a
        /// lot while full; a constant brighter tint, with no motion at all, under reduced motion.
        /// </summary>
        public static float FullWhiteMix(float elapsed, bool reducedMotion)
        {
            if (reducedMotion) return StaticWhiteMix;
            return 0.08f + 0.2f * PulseAmount(elapsed);
        }

        /// <summary>The wave does not play under reduced motion.</summary>
        public static float EffectiveWaveProgress(float elapsed, bool reducedMotion)
        {
            return reducedMotion ? -1f : WaveProgress(elapsed);
        }
    }

    /// <summary>
    /// Typed balance caps. Nonpositive simulation caps are uncapped, but presentation
    /// retains finite display defaults (30/30/10) for its rings and bars.
    /// </summary>
    public static class ResourceCaps
    {
        public static int Food(GameBalanceData balance)
        {
            return balance.foodCap > 0 ? balance.foodCap : ResourceHudMath.DefaultFoodCap;
        }

        public static int Materials(GameBalanceData balance)
        {
            return balance.materialsCap > 0 ? balance.materialsCap : ResourceHudMath.DefaultMaterialsCap;
        }

        public static int Metal(GameBalanceData balance)
        {
            return balance.metalCap > 0 ? balance.metalCap : ResourceHudMath.DefaultMetalCap;
        }
    }
}
