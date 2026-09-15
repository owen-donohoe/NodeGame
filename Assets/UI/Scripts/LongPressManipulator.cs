using UnityEngine;
using UnityEngine.UIElements;
using NodeWar.Input;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Hold still on an element and something happens; move and nothing does.
    ///
    /// The one long press the lobby uses, so every held card, box and row
    /// behaves alike:
    ///   - a press starts a timer;
    ///   - moving past the tap slop cancels it. Without this, every scroll that
    ///     happens to start on a card would open a menu;
    ///   - lifting before the timer is an ordinary tap, left to the element;
    ///   - once it has fired, the lift that follows is swallowed, so a Button
    ///     does not also count the same press as a click.
    ///
    /// The slop comes from GestureThresholds, so "moved" means the same
    /// physical distance here as it does on the board. The hold time is the
    /// prototype's 500ms rather than the board's 0.3s: a menu is a
    /// deliberate act, not a selection.
    /// </summary>
    public class LongPressManipulator : PointerManipulator
    {
        private const long HoldMs = 500;

        private static readonly GestureThresholds thresholds = new GestureThresholds();

        private readonly System.Action<VisualElement, Vector2> onLongPress;

        private IVisualElementScheduledItem timer;
        private int pointerId = PointerId.invalidPointerId;
        private Vector2 startPosition;
        private bool fired;

        public LongPressManipulator(System.Action<VisualElement, Vector2> onLongPress)
        {
            this.onLongPress = onLongPress;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            // Trickle-down, so this runs before the Button's own Clickable at
            // the target and can swallow the lift after a long press.
            target.RegisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCancelEvent>(OnCancel);
            target.RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerCancelEvent>(OnCancel);
            target.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        private void OnDown(PointerDownEvent evt)
        {
            Stop();

            fired = false;
            pointerId = evt.pointerId;
            startPosition = evt.position;

            Vector2 at = evt.position;
            timer = target.schedule.Execute(() => Fire(at));
            timer.ExecuteLater(HoldMs);
        }

        private void OnMove(PointerMoveEvent evt)
        {
            if (timer == null || evt.pointerId != pointerId) return;

            Vector2 delta = (Vector2)evt.position - startPosition;
            if (delta.magnitude > SlopInPanelUnits()) Stop();
        }

        private void OnUp(PointerUpEvent evt)
        {
            if (evt.pointerId != pointerId) return;

            Stop();

            if (fired)
            {
                fired = false;
                evt.StopImmediatePropagation();
                if (target.HasPointerCapture(evt.pointerId)) target.ReleasePointer(evt.pointerId);
            }
        }

        private void OnCancel(PointerCancelEvent evt)
        {
            Stop();
            fired = false;
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            Stop();
        }

        private void Fire(Vector2 position)
        {
            timer = null;
            fired = true;
            if (onLongPress != null) onLongPress(target, position);
        }

        private void Stop()
        {
            if (timer != null) timer.Pause();
            timer = null;
        }

        /// <summary>
        /// GestureThresholds speaks screen pixels; pointer positions here are
        /// panel units, which the lobby's panel scales to a 390-wide screen.
        /// </summary>
        private float SlopInPanelUnits()
        {
            float panelWidth = target.panel != null ? target.panel.visualTree.layout.width : 0f;
            if (panelWidth <= 0f || Screen.width <= 0) return thresholds.TapSlopPx;

            return thresholds.TapSlopPx * panelWidth / Screen.width;
        }
    }
}
