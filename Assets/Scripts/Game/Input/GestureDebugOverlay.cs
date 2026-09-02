using System.Collections.Generic;
using UnityEngine;

namespace NodeWar.Input
{
    /// <summary>
    /// On-screen readout of the resolved gesture thresholds.
    ///
    /// Thresholds are authored in millimetres and only become pixels at runtime
    /// against the live screen density, so the authored numbers cannot be
    /// sanity-checked by reading the inspector. This draws what they actually
    /// resolve to, and -- for the lasso minimum, which is an *area* and so does
    /// not scale like the others -- draws a reference square of exactly that
    /// size to compare a real stroke against.
    ///
    /// Drop it on any GameObject in the Gameplay scene. Editor and development
    /// builds only; it compiles out of a release player.
    /// </summary>
    public class GestureDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool showOverlay = true;

        [Tooltip("Draw a square of exactly minLassoArea next to the readout.")]
        [SerializeField] private bool showAreaReference = true;

        [Tooltip("Optional. When set, the overlay reports that source's live " +
                 "gesture state, its thresholds and its in-progress stroke, " +
                 "rather than driving a stroke of its own.")]
        [SerializeField] private PointerGestureSource source;

        [Tooltip("Thresholds to report when no gesture source is assigned.")]
        [SerializeField] private GestureThresholds thresholds = new GestureThresholds();

        /// <summary>The source's thresholds win, so the overlay can never report values the gesture layer is not using.</summary>
        public GestureThresholds Active => source != null ? source.Thresholds : thresholds;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GUIStyle labelStyle;
        private GUIStyle headerStyle;
        private Texture2D swatch;

        // Live stroke, so a real drag can be measured against the minimum.
        private readonly List<Vector2> stroke = new List<Vector2>();
        private bool strokeActive;
        private float lastStrokeArea;
        private bool lastStrokeValid;

        private void Update()
        {
            if (!showOverlay) return;

            // A gesture source owns the stroke when one is present; duplicating
            // the capture here would report different decimation than the real
            // lasso and quietly disagree with it.
            if (source != null) return;

            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer == null) return;

            // GUI space is Y-down, input is Y-up. Record in input space and flip
            // only when drawing, so the area figure matches what the real lasso
            // will compute.
            Vector2 pos = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                stroke.Clear();
                strokeActive = true;
                LassoGeometry.TryAppend(stroke, pos, thresholds.LassoDecimationPx, thresholds.maxLassoPoints);
            }
            else if (strokeActive && pointer.press.isPressed)
            {
                LassoGeometry.TryAppend(stroke, pos, thresholds.LassoDecimationPx, thresholds.maxLassoPoints);
            }
            else if (strokeActive && pointer.press.wasReleasedThisFrame)
            {
                strokeActive = false;
                lastStrokeArea = LassoGeometry.Area(stroke);
                lastStrokeValid = LassoGeometry.IsValid(stroke, thresholds.MinLassoAreaSqPx);
            }
        }

        private void OnGUI()
        {
            if (!showOverlay) return;
            EnsureStyles();

            GestureThresholds t = Active;

            const float pad = 10f;
            float w = 320f;
            float h = showAreaReference ? 268f : 190f;

            GUI.Box(new Rect(pad, pad, w, h), GUIContent.none);

            float y = pad + 8f;
            GUI.Label(new Rect(pad + 10f, y, w - 20f, 18f), "GESTURE THRESHOLDS", headerStyle);
            y += 22f;

            Row(ref y, w, "state", source != null ? source.State.ToString() : "(no source)");
            Row(ref y, w, "screen", Screen.width + " x " + Screen.height);
            Row(ref y, w, "dpi", ScreenMetrics.Dpi.ToString("0.#") +
                (Screen.dpi > 1f ? "" : "  (fallback)"));
            Row(ref y, w, "px / mm", ScreenMetrics.PixelsPerMm.ToString("0.000"));
            y += 6f;

            Row(ref y, w, "tap slop",
                t.tapSlopMm.ToString("0.##") + " mm  =  " +
                t.TapSlopPx.ToString("0.0") + " px");
            Row(ref y, w, "decimation",
                t.lassoDecimationMm.ToString("0.##") + " mm  =  " +
                t.LassoDecimationPx.ToString("0.0") + " px");
            Row(ref y, w, "long press", t.longPressTime.ToString("0.00") + " s");
            y += 6f;

            Row(ref y, w, "min lasso",
                t.minLassoAreaSqMm.ToString("0.#") + " mm2  =  " +
                t.MinLassoAreaSqPx.ToString("0") + " px2");
            Row(ref y, w, "  as square",
                t.MinLassoSidePx.ToString("0.0") + " px per side");

            IReadOnlyList<Vector2> live = ActiveStroke();
            if (live != null && live.Count > 0)
            {
                float area = LassoGeometry.Area(live);
                bool valid = LassoGeometry.IsValid(live, t.MinLassoAreaSqPx);

                Row(ref y, w, "stroke",
                    area.ToString("0") + " px2  " + (valid ? "PASS" : "reject") +
                    "  (" + live.Count + " pts)");
            }
            else if (lastStrokeArea > 0f)
            {
                Row(ref y, w, "last stroke",
                    lastStrokeArea.ToString("0") + " px2  " +
                    (lastStrokeValid ? "PASS" : "reject"));
            }

            if (showAreaReference)
            {
                y += 8f;
                GUI.Label(new Rect(pad + 10f, y, w - 20f, 18f),
                          "minimum lasso, actual size:", labelStyle);
                y += 20f;

                float side = t.MinLassoSidePx;
                GUI.DrawTexture(new Rect(pad + 14f, y, side, side), Swatch());
            }

            DrawStroke(live);
        }

        private void Row(ref float y, float w, string key, string value)
        {
            GUI.Label(new Rect(12f + 8f, y, 96f, 18f), key, labelStyle);
            GUI.Label(new Rect(12f + 108f, y, w - 120f, 18f), value, labelStyle);
            y += 18f;
        }

        /// <summary>The source's stroke when bound, otherwise the overlay's own.</summary>
        private IReadOnlyList<Vector2> ActiveStroke()
        {
            if (source != null)
                return source.State == GestureState.Lassoing ||
                       source.State == GestureState.LassoArmed
                    ? source.CurrentStroke
                    : null;

            return strokeActive ? stroke : null;
        }

        private void DrawStroke(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 2) return;

            // Input is Y-up, GUI is Y-down. Points are stored in input space so
            // the reported area matches what the real lasso computes; the flip
            // happens only here, at draw time.
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.4f, 0.9f);

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                GUI.DrawTexture(new Rect(p.x - 2f, (Screen.height - p.y) - 2f, 4f, 4f), Swatch());
            }

            GUI.color = prev;
        }

        private Texture2D Swatch()
        {
            if (swatch == null)
            {
                swatch = new Texture2D(1, 1);
                swatch.SetPixel(0, 0, Color.white);
                swatch.Apply();
                swatch.hideFlags = HideFlags.HideAndDontSave;
            }
            return swatch;
        }

        private void EnsureStyles()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.fontSize = 11;
                labelStyle.richText = false;
            }
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label);
                headerStyle.fontSize = 11;
                headerStyle.fontStyle = FontStyle.Bold;
            }
        }

        private void OnDestroy()
        {
            if (swatch != null) DestroyImmediate(swatch);
        }
#endif
    }
}
