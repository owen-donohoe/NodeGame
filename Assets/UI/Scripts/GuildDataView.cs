using System;
using System.Threading.Tasks;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    internal sealed class GuildDataView : VisualElement
    {
        private AsyncState state = new AsyncState();
        private int request;
        private readonly VisualElement status = GuildViews.Card(null);
        private readonly Label banner = GuildViews.Text("", "mk-card");
        private IVisualElementScheduledItem reveal;
        public VisualElement Content { get; } = new VisualElement();
        public event Action RetryRequested;
        internal static long Now => (long)(Time.realtimeSinceStartupAsDouble * 1000);

        public GuildDataView()
        {
            Add(banner); Add(Content); Add(status);
            Present();
        }

        private void Present(string message = null)
        {
            reveal?.Pause(); status.Clear(); status.style.display = DisplayStyle.None;
            Content.style.display = state.HasContent ? DisplayStyle.Flex : DisplayStyle.None;
            banner.style.display = state.ShowBanner ? DisplayStyle.Flex : DisplayStyle.None;
            banner.text = state.IsOffline ? "Offline · showing saved content" : "Could not refresh · showing saved content";
            if (state.Status == AsyncStatus.Ready) return;
            if (state.Status == AsyncStatus.Loading)
            {
                Action show = () =>
                {
                    status.Add(GuildViews.Text("Loading guild data…", "mk-muted")); status.style.display = DisplayStyle.Flex;
                };
                if (state.ShowLoading(Now)) show();
                else reveal = schedule.Execute(show).StartingIn(Math.Max(0, 300 - (Now - state.StartedAt)));
                return;
            }
            status.style.display = DisplayStyle.Flex;
            status.Add(GuildViews.Text(message ?? (state.Status == AsyncStatus.Empty ? "Nothing here yet." : "Couldn't load guild data.")));
            if (state.Status != AsyncStatus.Empty)
                status.Add(GuildViews.Button("Retry", () => RetryRequested?.Invoke()));
        }

        public async Task Load<T>(Func<Task<T>> fetch, Action<T> render, Func<T, bool> hasContent, string empty)
        {
            int token = ++request;
            string account = BackendServices.Account.Current?.PlayerId;
            state = state.Reduce(AsyncResult.Loading, Now); Present();
            try
            {
                T data = await fetch();
                if (token != request || account != BackendServices.Account.Current?.PlayerId) return;
                Content.Clear();
                bool has = hasContent(data);
                if (has) render(data);
                state = state.Reduce(has ? AsyncResult.Ready : AsyncResult.Empty, Now); Present(empty);
            }
            catch (Exception)
            {
                if (token != request || account != BackendServices.Account.Current?.PlayerId) return;
                bool offline = BackendServices.SimulatedOffline || Application.internetReachability == NetworkReachability.NotReachable;
                state = state.Reduce(offline ? AsyncResult.Offline : AsyncResult.Error, Now);
                Present(offline ? "You're offline. Connect to refresh guild data." : "Couldn't load guild data. Try again.");
            }
        }

        public void Invalidate(bool clear = false)
        {
            request++;
            if (clear) { Content.Clear(); state = new AsyncState(); }
            state = state.Reduce(state.HasContent ? AsyncResult.Ready : AsyncResult.Empty, Now); Present();
        }
    }
}
