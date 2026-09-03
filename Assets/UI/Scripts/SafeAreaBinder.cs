using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Insets a VisualElement to Screen.safeArea, keeping its children clear of
    /// notches, punch-holes and the home indicator.
    ///
    /// This exists because the project's safe-area handling was never actually
    /// wired up. Assets/Scripts/Game/UI/SafeAreaFitter.cs is a correct and
    /// complete component that is attached to nothing - no scene, no prefab, no
    /// script references it (docs/ui-migration-inventory.md, finding 1). Its
    /// doc comment claims to be the only reader of Screen.safeArea, which is
    /// true and also the problem: nothing reads it. So the new stack gets a
    /// working equivalent rather than inheriting a dormant one.
    ///
    /// Insets are computed as a PROPORTION of the screen and applied to the
    /// panel's own resolved size, rather than assigning screen pixels directly.
    /// PanelSettings runs in ConstantPhysicalSize, so one panel unit is not one
    /// screen pixel, and assigning raw Screen.safeArea numbers would inset by
    /// the wrong amount on every device whose scale factor is not exactly 1.
    /// </summary>
    public class SafeAreaBinder
    {
        private readonly VisualElement target;

        private Rect lastSafeArea = new Rect(0, 0, 0, 0);
        private int lastScreenWidth;
        private int lastScreenHeight;
        private float lastPanelWidth;
        private float lastPanelHeight;

        public SafeAreaBinder(VisualElement target)
        {
            this.target = target;
        }

        /// <summary>
        /// Re-applies the inset if anything it depends on has changed. Cheap
        /// enough to call every frame; does nothing on the frames where the
        /// answer would be identical.
        ///
        /// Rotation, a resized editor Game view, and the panel finishing its
        /// first layout all have to be caught, which is why the panel's own
        /// size is part of the change check and not just Screen.
        /// </summary>
        public void Update()
        {
            if (target == null) return;

            float panelWidth = target.resolvedStyle.width;
            float panelHeight = target.resolvedStyle.height;

            // Before the first layout pass these are NaN or zero. Nothing to do
            // yet; the next frame will have real numbers.
            if (float.IsNaN(panelWidth) || float.IsNaN(panelHeight)) return;
            if (panelWidth <= 0f || panelHeight <= 0f) return;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Rect safeArea = Screen.safeArea;

            bool unchanged =
                safeArea == lastSafeArea &&
                Screen.width == lastScreenWidth &&
                Screen.height == lastScreenHeight &&
                Mathf.Approximately(panelWidth, lastPanelWidth) &&
                Mathf.Approximately(panelHeight, lastPanelHeight);

            if (unchanged) return;

            lastSafeArea = safeArea;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastPanelWidth = panelWidth;
            lastPanelHeight = panelHeight;

            Apply(safeArea, panelWidth, panelHeight);
        }

        private void Apply(Rect safeArea, float panelWidth, float panelHeight)
        {
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            // Screen.safeArea is Y-up with its origin at the bottom-left, so
            // yMin is the bottom inset and the top inset is what is left above
            // yMax. Panel padding is Y-down, hence the swap.
            float leftPx = safeArea.xMin;
            float rightPx = screenWidth - safeArea.xMax;
            float bottomPx = safeArea.yMin;
            float topPx = screenHeight - safeArea.yMax;

            target.style.paddingLeft = ToPanelUnits(leftPx, screenWidth, panelWidth);
            target.style.paddingRight = ToPanelUnits(rightPx, screenWidth, panelWidth);
            target.style.paddingTop = ToPanelUnits(topPx, screenHeight, panelHeight);
            target.style.paddingBottom = ToPanelUnits(bottomPx, screenHeight, panelHeight);
        }

        private static float ToPanelUnits(float screenPixels, float screenExtent, float panelExtent)
        {
            if (screenExtent <= 0f) return 0f;

            // Negative would mean a safe area larger than the screen. Clamp
            // rather than trust it; some editor device simulators report odd
            // rects mid-rotation.
            float units = screenPixels / screenExtent * panelExtent;
            return units > 0f ? units : 0f;
        }
    }
}
