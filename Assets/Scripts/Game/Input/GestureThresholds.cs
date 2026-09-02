using UnityEngine;

namespace NodeWar.Input
{
    /// <summary>
    /// Tunable gesture thresholds, authored in physical units so they mean the
    /// same thing to a finger on any screen density.
    ///
    /// Serialized rather than const because these need tuning against a real
    /// hand on a real device, and a rebuild per adjustment is the wrong loop.
    /// The pixel values are derived, never authored -- see ScreenMetrics for why
    /// the area conversion is not the length conversion.
    /// </summary>
    [System.Serializable]
    public class GestureThresholds
    {
        [Header("Movement (millimetres)")]
        [Tooltip("How far the pointer may travel and still count as a tap. " +
                 "Below the jitter of a deliberate press; above this the gesture " +
                 "becomes a pan and any pending selection is cancelled.")]
        [Range(0.5f, 6f)]
        public float tapSlopMm = 2.0f;

        [Tooltip("Minimum spacing between recorded lasso points. Kept below " +
                 "tapSlop so the polygon still tracks a tight curve, while " +
                 "keeping the vertex count bounded.")]
        [Range(0.25f, 5f)]
        public float lassoDecimationMm = 1.5f;

        [Header("Timing (seconds, unscaled)")]
        [Tooltip("Hold this long without moving and the press becomes a long " +
                 "press. Doubles as the tap's implicit maximum -- a press that " +
                 "survives it is no longer a tap candidate.")]
        [Range(0.15f, 1f)]
        // TODO: expose in player settings when a settings system exists.
        // Long-press duration is an accessibility control as much as a feel
        // one -- it is the standard accommodation for reduced motor control,
        // and players differ widely in what reads as "held" versus "tapped".
        // Tuned to 0.3s by hand; that is the default, not a fixed value.
        public float longPressTime = 0.3f;

        [Tooltip("How long the white touch-down flash lasts.")]
        [Range(0.03f, 0.5f)]
        public float flashDuration = 0.12f;

        [Header("Lasso")]
        [Tooltip("Minimum enclosed area for a lasso to select anything, in " +
                 "SQUARE millimetres. Default is a 5x5 mm square. Below this " +
                 "the stroke is a scribble, not a shape -- the selection is " +
                 "left untouched rather than cleared.")]
        [Range(4f, 400f)]
        public float minLassoAreaSqMm = 25f;

        [Tooltip("Hard cap on recorded lasso vertices. Decimation should keep " +
                 "strokes well under this; the cap bounds the pathological case.")]
        [Range(32, 1024)]
        public int maxLassoPoints = 256;

        [Tooltip("Chaikin corner-cutting passes applied to the lasso before it " +
                 "is drawn and tested. 0 leaves the raw decimated polyline. " +
                 "Each pass doubles the point count, so 1-2 is plenty at the " +
                 "default decimation.")]
        [Range(0, 3)]
        public int lassoSmoothingIterations = 2;

        /// <summary>
        /// Headroom for the smoothing passes, which double the count each time.
        /// Kept separate from maxLassoPoints so raising smoothing cannot
        /// silently start truncating strokes at capture time.
        /// </summary>
        public int MaxSmoothedPoints => maxLassoPoints * 8;

        // ===== DERIVED (pixels, current screen) =====

        public float TapSlopPx => ScreenMetrics.MmToPixels(tapSlopMm);
        public float LassoDecimationPx => ScreenMetrics.MmToPixels(lassoDecimationMm);

        /// <summary>
        /// Square pixels. Uses the area conversion -- density squared, not
        /// density. At the 25 mm^2 default this is ~992 px^2 at 160 dpi and
        /// ~7502 px^2 at 440 dpi.
        /// </summary>
        public float MinLassoAreaSqPx => ScreenMetrics.SqMmToSqPixels(minLassoAreaSqMm);

        /// <summary>Side length of the equivalent square, for display and gizmos.</summary>
        public float MinLassoSidePx => ScreenMetrics.MmToPixels(Mathf.Sqrt(minLassoAreaSqMm));
    }
}
