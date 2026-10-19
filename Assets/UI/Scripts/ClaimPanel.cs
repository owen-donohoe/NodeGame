using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    public sealed class ClaimPanel
    {
        public VisualElement Root { get; }
        public bool IsOpen { get; private set; }
        public event Action Opened;
        public event Action Closed;
        public event Action Confirmed;
        public event Action Cancelled;
        private readonly Label title;
        private readonly VisualElement details;
        private readonly Button primary;

        public ClaimPanel()
        {
            Root = new VisualElement { name = "claim-overlay" };
            var layout = Resources.Load<VisualTreeAsset>("Lobby/ClaimPanel");
            if (layout != null) layout.CloneTree(Root);
            Root.style.position = Position.Absolute;
            Root.style.left = Root.style.right = Root.style.top = Root.style.bottom = 0;
            title = Root.Q<Label>("claim-title");
            details = Root.Q<VisualElement>("claim-details");
            primary = Root.Q<Button>("claim-primary");
            var info = Root.Q<Button>("claim-info");
            if (info != null) info.clicked += () =>
            {
                if (details != null) details.style.display = details.resolvedStyle.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            };
            if (primary != null) primary.clicked += () =>
            {
                if (!IsOpen) return;
                Close();
                Confirmed?.Invoke();
            };
            Root.Q<VisualElement>("claim-scrim")?.RegisterCallback<PointerDownEvent>(evt =>
            {
                Cancel();
                evt.StopPropagation();
            });
            Root.style.display = DisplayStyle.None;
        }

        public void Open(ClaimRequest request)
        {
            LobbyScrollbars.Hide(Root);
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (IsOpen) Cancel();
            if (title != null) { title.text = request.Title; title.enableRichText = false; }
            if (primary != null) primary.text = request.PrimaryLabel();
            if (details != null)
            {
                details.Clear();
                details.Add(new Label(request.Description) { enableRichText = false });
                if (request.Disclosure != null)
                    foreach (string line in request.Disclosure) details.Add(new Label(line ?? "") { enableRichText = false });
                details.style.display = DisplayStyle.None;
            }
            IsOpen = true;
            Root.style.display = DisplayStyle.Flex;
            Opened?.Invoke();
        }

        public void Cancel()
        {
            if (!IsOpen) return;
            Close();
            Cancelled?.Invoke();
        }

        private void Close()
        {
            IsOpen = false;
            Root.style.display = DisplayStyle.None;
            Closed?.Invoke();
        }
    }
}
