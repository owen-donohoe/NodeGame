using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>One root manipulator covers current and dynamically added lobby lists.</summary>
    internal sealed class LobbyMouseDrag : Manipulator
    {
        private const string SuppressClick = "lb-suppress-drag-click";
        private ScrollView scroll;
        private int pointer = -1;
        private Vector2 start, origin, velocity;
        private long previousAt;
        private double sampledAt;
        private bool dragging, horizontal;
        private IVisualElementScheduledItem inertia;

        // Pager and shop already own their springs; they share capture/click cancellation.
        public static void Capture(VisualElement owner, int pointerId)
        {
            owner.AddToClassList(SuppressClick);
            owner.CapturePointer(pointerId);
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(Down, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerMoveEvent>(Move, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerUpEvent>(Up, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCancelEvent>(Cancel, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCaptureOutEvent>(CaptureOut);
            target.RegisterCallback<MouseUpEvent>(MouseUp, TrickleDown.TrickleDown);
            target.RegisterCallback<ClickEvent>(Click, TrickleDown.TrickleDown);
            target.RegisterCallback<WheelEvent>(Wheel, TrickleDown.TrickleDown);
            target.RegisterCallback<DetachFromPanelEvent>(Detach);
            inertia = target.schedule.Execute(Step).Every(16); inertia.Pause();
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            Stop();
            target.UnregisterCallback<PointerDownEvent>(Down, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerMoveEvent>(Move, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerUpEvent>(Up, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerCancelEvent>(Cancel, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerCaptureOutEvent>(CaptureOut);
            target.UnregisterCallback<MouseUpEvent>(MouseUp, TrickleDown.TrickleDown);
            target.UnregisterCallback<ClickEvent>(Click, TrickleDown.TrickleDown);
            target.UnregisterCallback<WheelEvent>(Wheel, TrickleDown.TrickleDown);
            target.UnregisterCallback<DetachFromPanelEvent>(Detach);
        }

        private void Down(PointerDownEvent evt)
        {
            target.RemoveFromClassList(SuppressClick);
            target.Query(className: SuppressClick).ForEach(e => e.RemoveFromClassList(SuppressClick));
            Stop();
            if (evt.pointerType != UnityEngine.UIElements.PointerType.mouse || evt.button != 0 || !evt.isPrimary) return;
            // Text selection and slider thumbs retain their editing gestures.
            for (var element = evt.target as VisualElement; element != null; element = element.parent)
            {
                if (element is TextField || element is Slider || element is SliderInt || element is Scroller) return;
                if (element is ScrollView list && list.contentViewport.worldBound.Contains(evt.position))
                { scroll = list; break; }
                if (element == target) break;
            }
            if (scroll == null) return;
            scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            pointer = evt.pointerId; start = evt.position;
            origin = scroll.scrollOffset; previousAt = evt.timestamp;
        }

        private void Move(PointerMoveEvent evt)
        {
            if (pointer < 0 || evt.pointerId != pointer) return;
            if ((evt.pressedButtons & 1) == 0) { Stop(); return; }
            Vector2 point = evt.position, delta = point - start;
            if (!dragging)
            {
                var axis = SwipeDecision.Lock(delta.x, delta.y);
                if (axis == SwipeAxis.Pending) return;
                horizontal = axis == SwipeAxis.Horizontal;
                // A fitting inner list yields to an overflowing parent or the
                // shop walk/pager, just as the native touch path does.
                while ((horizontal && (scroll.mode == ScrollViewMode.Vertical || Maximum.x <= 1)) ||
                    (!horizontal && (scroll.mode == ScrollViewMode.Horizontal || Maximum.y <= 1)))
                {
                    var ancestor = scroll.parent;
                    while (ancestor != null && !(ancestor is ScrollView)) ancestor = ancestor.parent;
                    scroll = ancestor as ScrollView;
                    if (scroll == null) { pointer = -1; return; }
                    origin = scroll.scrollOffset;
                }
                dragging = true; Capture(target, pointer);
            }
            Vector2 offset = origin;
            Vector2 max = Maximum;
            if (horizontal) offset.x = LobbyDragMath.Elastic(origin.x - delta.x, max.x, scroll.contentViewport.layout.width);
            else offset.y = LobbyDragMath.Elastic(origin.y - delta.y, max.y, scroll.contentViewport.layout.height);
            Vector2 change = offset - scroll.scrollOffset;
            velocity = new Vector2(LobbyDragMath.Velocity(change.x, evt.timestamp - previousAt),
                LobbyDragMath.Velocity(change.y, evt.timestamp - previousAt));
            scroll.scrollOffset = offset;
            previousAt = evt.timestamp;
            evt.StopImmediatePropagation();
        }

        private void Up(PointerUpEvent evt)
        {
            if (evt.pointerId != pointer) return;
            bool wasDragging = dragging;
            pointer = -1; dragging = false;
            if (!wasDragging) { scroll = null; return; }
            if (target.HasPointerCapture(evt.pointerId)) target.ReleasePointer(evt.pointerId);
            velocity.x = LobbyDragMath.ReleaseVelocity(velocity.x, evt.timestamp - previousAt);
            velocity.y = LobbyDragMath.ReleaseVelocity(velocity.y, evt.timestamp - previousAt);
            sampledAt = Time.unscaledTimeAsDouble; inertia.Resume();
            evt.StopImmediatePropagation();
        }

        private Vector2 Maximum => new Vector2(
            Mathf.Max(0, scroll.contentContainer.layout.width - scroll.contentViewport.layout.width),
            Mathf.Max(0, scroll.contentContainer.layout.height - scroll.contentViewport.layout.height));

        private void Step()
        {
            if (scroll == null || scroll.panel == null || scroll.resolvedStyle.display == DisplayStyle.None ||
                scroll.contentViewport.worldBound.height <= 0) { Stop(); return; }
            double now = Time.unscaledTimeAsDouble;
            float seconds = (float)(now - sampledAt); sampledAt = now;
            Vector2 offset = scroll.scrollOffset, max = Maximum;
            LobbyDragMath.Step(ref offset.x, ref velocity.x, max.x, seconds);
            LobbyDragMath.Step(ref offset.y, ref velocity.y, max.y, seconds);
            scroll.scrollOffset = offset;
            if (velocity == Vector2.zero && offset.x >= 0 && offset.x <= max.x && offset.y >= 0 && offset.y <= max.y)
            { inertia.Pause(); scroll = null; }
        }

        private void Stop()
        {
            inertia?.Pause();
            int released = pointer; pointer = -1; dragging = false;
            if (released >= 0 && target.HasPointerCapture(released)) target.ReleasePointer(released);
            if (scroll != null && scroll.panel != null)
            {
                Vector2 max = Maximum, offset = scroll.scrollOffset;
                scroll.scrollOffset = new Vector2(Mathf.Clamp(offset.x, 0, max.x), Mathf.Clamp(offset.y, 0, max.y));
            }
            scroll = null; velocity = Vector2.zero;
        }
        private void Cancel(PointerCancelEvent evt) { if (evt.pointerId == pointer) Stop(); }
        private void CaptureOut(PointerCaptureOutEvent evt) { if (evt.target == target && evt.pointerId == pointer) Stop(); }
        private void Detach(DetachFromPanelEvent evt) { if (evt.target == target) Stop(); }
        private void Wheel(WheelEvent evt) { Stop(); } // Native wheel scrolling remains enabled.
        private bool Suppressed(VisualElement element)
        {
            for (; element != null; element = element.parent)
            {
                if (element.ClassListContains(SuppressClick)) return true;
                if (element == target) break;
            }
            return false;
        }
        private void MouseUp(MouseUpEvent evt) { if (Suppressed(evt.target as VisualElement)) evt.StopImmediatePropagation(); }
        private void Click(ClickEvent evt) { if (Suppressed(evt.target as VisualElement)) evt.StopImmediatePropagation(); }
    }
}
