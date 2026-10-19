using System;
using System.Threading.Tasks;
using NodeWar.Backend;
using UnityEngine;

namespace NodeWar.Lobby
{
    internal sealed class GuildDataView : AsyncStateView
    {
        private AsyncState state = new AsyncState();
        private int request;
        internal static long Now => (long)(Time.realtimeSinceStartupAsDouble * 1000);

        public async Task Load<T>(Func<Task<T>> fetch, Action<T> render, Func<T, bool> hasContent, string empty)
        {
            int token = ++request;
            string account = BackendServices.Account.Current?.PlayerId;
            state = state.Reduce(AsyncResult.Loading, Now);
            Render(state, Now);
            try
            {
                T data = await fetch();
                if (token != request || account != BackendServices.Account.Current?.PlayerId) return;
                Content.Clear();
                bool has = hasContent(data);
                if (has) render(data);
                state = state.Reduce(has ? AsyncResult.Ready : AsyncResult.Empty, Now);
                Render(state, Now, empty, LobbyIconKind.Guild);
            }
            catch (Exception)
            {
                if (token != request || account != BackendServices.Account.Current?.PlayerId) return;
                bool offline = BackendServices.SimulatedOffline || Application.internetReachability == NetworkReachability.NotReachable;
                state = state.Reduce(offline ? AsyncResult.Offline : AsyncResult.Error, Now);
                Render(state, Now, offline ? "You're offline. Connect to refresh guild data." : "Couldn't load guild data. Try again.");
            }
        }

        public void Invalidate(bool clear = false)
        {
            request++;
            if (clear) { Content.Clear(); state = new AsyncState(); }
            state = state.Reduce(state.HasContent ? AsyncResult.Ready : AsyncResult.Empty, Now);
            Render(state, Now);
        }
    }
}
