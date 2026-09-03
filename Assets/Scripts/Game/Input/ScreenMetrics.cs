using UnityEngine;

namespace NodeWar.Input
{
    /// <summary>
    /// Converts physical units (millimetres) into pixels for the current screen.
    ///
    /// Why this exists: input thresholds must mean the same thing to a finger on
    /// every device. The old SelectionSystem used a flat 10-pixel drag threshold,
    /// which is ~1.6 mm on a 160 dpi screen but ~0.6 mm on a 440 dpi phone --
    /// small enough there that the jitter of a deliberate tap would register as a
    /// drag and every tap would be read as a pan.
    ///
    /// All gesture thresholds are authored in millimetres and converted here.
    /// </summary>
    public static class ScreenMetrics
    {
        /// <summary>
        /// Assumed density when the platform does not report one. Screen.dpi
        /// returns 0 on several platforms (and on some editor configurations),
        /// so a sane desktop-class default keeps thresholds usable rather than
        /// collapsing them to zero.
        /// </summary>
        public const float FallbackDpi = 160f;

        public static float Dpi
        {
            get
            {
                float reported = Screen.dpi;
                return reported > 1f ? reported : FallbackDpi;
            }
        }

        public static float PixelsPerMm => Dpi / 25.4f;

        /// <summary>
        /// Length conversion. A distance in millimetres becomes a distance in pixels.
        /// </summary>
        public static float MmToPixels(float mm)
        {
            return mm * PixelsPerMm;
        }

        /// <summary>
        /// Area conversion. A square-millimetre value becomes square pixels.
        ///
        /// This is deliberately a separate method rather than a caller writing
        /// MmToPixels(area). Density scales *length*, so an area scales by the
        /// density squared: 25 mm^2 at 160 dpi is 25 * 6.2992^2 = 992 px^2, not
        /// 25 * 6.2992 = 158 px^2. Converting an area with the length helper
        /// under-reports it by a factor of PixelsPerMm -- roughly 6x at 160 dpi
        /// and 17x at 440 dpi -- which would let a scribble far below the
        /// intended minimum pass as a valid lasso.
        /// </summary>
        public static float SqMmToSqPixels(float squareMm)
        {
            float perMm = PixelsPerMm;
            return squareMm * perMm * perMm;
        }
    }
}
