using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    public class AsyncStateView : VisualElement
    {
        private readonly VisualElement content = new VisualElement();
        private readonly VisualElement status = new VisualElement();
        private readonly Label banner = new Label();
        private IVisualElementScheduledItem reveal;
        public VisualElement Content => content;
        public event Action RetryRequested;

        public AsyncStateView()
        {
            AddToClassList("lb-async");
            var styles = Resources.Load<StyleSheet>("Lobby/LobbyBlocks");
            if (styles != null) styleSheets.Add(styles);
            banner.enableRichText = false;
            Add(banner);
            Add(content);
            Add(status);
        }

        public void Render(AsyncState state, long now, string message = null,
            LobbyIconKind emptyIcon = LobbyIconKind.None, string actionLabel = null, Action action = null)
        {
            reveal?.Pause();
            status.Clear();
            content.style.display = state.HasContent ? DisplayStyle.Flex : DisplayStyle.None;
            banner.style.display = state.ShowBanner ? DisplayStyle.Flex : DisplayStyle.None;
            banner.text = state.IsOffline ? "Offline — showing saved content" : "Could not refresh — showing saved content";
            if (state.Status == AsyncStatus.Loading)
            {
                Action show = () =>
                {
                    var spinner = new VisualElement { tooltip = "Loading" };
                    spinner.AddToClassList("lb-spinner");
                    status.Add(spinner);
                    int degrees = 0;
                    spinner.schedule.Execute(() => spinner.style.rotate = new Rotate(new Angle(degrees = (degrees + 30) % 360))).Every(80);
                };
                if (state.ShowLoading(now)) show();
                else
                {
                    reveal = schedule.Execute(show);
                    reveal.ExecuteLater(Math.Max(0, 300 - (now - state.StartedAt)));
                }
                return;
            }
            if (state.Status == AsyncStatus.Ready) return;
            if (emptyIcon != LobbyIconKind.None && state.Status == AsyncStatus.Empty)
                status.Add(new LobbyIcon(emptyIcon));
            status.Add(new Label(message ?? (state.Status == AsyncStatus.Empty ? "Nothing here yet" :
                state.Status == AsyncStatus.Offline ? "You're offline" : "Something went wrong")) { enableRichText = false });
            if (state.Status == AsyncStatus.Empty)
            {
                if (action != null && !string.IsNullOrEmpty(actionLabel)) status.Add(new Button(action) { text = actionLabel });
            }
            else status.Add(new Button(() => RetryRequested?.Invoke()) { text = "Retry" });
        }
    }
}
