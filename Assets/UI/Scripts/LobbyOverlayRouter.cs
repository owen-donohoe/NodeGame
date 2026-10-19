using System;
using System.Collections.Generic;

namespace NodeWar.Lobby
{
    /// <summary>Dismiss through each overlay's own close path, including its cleanup.</summary>
    public sealed class LobbyOverlayRouter
    {
        private readonly Dictionary<string, Action> dismiss = new Dictionary<string, Action>();
        public LobbyRouteStack Stack { get; } = new LobbyRouteStack();

        public void Open(LobbyOverlay entry, Action close)
        {
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
            if (id == null) return;
            if (dismiss.TryGetValue(id, out Action close)) close?.Invoke();
            Closed(id);
        }

        public void CloseAll()
        {
            while (Stack.Top != null) Close(Stack.Top.ID);
        }
    }
}
