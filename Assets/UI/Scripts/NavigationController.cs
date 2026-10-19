using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Owns which tab page is showing and keeps the tab bar in step with it.
    ///
    /// Pages sit side by side in one track inside the page host, in the order
    /// they are registered, and changing page slides the track - so a tab press
    /// scrolls to its page rather than cutting. Every page stays in the tree,
    /// which also keeps each page's scroll position and element state between
    /// visits for free.
    ///
    /// Each page is sized to the host here rather than in USS, because the
    /// page count is decided by registration, not by a stylesheet.
    ///
    /// Profile, Settings and Match history are not pages in this sense: they
    /// are overlays above the chrome, owned by the shell.
    /// </summary>
    public class NavigationController
    {
        private readonly VisualElement pageHost;
        private readonly VisualElement track;
        private readonly List<LobbyPage> pages = new List<LobbyPage>();
        private readonly Dictionary<LobbyPageID, Button> navButtons =
            new Dictionary<LobbyPageID, Button>();

        private LobbyPage currentPage;
        private System.Func<bool> overlayOpen;
        private int swipePointer = -1;
        private Vector2 swipeStart;
        private long swipeStartedAt;
        private SwipeAxis swipeAxis;

        /// <summary>Raised after a page becomes the current one.</summary>
        public event System.Action<LobbyPageID> PageShown;

        public LobbyPageID? CurrentPageID
        {
            get { return currentPage != null ? currentPage.ID : (LobbyPageID?)null; }
        }

        public NavigationController(VisualElement pageHost)
        {
            this.pageHost = pageHost;
            // Empty page space must receive gestures too; Home's art ignores picking.
            pageHost.pickingMode = PickingMode.Position;

            track = new VisualElement();
            track.name = "page-track";
            track.AddToClassList("lb-page-track");
            track.pickingMode = PickingMode.Ignore;
            pageHost.Add(track);

            pageHost.RegisterCallback<GeometryChangedEvent>(evt => Layout(false));
            pageHost.RegisterCallback<PointerDownEvent>(OnSwipeStart, TrickleDown.TrickleDown);
            pageHost.RegisterCallback<PointerMoveEvent>(OnSwipeMove, TrickleDown.TrickleDown);
            pageHost.RegisterCallback<PointerUpEvent>(OnSwipeEnd, TrickleDown.TrickleDown);
            pageHost.RegisterCallback<PointerCancelEvent>(evt => CancelSwipe());
            pageHost.RegisterCallback<PointerCaptureOutEvent>(evt => swipePointer = -1);
        }

        public void BindSwipes(System.Func<bool> anyOverlayOpen) { overlayOpen = anyOverlayOpen; }

        private void OnSwipeStart(PointerDownEvent evt)
        {
            if (swipePointer != -1 || evt.button != 0 || overlayOpen?.Invoke() == true) return;
            for (VisualElement target = evt.target as VisualElement; target != null; target = target.parent)
            {
                if (target.ClassListContains("lb-no-swipe")) return;
                if (target == pageHost) break;
            }
            swipePointer = evt.pointerId;
            swipeStart = evt.position;
            swipeStartedAt = evt.timestamp;
            swipeAxis = SwipeAxis.Pending;
        }

        private void OnSwipeMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != swipePointer) return;
            if (overlayOpen?.Invoke() == true) { CancelSwipe(); return; }
            Vector2 delta = (Vector2)evt.position - swipeStart;
            if (swipeAxis == SwipeAxis.Pending) swipeAxis = SwipeDecision.Lock(delta.x, delta.y);
            if (swipeAxis != SwipeAxis.Horizontal) return;
            pageHost.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnSwipeEnd(PointerUpEvent evt)
        {
            if (evt.pointerId != swipePointer) return;
            Vector2 delta = (Vector2)evt.position - swipeStart;
            int offset = SwipeDecision.Decide(delta.x, delta.y, evt.timestamp - swipeStartedAt,
                overlayOpen?.Invoke() == true, false, swipeAxis);
            bool horizontal = swipeAxis == SwipeAxis.Horizontal;
            CancelSwipe();
            if (horizontal) evt.StopPropagation();
            int index = currentPage != null ? pages.IndexOf(currentPage) : -1;
            if (offset != 0 && index >= 0 && index + offset >= 0 && index + offset < pages.Count)
                Show(pages[index + offset].ID);
        }

        private void CancelSwipe()
        {
            int pointer = swipePointer;
            swipePointer = -1;
            if (pointer >= 0 && pageHost.HasPointerCapture(pointer)) pageHost.ReleasePointer(pointer);
        }

        /// <summary>
        /// Wires one tab button to a page. Safe to call before the page is
        /// registered - the button does nothing until it is.
        /// </summary>
        public void BindNavButton(LobbyPageID id, Button button)
        {
            if (button == null) return;

            navButtons[id] = button;
            button.clicked += () => Show(id);
        }

        public void Register(LobbyPage page)
        {
            if (page == null) return;

            pages.Add(page);
            track.Add(page.Root);
            Layout(false);
        }

        public bool Has(LobbyPageID id)
        {
            return IndexOf(id) >= 0;
        }

        /// <summary>
        /// Slides to a page. A request for a page that is not registered is
        /// ignored rather than throwing, so a half-built lobby stays usable.
        /// </summary>
        public void Show(LobbyPageID id)
        {
            int index = IndexOf(id);
            if (index < 0) return;

            LobbyPage target = pages[index];
            if (currentPage == target) return;

            bool first = currentPage == null;

            if (currentPage != null) currentPage.OnHide();

            currentPage = target;
            currentPage.OnShow();

            Layout(!first);
            RefreshNavButtons();

            if (PageShown != null) PageShown(id);
        }

        /// <summary>
        /// Sizes the track and pages to the host and positions the track on the
        /// current page. The first placement is not animated, so the lobby
        /// opens on Home rather than sliding to it from Shop.
        /// </summary>
        private void Layout(bool animate)
        {
            float width = pageHost.resolvedStyle.width;
            if (float.IsNaN(width) || width <= 0f) return;

            track.style.width = width * pages.Count;
            for (int i = 0; i < pages.Count; i++)
                pages[i].Root.style.width = width;

            int index = currentPage != null ? pages.IndexOf(currentPage) : 0;

            track.style.transitionDuration = animate
                ? StyleKeyword.Null
                : new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(0) });
            track.style.translate = new Translate(-width * index, 0);
        }

        private void RefreshNavButtons()
        {
            foreach (KeyValuePair<LobbyPageID, Button> entry in navButtons)
            {
                bool active = currentPage != null && entry.Key == currentPage.ID;
                entry.Value.EnableInClassList("mk-tab--on", active);
            }
        }

        private int IndexOf(LobbyPageID id)
        {
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i].ID == id) return i;
            }
            return -1;
        }
    }
}
