using System;
using System.Collections.Generic;

namespace NodeWar.Lobby
{
    public enum OverlayKind { Sheet, Panel, Fullscreen, Menu }
    public enum BackActionKind { CloseTop, GoHome, ConfirmExit }

    public readonly struct BackAction
    {
        public BackActionKind Kind { get; }
        public string ID { get; }
        public BackAction(BackActionKind kind, string id = null) { Kind = kind; ID = id; }
    }

    public sealed class LobbyOverlay
    {
        public string ID { get; }
        public OverlayKind Kind { get; }
        public bool HidesPills { get; }
        public bool HidesHeader { get; }
        public Func<bool> BackHandler { get; }

        public LobbyOverlay(string id, OverlayKind kind, bool hidesPills = false, bool hidesHeader = false,
            Func<bool> backHandler = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Overlay needs an id", nameof(id));
            ID = id; Kind = kind; HidesPills = kind == OverlayKind.Fullscreen && hidesPills; HidesHeader = hidesHeader;
            BackHandler = backHandler;
        }
    }

    public sealed class LobbyRouteStack
    {
        private readonly List<LobbyOverlay> entries = new List<LobbyOverlay>();
        public event Action Changed;
        public int Count => entries.Count;
        public LobbyOverlay Top => Count == 0 ? null : entries[Count - 1];
        public bool IsSheetOpen => entries.Exists(entry => entry.Kind == OverlayKind.Sheet);
        public bool AnyFullscreen => entries.Exists(entry => entry.Kind == OverlayKind.Fullscreen);
        public bool HidesPills => entries.Exists(entry => entry.HidesPills);

        public void Push(LobbyOverlay entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            entries.RemoveAll(item => item.ID == entry.ID);
            entries.Add(entry);
            Changed?.Invoke();
        }

        public bool Pop(string id)
        {
            if (entries.RemoveAll(entry => entry.ID == id) == 0) return false;
            Changed?.Invoke();
            return true;
        }

        public BackAction Back(LobbyPageID currentPage)
        {
            if (Top != null) return new BackAction(BackActionKind.CloseTop, Top.ID);
            return new BackAction(currentPage == LobbyPageID.Home ? BackActionKind.ConfirmExit : BackActionKind.GoHome);
        }

        public bool TryHandleBack(string id) => Top != null && Top.ID == id && Top.BackHandler?.Invoke() == true;
    }
}
