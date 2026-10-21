using System;
using System.Collections.Generic;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>Private room surface. The launcher owns its Relay and waiting protocol.</summary>
    public sealed class PrivateLobbyPanel : IDisposable
    {
        private readonly MkPanel panel = new MkPanel { name = "private-lobby" };
        private readonly MatchLauncher launcher;
        private readonly LobbyContextMenu menu;
        private readonly LobbyToast toast;
        private readonly Action<string> openPlayer, invite;
        private readonly RoomView room;
        private readonly Label code = Text("Creating room…", "private-code");
        private readonly Label meta = Text("", "private-meta");
        private readonly Button start, copy;
        private readonly MkPanel confirm = new MkPanel { name = "private-kick" };
        private string localId;
        private bool disposed;
        public VisualElement Root => panel;

        public PrivateLobbyPanel(VisualElement shell, LobbyOverlayRouter router, LoadoutCatalog catalog,
            LobbyContextMenu menu, LobbyToast toast, Action<string> openPlayer, Action<string> invite)
        {
            this.menu = menu; this.toast = toast; this.openPlayer = openPlayer; this.invite = invite;
            launcher = new MatchLauncher(catalog);
            launcher.Changed += Refresh;
            panel.AddToClassList("private-lobby-host");
            var styleSheet = Resources.Load<StyleSheet>("Lobby/PrivateLobby");
            if (styleSheet != null) panel.styleSheets.Add(styleSheet);
            panel.Surface.AddToClassList("private-lobby-panel");
            panel.BindRouter(router, "private-lobby");
            panel.Opened += () => shell.Q<VisualElement>("toast-dock")?.BringToFront();
            TrackedText.Track(panel.Title);
            var tag = Text("PRIVATE", "private-tag");
            panel.Title.parent.Insert(1, tag);
            panel.Closed += () => { room.ResetDrag(); confirm.Close(); launcher.Cancel(); };
            room = new RoomView(menu, () => localId, (a, b) => launcher.PrivateAction(2, a, b), MenuRows, InvitePlayers, "+ Invite");
            panel.Body.Add(room.Root);
            var codeRow = new VisualElement(); codeRow.AddToClassList("private-code-row");
            codeRow.Add(code);
            copy = Button("Copy", () =>
            {
                if (string.IsNullOrEmpty(launcher.JoinCode)) return;
                GUIUtility.systemCopyBuffer = launcher.JoinCode; toast?.Show("Code copied");
            });
            // The code block scrolls if the viewport is short; Start and Leave sit in a fixed footer below it.
            var info = new ScrollView { mode = ScrollViewMode.Vertical, horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            info.AddToClassList("private-info"); LobbyScrollbars.Hide(info); panel.Body.Add(info);
            codeRow.Add(copy); info.Add(codeRow);
            info.Add(Button("Invite players", InvitePlayers));
            info.Add(meta);
            var footer = new VisualElement(); footer.AddToClassList("private-footer"); panel.Body.Add(footer);
            start = Button("Start", () => launcher.PrivateAction(5));
            start.AddToClassList("private-start"); footer.Add(start);
            var leave = Button("Leave", panel.Close); leave.AddToClassList("private-leave"); footer.Add(leave);
            shell.Add(panel);
            confirm.Title.text = "Remove player?"; confirm.BindRouter(router, "private-lobby-kick"); shell.Add(confirm);
        }

        private static Label Text(string text, string css)
        {
            var label = new Label { enableRichText = false, pickingMode = PickingMode.Ignore };
            label.AddToClassList(css); TrackedText.Track(label); TrackedText.SetText(label, text); return label;
        }
        private static Button Button(string text, Action action)
        {
            var button = new Button(action); button.AddToClassList("mk-btn");
            button.Add(Text(text, "private-button-label")); return button;
        }
        private void Identity()
        {
            localId = BackendServices.Account.Current?.PlayerId ?? "local-host";
        }
        private string LocalName() => HandleDisplay.Display(BackendServices.LastKnownState?.Profile,
            PlayerProfile.Instance != null ? PlayerProfile.Instance.Username : "Player");

        // Entry point shared with the game-mode page.
        public void Open(LobbyModeSelection selection)
        {
            if (disposed) return;
            if (panel.IsOpen) panel.Close();
            Identity(); ResetView(); panel.Open();
            launcher.HostPrivate(selection.Mode, localId, LocalName());
            Refresh();
        }
        public void Join(string joinCode)
        {
            if (disposed) return;
            if (string.IsNullOrWhiteSpace(joinCode)) { toast?.Show("Enter the host's join code"); return; }
            if (panel.IsOpen) panel.Close();
            Identity(); ResetView(); panel.Open();
            launcher.JoinPrivate(joinCode, localId, LocalName()); Refresh();
        }
        private void ResetView()
        {
            room.Clear();
            TrackedText.SetText(panel.Title, "1v1");
        }

        private void Refresh()
        {
            if (disposed || !panel.IsOpen) return;
            var model = launcher.PrivateLobby;
            bool host = model != null && model.IsHost(localId);
            start.style.display = host ? DisplayStyle.Flex : DisplayStyle.None;
            start.SetEnabled(model != null && model.CanStart(localId));
            copy.SetEnabled(!string.IsNullOrEmpty(launcher.JoinCode));
            TrackedText.SetText(code, launcher.JoinCode ?? "Creating room…");
            bool failed = launcher.CurrentPhase == MatchLauncher.Phase.Failed;
            string message = failed ? launcher.FailureMessage : model == null ? "Connecting to host…"
                : model.Capacity == 4 ? "Coming soon: 4-player matches"
                : model.Starting ? "Starting match…"
                : model.CanStart(model.HostId) ? host ? "Ready when you are" : "Waiting for the host to start"
                : "Waiting for an invited player";
            TrackedText.SetText(meta, message);
            bool starting = model != null && model.Starting;
            start.SetEnabled(!failed && !starting && launcher.PrivateReady);
            if (starting) { room.ResetDrag(); confirm.Close(); }
            if (model == null) return;
            TrackedText.SetText(panel.Title, ModeDefinition.For(model.Mode).DisplayName);
            room.Render(model);
        }
        private void InvitePlayers()
        {
            if (string.IsNullOrEmpty(launcher.JoinCode)) { toast?.Show("The room is still being created"); return; }
            invite?.Invoke(launcher.JoinCode);
        }
        private IList<LobbyMenuItem> MenuRows(int seat)
        {
            var model = launcher.PrivateLobby;
            var rows = new List<LobbyMenuItem>();
            if (model == null || model.Player(seat) == null) return rows;
            rows.Add(new LobbyMenuItem("View player", () => openPlayer?.Invoke(model.Player(seat))));
            if (model.IsHost(localId) && !model.Starting)
            {
                if (!model.IsHost(model.Player(seat))) rows.Add(new LobbyMenuItem("Make host", () => launcher.PrivateAction(3, seat)));
                if (model.Player(seat) != localId) rows.Add(new LobbyMenuItem("Kick", () => ConfirmKick(seat)));
            }
            return rows;
        }
        private void ConfirmKick(int seat)
        {
            confirm.Body.Clear();
            confirm.Body.Add(new Label("Remove this player from the private lobby?") { enableRichText = false });
            confirm.Body.Add(Button("Kick player", () => { confirm.Close(); launcher.PrivateAction(4, seat); }));
            confirm.Body.Add(Button("Cancel", confirm.Close)); confirm.Open();
        }
        public void Update()
        {
            if (!panel.IsOpen || disposed) return;
            if (localId != (BackendServices.Account.Current?.PlayerId ?? "local-host")) { panel.Close(); return; }
            launcher.Update();
        }
        public void Dispose()
        {
            if (disposed) return;
            panel.Close(); disposed = true; launcher.Changed -= Refresh; launcher.Dispose();
            room.Dispose(); panel.RemoveFromHierarchy(); confirm.RemoveFromHierarchy();
        }
    }
}
