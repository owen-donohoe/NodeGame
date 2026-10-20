using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>One physical strip, sampled once per frame for pages, pills and camera.</summary>
    public class NavigationController
    {
        private static readonly LobbyPageID[] Order = { LobbyPageID.Shop, LobbyPageID.Workshop, LobbyPageID.Home, LobbyPageID.Social, LobbyPageID.Guild };
        private readonly VisualElement pageHost, track;
        private readonly LobbyPage[] pages = new LobbyPage[5];
        private readonly bool[] live = new bool[5];
        private readonly Dictionary<LobbyPageID, Button> navButtons = new Dictionary<LobbyPageID, Button>();
        private readonly LobbyMotion.PageStrip strip = new LobbyMotion.PageStrip();
        private readonly VisualElement indicator = new VisualElement { name = "pill-progress", pickingMode = PickingMode.Ignore };
        private Func<bool> overlayOpen;
        private bool initialized;
        private int swipePointer = -1;
        private Vector2 swipeStart, swipePrevious;
        private long swipeStartedAt, swipePreviousAt;
        private SwipeAxis swipeAxis;
        public event Action<LobbyPageID> PageShown;
        public event Action<LobbyPageID, LobbyPageID, float, bool> MotionChanged;
        public LobbyPageID? CurrentPageID => initialized ? Order[strip.Current] : (LobbyPageID?)null;

        public NavigationController(VisualElement pageHost)
        {
            this.pageHost = pageHost;
            pageHost.pickingMode = PickingMode.Position;
            track = new VisualElement { name = "page-track", pickingMode = PickingMode.Ignore };
            track.AddToClassList("lb-page-track"); pageHost.Add(track);
            // Block actions during travel without disabling/dimming page contents.
            track.RegisterCallback<PointerDownEvent>(evt => { if (strip.Moving || strip.Dragging) evt.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
            track.RegisterCallback<ClickEvent>(evt => { if (strip.Moving || strip.Dragging) evt.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
            track.RegisterCallback<WheelEvent>(evt => { if (strip.Moving || strip.Dragging) evt.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
            pageHost.RegisterCallback<PointerDownEvent>(OnSwipeStart, TrickleDown.TrickleDown);
            pageHost.RegisterCallback<PointerMoveEvent>(OnSwipeMove, TrickleDown.TrickleDown);
            pageHost.RegisterCallback<PointerUpEvent>(OnSwipeEnd, TrickleDown.TrickleDown);
            pageHost.RegisterCallback<PointerCancelEvent>(_ => CancelSwipe());
            pageHost.RegisterCallback<PointerCaptureOutEvent>(evt => { if (evt.pointerId == swipePointer) CancelSwipe(); });
            indicator.AddToClassList("lb-pill-progress");
            indicator.style.position = Position.Absolute;
        }
        public void BindSwipes(Func<bool> anyOverlayOpen) => overlayOpen = anyOverlayOpen;
        public void BindNavButton(LobbyPageID id, Button button)
        {
            if (button == null) return;
            navButtons[id] = button; button.clicked += () => Show(id);
            if (indicator.parent == null)
            {
                button.parent.AddToClassList("mk-tabs--sliding");
                button.parent.Insert(0, indicator);
            }
        }
        public void Register(LobbyPage page)
        {
            if (page == null) return;
            int i = LobbyMotion.PillIndex(page.ID); pages[i] = page; track.Add(page.Root);
            page.Root.style.position = Position.Absolute;
            page.Root.style.left = page.Root.style.right = page.Root.style.top = page.Root.style.bottom = 0;
            page.Root.style.display = DisplayStyle.None;
            // Never inherit a USS transition on the physical strip.
            page.Root.style.transitionDuration = new List<TimeValue> { new TimeValue(0) };
        }
        public bool Has(LobbyPageID id) => pages[LobbyMotion.PillIndex(id)] != null;
        public void Show(LobbyPageID id)
        {
            int i = LobbyMotion.PillIndex(id);
            if (pages[i] == null) return;
            if (initialized && strip.Current == i && !strip.Dragging) return;
            ReleasePointer();
            if (!initialized) { strip.Reset(i); initialized = true; }
            else strip.Go(i);
            PageShown?.Invoke(id); Apply();
        }
        // The shell calls this before applying the stage and projecting shop UI.
        public void Tick(float seconds)
        {
            if (!initialized) return;
            strip.Step(seconds); Apply();
        }
        private void Apply()
        {
            float width = Mathf.Max(1f, pageHost.layout.width);
            for (int i = 0; i < pages.Length; i++)
            {
                var page = pages[i]; if (page == null) continue;
                bool visible = strip.Visible(i);
                if (visible != live[i])
                {
                    live[i] = visible;
                    page.Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                    if (visible) page.OnShow(); else page.OnHide();
                }
                if (visible) page.Root.style.translate = new Translate((strip.Slots[i] - strip.Motion.Position) * width, 0f);
            }
            strip.Pair(out int from, out int to, out float progress);
            UpdatePills(from, to, progress);
            MotionChanged?.Invoke(Order[from], Order[to], progress, strip.Moving || strip.Dragging);
        }
        private void UpdatePills(int from, int to, float progress)
        {
            int highlighted = Mathf.Clamp(Mathf.RoundToInt(Mathf.LerpUnclamped(from, to, progress)), 0, 4);
            foreach (var entry in navButtons) entry.Value.EnableInClassList("mk-tab--on", entry.Key == Order[highlighted]);
            if (!navButtons.TryGetValue(Order[from], out var a) || !navButtons.TryGetValue(Order[to], out var b) || indicator.parent == null) return;
            Rect ar = indicator.parent.WorldToLocal(a.worldBound), br = indicator.parent.WorldToLocal(b.worldBound);
            indicator.style.left = Mathf.LerpUnclamped(ar.x, br.x, progress);
            indicator.style.top = Mathf.LerpUnclamped(ar.y, br.y, progress);
            indicator.style.height = Mathf.LerpUnclamped(ar.height, br.height, progress);
            indicator.style.width = Mathf.Max(0f, Mathf.LerpUnclamped(ar.width, br.width, progress));
        }
        private void OnSwipeStart(PointerDownEvent evt)
        {
            if (!initialized || swipePointer != -1 || evt.button != 0 || overlayOpen?.Invoke() == true) return;
            for (var target = evt.target as VisualElement; target != null; target = target.parent)
            {
                if (target.ClassListContains("lb-no-swipe")) return;
                if (target == pageHost) break;
            }
            swipePointer = evt.pointerId; swipeStart = swipePrevious = evt.position;
            swipeStartedAt = swipePreviousAt = evt.timestamp; swipeAxis = SwipeAxis.Pending;
        }
        private void OnSwipeMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != swipePointer) return;
            if (overlayOpen?.Invoke() == true) { CancelSwipe(); return; }
            Vector2 point = evt.position, delta = point - swipeStart;
            if (swipeAxis == SwipeAxis.Pending)
            {
                swipeAxis = SwipeDecision.Lock(delta.x, delta.y);
                if (swipeAxis == SwipeAxis.Horizontal) strip.BeginDrag();
            }
            if (swipeAxis != SwipeAxis.Horizontal) return;
            float width = Mathf.Max(1f, pageHost.layout.width);
            float velocity = -(point.x - swipePrevious.x) / width / Mathf.Max(0.001f, (evt.timestamp - swipePreviousAt) / 1000f);
            strip.Drag(-delta.x / width, velocity);
            swipePrevious = point; swipePreviousAt = evt.timestamp;
            pageHost.CapturePointer(evt.pointerId); Apply(); evt.StopPropagation();
        }
        private void OnSwipeEnd(PointerUpEvent evt)
        {
            if (evt.pointerId != swipePointer) return;
            Vector2 delta = (Vector2)evt.position - swipeStart;
            if (swipeAxis == SwipeAxis.Horizontal)
            {
                int offset = SwipeDecision.Decide(delta.x, delta.y, evt.timestamp - swipeStartedAt, overlayOpen?.Invoke() == true, false, swipeAxis);
                if (evt.timestamp - swipePreviousAt > 100) strip.Motion.Velocity = 0f;
                int previous = strip.Current; strip.Release(offset);
                if (previous != strip.Current) PageShown?.Invoke(Order[strip.Current]);
                Apply(); evt.StopPropagation();
            }
            ReleasePointer();
        }
        private void CancelSwipe()
        {
            if (strip.Dragging) { strip.Release(0); Apply(); }
            ReleasePointer();
        }
        private void ReleasePointer()
        {
            int pointer = swipePointer; swipePointer = -1;
            if (pointer >= 0 && pageHost.HasPointerCapture(pointer)) pageHost.ReleasePointer(pointer);
        }
    }
}
