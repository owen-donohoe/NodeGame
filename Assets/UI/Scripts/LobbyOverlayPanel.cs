using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>A scrim-dismissed panel that uses the shell's overlay router.</summary>
    public sealed class LobbyOverlayPanel
    {
        public VisualElement Root { get; } = new VisualElement();
        public VisualElement Content { get; }
        public bool IsOpen { get; private set; }
        public event Action Opened;
        public event Action Closed;
        private readonly SafeAreaBinder safeArea;

        public LobbyOverlayPanel(string title)
        {
            var layout = Resources.Load<VisualTreeAsset>("Lobby/Panel");
            if (layout != null) layout.CloneTree(Root);
            Root.style.position = Position.Absolute;
            Root.style.left = Root.style.right = Root.style.top = Root.style.bottom = 0;
            var heading = Root.Q<Label>("panel-title");
            if (heading != null) { heading.text = title; heading.enableRichText = false; }
            Content = Root.Q<ScrollView>("panel-content") ?? new ScrollView();
            if (Content.parent == null) Root.Add(Content);
            Root.Q<Button>("panel-close")?.RegisterCallback<ClickEvent>(_ => Close());
            Root.Q<VisualElement>("panel-scrim")?.RegisterCallback<PointerDownEvent>(evt =>
            {
                Close();
                evt.StopPropagation();
            });
            safeArea = new SafeAreaBinder(Root.Q<VisualElement>("panel-safe-area"));
            Root.style.display = DisplayStyle.None;
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            Root.style.display = DisplayStyle.Flex;
            safeArea.Update();
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Root.style.display = DisplayStyle.None;
            Closed?.Invoke();
        }

        public void Update() { if (IsOpen) safeArea.Update(); }
    }
}
