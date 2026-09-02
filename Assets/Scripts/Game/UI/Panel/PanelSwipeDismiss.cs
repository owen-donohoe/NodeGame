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

        private void Awake()
        {
            if (panel == null) panel = GetComponentInParent<NodePanelManager>();
            if (sheet == null) sheet = GetComponentInParent<RectTransform>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (sheet == null) return;

            dragging = true;
            startAnchoredPos = sheet.anchoredPosition;
            dragStartY = eventData.position.y;
            lastY = eventData.position.y;
            lastTime = Time.unscaledTime;
            velocityPxPerSec = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || sheet == null) return;

            float dy = eventData.position.y - dragStartY;

            // Downward only. Dragging up does not stretch the sheet past its
            // resting position -- there is nothing above it to reveal.
            if (dy > 0f) dy = 0f;

            sheet.anchoredPosition = new Vector2(startAnchoredPos.x, startAnchoredPos.y + dy);

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

            float draggedDown = dragStartY - eventData.position.y;

            bool pastDistance = draggedDown >= NodeWar.Input.ScreenMetrics.MmToPixels(dismissDistanceMm);
            bool fastFlick = velocityPxPerSec <= -NodeWar.Input.ScreenMetrics.MmToPixels(dismissVelocityMmPerSec);

            if (panel != null && (pastDistance || fastFlick))
            {
                // ClosePanel animates from wherever the sheet currently sits,
                // so the dismissal continues the drag rather than snapping back
                // first and then sliding away.
                panel.ClosePanel();
                return;
            }

            // Not far or fast enough: return to where it was.
            panel?.ReturnToRestingPosition();
        }
    }
}
