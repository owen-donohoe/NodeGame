using UnityEngine;

namespace NodeWar.Input
{
    /// <summary>Development readout of physical thresholds and the live paint snapshot.</summary>
    public class GestureDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool showOverlay = true;
        // Retain the serialized name; the reference now shows the brush disc.
        [SerializeField] private bool showAreaReference = true;
        [SerializeField] private PointerGestureSource source;
        [SerializeField] private GestureThresholds thresholds = new GestureThresholds();
        public GestureThresholds Active => source != null ? source.Thresholds : thresholds;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private Texture2D disc;
        private void OnGUI()
        {
            if (!showOverlay) return;
            GestureThresholds t = Active;
            bool painting = source != null &&
                (source.State == GestureState.LassoArmed || source.State == GestureState.Lassoing);
            float radius = painting ? source.CurrentPaintRadiusPx : ScreenMetrics.MmToPixels(t.paintRadiusMm);
            GUI.Box(new Rect(10f, 10f, 320f, showAreaReference ? 270f : 190f), GUIContent.none);
            float y = 18f;
            Row(ref y, "GESTURE THRESHOLDS");
            Row(ref y, "state: " + (source != null ? source.State.ToString() : "(no source)"));
            Row(ref y, "screen: " + Screen.width + " x " + Screen.height);
            Row(ref y, "dpi: " + ScreenMetrics.Dpi.ToString("0.#") + (Screen.dpi > 1f ? "" : " (fallback)"));
            Row(ref y, "tap slop: " + t.tapSlopMm.ToString("0.##") + " mm");
            Row(ref y, "decimation: " + t.lassoDecimationMm.ToString("0.##") + " mm");
            Row(ref y, "hold: " + t.longPressTime.ToString("0.00") + " s");
            Row(ref y, "paint radius: " + radius.ToString("0.0") + " px (fixed screen size)");
            if (painting)
                Row(ref y, "recorded: " + source.CurrentStroke.Count + " pts, " +
                    (LassoGeometry.IsValid(source.CurrentStroke, radius) ? "valid" : "invalid"));
            if (showAreaReference)
            {
                Row(ref y, "brush disc, actual size:");
                GUI.DrawTexture(new Rect(24f, y, radius * 2f, radius * 2f), Disc());
            }
        }

        private static void Row(ref float y, string value)
        {
            GUI.Label(new Rect(20f, y, 300f, 18f), value);
            y += 18f;
        }

        private Texture2D Disc()
        {
            if (disc != null) return disc;
            const int size = 64;
            disc = new Texture2D(size, size) { hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f, dy = y + 0.5f - size * 0.5f;
                    disc.SetPixel(x, y, dx * dx + dy * dy <= size * size * 0.25f
                        ? new Color(1f, 0.85f, 0.4f, 0.25f) : Color.clear);
                }
            disc.Apply();
            return disc;
        }

        private void OnDisable()
        {
            if (disc != null) Destroy(disc);
            disc = null;
        }
#endif
    }
}
