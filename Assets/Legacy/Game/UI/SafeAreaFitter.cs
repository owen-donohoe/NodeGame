using UnityEngine;

namespace NodeWar.UI
{
    /// <summary>
    /// Insets a RectTransform to Screen.safeArea, keeping its children clear of
    /// notches, punch-holes and the home indicator.
    ///
    /// This lives in the input foundation rather than alongside the first screen
    /// element that happens to need it. Every screen-edge element depends on it:
    /// the bottom sheet sits directly on the edge a home indicator occupies, and
    /// notification cards sit on another. Introducing it later would mean the
    /// sheet shipped once with the wrong geometry and was then corrected.
    ///
    /// Attach to a full-screen child of the Canvas and parent edge-anchored
    /// content to it. No other project script reads Screen.safeArea.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class SafeAreaFitter : MonoBehaviour
    {
        [Tooltip("Apply the horizontal inset. Turn off for content that should " +
                 "span the full width and only avoid the top/bottom insets.")]
        [SerializeField] private bool applyHorizontal = true;

        [Tooltip("Apply the vertical inset. This is the one the bottom sheet needs.")]
        [SerializeField] private bool applyVertical = true;

        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private ScreenOrientation lastOrientation;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            Apply();
        }

        private void OnEnable()
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            // Rotation, split-screen and editor Game-view resizes all change this
            // without an event. The comparison is cheap; the rebuild is not, so
            // it only runs when something actually moved.
            if (Screen.safeArea == lastSafeArea &&
                Screen.width == lastScreenSize.x &&
                Screen.height == lastScreenSize.y &&
                Screen.orientation == lastOrientation)
                return;

            Apply();
        }

        private void Apply()
        {
            if (rectTransform == null) return;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Rect safe = Screen.safeArea;

            lastSafeArea = safe;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            lastOrientation = Screen.orientation;

            Vector2 min = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            Vector2 max = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);

            if (!applyHorizontal) { min.x = 0f; max.x = 1f; }
            if (!applyVertical)   { min.y = 0f; max.y = 1f; }

            // Guard against a degenerate report; a zero-size rect would collapse
            // every child rather than merely mis-inset them.
            if (max.x <= min.x || max.y <= min.y) return;

            rectTransform.anchorMin = min;
            rectTransform.anchorMax = max;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
