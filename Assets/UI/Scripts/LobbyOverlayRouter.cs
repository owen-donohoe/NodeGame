using System;
using System.Collections.Generic;

namespace NodeWar.Lobby
{
    /// <summary>Dismiss through each overlay's own close path, including its cleanup.</summary>
    public sealed class LobbyOverlayRouter
    {
        private readonly Dictionary<string, Action> dismiss = new Dictionary<string, Action>();
        public LobbyRouteStack Stack { get; } = new LobbyRouteStack();

        public bool Allows(string id) => !Stack.Contains("searching") || id == "searching" || id == "teammate-invite";

        public void Open(LobbyOverlay entry, Action close)
        {
            if (!Allows(entry.ID)) { close?.Invoke(); return; }
            dismiss[entry.ID] = close;
            Stack.Push(entry);
        }

        public void Closed(string id)
        {
            dismiss.Remove(id);
            Stack.Pop(id);
        }

        public void Close(string id)
        {
            if (Stack.TryHandleBack(id)) return;
            CloseDirect(id);
        }

        private void CloseDirect(string id)
        {
            if (id == null) return;
            if (dismiss.TryGetValue(id, out Action close)) close?.Invoke();
            Closed(id);
        }

        public void CloseAll()
        {
            while (Stack.Top != null) CloseDirect(Stack.Top.ID);
        }
    }
}
