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
        private readonly LobbyElementMotion motion;

        public VisualElement Root { get; private set; }

        public bool IsOpen { get; private set; }
        public event System.Action Opened;
        public event System.Action Closed;

        protected LobbyPushPage(string name, VisualTreeAsset layout, string missingNote)
        {
            Root = new VisualElement();
            Root.name = name;
            Root.AddToClassList("lb-push");
            Root.style.display = DisplayStyle.None;
            motion = new LobbyElementMotion(Root);
            motion.Set(0f, LobbyMotion.PageTravel, 1f);

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
            if (IsOpen) return;
            IsOpen = true;
            Root.style.display = DisplayStyle.Flex;
            Root.SetEnabled(true);
            OnOpen();
            LobbyScrollbars.Hide(Root);
            Opened?.Invoke();
            motion.To(1f, 0f, 1f, LobbyMotion.PageSeconds);
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            Root.SetEnabled(false);
            motion.To(0f, LobbyMotion.PageTravel, 1f, LobbyMotion.PageSeconds,
                done: () => Root.style.display = DisplayStyle.None);
            OnClose();
            Closed?.Invoke();
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
