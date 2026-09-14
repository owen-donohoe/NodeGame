using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// A full-screen page that slides in from the right over the whole lobby,
    /// chrome included: Settings and Match history. Its own idea, not a tab -
    /// the page track underneath does not move.
    ///
    /// The subclass supplies a layout with a back button named "push-back";
    /// this class owns opening, closing and the safe area.
    /// </summary>
    public abstract class LobbyPushPage
    {
        private readonly SafeAreaBinder safeArea;

        public VisualElement Root { get; private set; }

        public bool IsOpen { get; private set; }

        protected LobbyPushPage(string name, VisualTreeAsset layout, string missingNote)
        {
            Root = new VisualElement();
            Root.name = name;
            Root.AddToClassList("lb-push");

            if (layout != null)
            {
                layout.CloneTree(Root);
            }
            else
            {
                // Same degradation as every page: labelled, never an exception.
                Label note = new Label(missingNote);
                note.AddToClassList("lb-stub__body");
                Root.Add(note);
            }

            // The page covers the notch area, so its content needs the insets
            // the shell's own safe area gets.
            VisualElement content = Root.Q<VisualElement>("push-safe-area");
            safeArea = new SafeAreaBinder(content != null ? content : Root);

            Button back = Root.Q<Button>("push-back");
            if (back != null) back.clicked += Close;
        }

        public void Open()
        {
            IsOpen = true;
            Root.AddToClassList("lb-push--open");
            OnOpen();
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            Root.RemoveFromClassList("lb-push--open");
            OnClose();
        }

        /// <summary>Called every frame by the shell; cheap unless the screen changed.</summary>
        public void Update()
        {
            safeArea.Update();
        }

        protected virtual void OnOpen() { }

        protected virtual void OnClose() { }
    }
}
