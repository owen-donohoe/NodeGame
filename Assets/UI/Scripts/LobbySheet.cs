using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The one bottom sheet the lobby has. Pages do not build their own: they
    /// hand it a content element (the battle sheet, rename, a shop item) and
    /// it does the scrim, the slide, the handle and the two detents - so every
    /// sheet in the lobby moves and dismisses the same way.
    ///
    /// The sheet is pinned to the bottom edge by construction (Lobby.uss), so
    /// it cannot render off-screen. It is never display-toggled: a closed sheet
    /// is translated below the screen and the scrim stops taking taps, which is
    /// what keeps the slide animating in both directions.
    /// </summary>
    public class LobbySheet
    {
        // Drag distance on the handle that changes detent, from the prototype.
        private const float HandleDragThreshold = 44f;

        private readonly VisualElement scrim;
        private readonly VisualElement sheet;
        private readonly VisualElement body;

        private VisualElement content;
        private bool dragging;
        private float dragStartY;

        /// <summary>
        /// Raised after the sheet closes, with the content it was showing.
        /// Content that owns live work (a connection attempt) cancels it here,
        /// whatever closed the sheet - the scrim, the handle, or a button.
        /// </summary>
        public event System.Action<VisualElement> Closed;

        public LobbySheet(VisualElement root)
        {
            scrim = root.Q<VisualElement>("sheet-scrim");
            sheet = root.Q<VisualElement>("sheet");
            body = root.Q<VisualElement>("sheet-body");

            if (scrim != null)
            {
                scrim.RegisterCallback<PointerDownEvent>(evt =>
                {
                    Close();
                    evt.StopPropagation();
                });
            }

            WireHandle(root.Q<VisualElement>("sheet-handle"));
            SetOpen(false);
        }

        public bool IsOpen { get; private set; }

        /// <summary>
        /// Takes a page's sheet content out of the page layout it was authored
        /// in, carrying the page's stylesheets with it. A page keeps its sheet
        /// content in its own UXML so the two are edited together; this is
        /// what lets that content still be styled once it lives in the sheet,
        /// outside the page's subtree. Returns null if the element is missing.
        /// </summary>
        public static VisualElement Lift(VisualElement pageRoot, string contentName)
        {
            if (pageRoot == null) return null;

            VisualElement content = pageRoot.Q<VisualElement>(contentName);
            if (content == null) return null;

            content.RemoveFromHierarchy();

            for (int i = 0; i < pageRoot.styleSheets.count; i++)
                content.styleSheets.Add(pageRoot.styleSheets[i]);

            return content;
        }

        /// <summary>Whether this content is what the sheet is showing now.</summary>
        public bool IsShowing(VisualElement candidate)
        {
            return IsOpen && candidate != null && content == candidate;
        }

        public void Open(VisualElement newContent, bool large = false)
        {
            if (body == null || newContent == null) return;

            if (IsOpen && content != newContent) Close();

            if (newContent.parent != body)
            {
                body.Clear();
                body.Add(newContent);
            }

            content = newContent;
            if (sheet != null) sheet.EnableInClassList("lb-sheet--large", large);
            SetOpen(true);
        }

        public void Close()
        {
            if (!IsOpen) return;

            SetOpen(false);

            if (Closed != null) Closed(content);
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;

            if (sheet != null) sheet.EnableInClassList("ui-sheet--open", open);
            if (scrim != null)
            {
                scrim.EnableInClassList("ui-scrim--on", open);
                scrim.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
            }
        }

        /// <summary>Drag the handle up to expand, down to shrink or close.</summary>
        private void WireHandle(VisualElement handle)
        {
            if (handle == null) return;

            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                dragging = true;
                dragStartY = evt.position.y;
                handle.CapturePointer(evt.pointerId);
            });

            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging || sheet == null) return;

                float dy = evt.position.y - dragStartY;

                if (dy < -HandleDragThreshold)
                {
                    sheet.AddToClassList("lb-sheet--large");
                    dragging = false;
                }
                else if (dy > HandleDragThreshold)
                {
                    if (sheet.ClassListContains("lb-sheet--large"))
                        sheet.RemoveFromClassList("lb-sheet--large");
                    else
                        Close();
                    dragging = false;
                }
            });

            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                dragging = false;
                handle.ReleasePointer(evt.pointerId);
            });
        }
    }
}
