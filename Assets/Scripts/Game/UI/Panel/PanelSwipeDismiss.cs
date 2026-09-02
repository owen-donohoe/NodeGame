using UnityEngine;
using UnityEngine.EventSystems;

namespace NodeWar.UI
{
    /// <summary>
    /// Drag the sheet down to dismiss it.
    ///
    /// This cannot go through PointerGestureSource: a press that begins over UI
    /// is latched Blocked and publishes nothing, which is what stops a tap on a
    /// button from also selecting the world behind it. So the sheet listens for
    /// Unity's own UI drag events instead.
    ///
    /// Attach to the grab handle for handle-only dismissal, or to the sheet
    /// root for drag-anywhere. Either works -- buttons do not consume drag
    /// events, so a drag starting on one bubbles up to whichever ancestor
    /// handles it.
    ///
    /// Requires a raycastable Graphic on the same object; an Image with any
    /// alpha is enough.
    /// </summary>
    public class PanelSwipeDismiss : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("Panel to dismiss. Found on a parent if left empty.")]
        [SerializeField] private NodePanelManager panel;

        [Tooltip("Sheet transform that follows the finger. Found on a parent " +
                 "if left empty.")]
        [SerializeField] private RectTransform sheet;

        [Tooltip("How far down the sheet must be dragged to dismiss, in " +
                 "millimetres. Below this it springs back.")]
        [Range(2f, 25f)]
        [SerializeField] private float dismissDistanceMm = 8f;

        [Tooltip("Downward flick speed that dismisses regardless of distance, " +
                 "in millimetres per second. A fast short flick should still " +
                 "close it.")]
        [SerializeField] private float dismissVelocityMmPerSec = 45f;

        private Vector2 startAnchoredPos;
        private float dragStartY;
        private float lastY;
        private float lastTime;
        private float velocityPxPerSec;
        private bool dragging;
        private bool openedAtDragStart;
        private Canvas canvas;

        private void Awake()
        {
            if (panel == null) panel = GetComponentInParent<NodePanelManager>();
            if (sheet == null) sheet = GetComponentInParent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
        }

        /// <summary>
        /// Screen pixels per canvas unit.
        ///
        /// PointerEventData.position is in screen pixels; anchoredPosition is in
        /// canvas units. Under a ScaleWithScreenSize CanvasScaler those differ
        /// by scaleFactor, so assigning a raw pixel delta moves the sheet a
        /// fraction of the distance the finger travelled and reads as lag.
        /// Dividing by it makes the sheet track the finger exactly.
        /// </summary>
        private float ScaleFactor
        {
            get
            {
                if (canvas == null) return 1f;
                float s = canvas.scaleFactor;
                return s > 0.0001f ? s : 1f;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (sheet == null) return;

            dragging = true;
            openedAtDragStart = panel != null && panel.IsOpen;
            startAnchoredPos = sheet.anchoredPosition;
            dragStartY = eventData.position.y;
            lastY = eventData.position.y;
            lastTime = Time.unscaledTime;
            velocityPxPerSec = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || sheet == null) return;

            float dyPixels = eventData.position.y - dragStartY;

            // An open sheet only travels down; a dismissed one only travels up.
            // Neither stretches past its resting position, because there is
            // nothing beyond it to reveal in that direction.
            if (openedAtDragStart) { if (dyPixels > 0f) dyPixels = 0f; }
            else                   { if (dyPixels < 0f) dyPixels = 0f; }

            sheet.anchoredPosition = new Vector2(
                startAnchoredPos.x,
                startAnchoredPos.y + (dyPixels / ScaleFactor));

            float now = Time.unscaledTime;
            float dt = now - lastTime;
            if (dt > 0.0001f)
            {
                velocityPxPerSec = (eventData.position.y - lastY) / dt;
                lastY = eventData.position.y;
                lastTime = now;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging || sheet == null) return;
            dragging = false;

            if (panel == null) return;

            // Thresholds stay in screen pixels: they describe how far a finger
            // moved on glass, which is what the millimetre figure means.
            float distancePx = NodeWar.Input.ScreenMetrics.MmToPixels(dismissDistanceMm);
            float velocityPx = NodeWar.Input.ScreenMetrics.MmToPixels(dismissVelocityMmPerSec);

            if (openedAtDragStart)
            {
                float draggedDown = dragStartY - eventData.position.y;
                bool committed = draggedDown >= distancePx || velocityPxPerSec <= -velocityPx;

                // Swiping away leaves the handle reachable, unlike the close
                // button, which means closed. Animates from wherever the sheet
                // currently sits, so it continues the drag rather than snapping
                // back first.
                if (committed) panel.DismissToHandle();
                else panel.ReturnToRestingPosition();

                return;
            }

            // Dismissed: an upward drag pulls the last panel back.
            float draggedUp = eventData.position.y - dragStartY;
            bool restoring = draggedUp >= distancePx || velocityPxPerSec >= velocityPx;

            if (restoring) panel.ReopenLast();
            else panel.ReturnToRestingPosition();
        }
    }
}
