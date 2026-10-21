using System;
using System.Collections.Generic;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;
namespace NodeWar.Lobby
{
    public sealed class LanRoomSheet : IDisposable
    {
        private readonly MkSheet sheet = new MkSheet { name = "lan-room-sheet" };
        private readonly MkPanel confirm = new MkPanel { name = "lan-kick-confirm" };
        private readonly LanRoomState state = new LanRoomState();
        private readonly MatchLauncher launcher;
        private readonly RoomView room;
        private readonly LobbyContextMenu menu;
        private readonly LobbyToast toast;
        private readonly Action<string> profile;
        private readonly Label status = GuildViews.Text("", "mk-muted");
        private readonly Button play;
        private readonly VisualElement roomPage = new VisualElement();
        private readonly SegmentedSwitch modes;
        private string localId, address = "127.0.0.1";
        private bool disposed;
        public LanRoomSheet(VisualElement shell, LobbyOverlayRouter router, LoadoutCatalog catalog,
            LobbyContextMenu menu, LobbyToast toast, Action<string> profile)
        {
            this.menu = menu; this.toast = toast; this.profile = profile;
            launcher = new MatchLauncher(catalog); launcher.Changed += Refresh;
            sheet.styleSheets.Add(Resources.Load<StyleSheet>("Lobby/PrivateLobby"));
            sheet.styleSheets.Add(Resources.Load<StyleSheet>("Lobby/LanRoom"));
            sheet.SetScrollable(false); sheet.BindRouter(router, "lan-room"); shell.Add(sheet);
            sheet.Closed += () => { state.Leave(); room.ResetDrag(); menu.Close(); confirm.Close(); launcher.Cancel(); };
            sheet.Opened += () => { sheet.BringToFront(); shell.Q("toast-dock")?.BringToFront(); };
            room = new RoomView(menu, () => localId, (a, b) => launcher.PrivateAction(2, a, b), MenuRows);
            roomPage.AddToClassList("lan-room-page");
            modes = new SegmentedSwitch(new Button { text = "1v1" }, new Button { text = "2v2" }, new Button { text = "1v1v1v1" });
            modes.Changed += index => { state.SelectMode(index); launcher.ChangeLanRoomMode(state.Mode); };
            roomPage.Add(modes); roomPage.Add(room.Root); roomPage.Add(status);
            play = GuildViews.Button("Play", Play, "mk-btn--primary"); roomPage.Add(play);
            roomPage.Add(GuildViews.Button("Close room", () => ReturnToChoice()));
            confirm.Title.text = "Kick player?"; confirm.BindRouter(router, "lan-kick"); shell.Add(confirm);
            BackendServices.Account.Changed += AccountChanged;
        }
        private void AccountChanged(AccountInfo _) => sheet.Close();
        public void Open()
        {
            if (disposed) return;
            localId = LanRoomState.PlayerId(BackendServices.Account.Current?.PlayerId,
                BackendServices.Account is LocalAccountService, Guid.NewGuid().ToString("N"));
            ReturnToChoice(); sheet.Open(true);
        }
        private string LocalName() => HandleDisplay.Display(BackendServices.LastKnownState?.Profile,
            PlayerProfile.Instance != null ? PlayerProfile.Instance.Username : "Player");
        private void ReturnToChoice(string error = null, bool kicked = false)
        {
            if (kicked) state.Kicked(); else state.Leave();
            room.ResetDrag(); menu.Close(); confirm.Close(); launcher.Cancel();
            sheet.Body.Clear(); sheet.Body.Add(GuildViews.Text("LAN", "mk-h2"));
            if (!string.IsNullOrEmpty(error)) sheet.Body.Add(GuildViews.Text(error, "mk-muted"));
            sheet.Body.Add(GuildViews.Button("Host", Host));
            sheet.Body.Add(GuildViews.Button("Join", () => { state.Join(); ShowJoin(); }));
        }
        private void ShowJoin(string error = null)
        {
            sheet.Body.Clear(); sheet.Body.Add(GuildViews.Text("Join LAN room", "mk-h2"));
            var field = new TextField("IP address") { value = address }; field.AddToClassList("lan-address");
            field.RegisterValueChangedCallback(e => address = e.newValue); sheet.Body.Add(field);
            if (!string.IsNullOrEmpty(error)) sheet.Body.Add(GuildViews.Text(error, "mk-muted"));
            sheet.Body.Add(GuildViews.Button("Join", () =>
            {
                state.Connect(); sheet.Body.Clear(); sheet.Body.Add(GuildViews.Text("Connecting\u2026", "mk-h2"));
                sheet.Body.Add(GuildViews.Button("Cancel", () => { state.Join(); launcher.Cancel(); ShowJoin(); }));
                launcher.JoinLanRoom(address, localId, LocalName());
            }));
            sheet.Body.Add(GuildViews.Button("Cancel", () => ReturnToChoice()));
        }
        private void Host()
        {
            state.Host(); modes.Select(0); room.Clear(); ShowHost();
            launcher.HostLanRoom(state.Mode, localId, LocalName()); Refresh();
        }
        private void ShowHost()
        {
            roomPage.Clear(); roomPage.Add(modes); roomPage.Add(room.Root); roomPage.Add(status); roomPage.Add(play);
            roomPage.Add(GuildViews.Button("Close room", () => ReturnToChoice()));
            sheet.Body.Clear(); sheet.Body.Add(GuildViews.Text("LAN room", "mk-h2")); sheet.Body.Add(roomPage);
        }
        private void ShowGuest()
        {
            sheet.Body.Clear(); sheet.Body.Add(GuildViews.Text("LAN room", "mk-h2"));
            sheet.Body.Add(room.Root); sheet.Body.Add(status);
            sheet.Body.Add(GuildViews.Button("Leave", () => ReturnToChoice()));
        }
        private void Play()
        {
            if (state.PlaceholderPlay) { toast?.Show("4-player matches aren't live yet"); return; }
            launcher.PrivateAction(5);
        }
        private void Refresh()
        {
            if (!sheet.IsOpen || disposed) return;
            if (launcher.CurrentPhase == MatchLauncher.Phase.Failed)
            {
                string error = launcher.FailureMessage;
                if (state.Page == LanRoomPage.Guest) { ReturnToChoice(error, true); return; }
                if (state.Page == LanRoomPage.Connecting) { state.Join(); launcher.Cancel(); ShowJoin(error); return; }
                status.text = error; play.SetEnabled(false); return;
            }
            var model = launcher.PrivateLobby;
            if (state.Page == LanRoomPage.Connecting && model?.SeatOf(localId) >= 0) { state.Joined(); ShowGuest(); }
            if (state.Page != LanRoomPage.Host && state.Page != LanRoomPage.Guest) return;
            room.Render(model);
            status.text = model?.Starting == true ? "Starting match\u2026" : state.Page == LanRoomPage.Guest ? "Waiting for the host to play"
                : launcher.JoinCode == null ? "Creating room\u2026" : "IP: " + launcher.JoinCode;
            play.SetEnabled(model != null && !model.Starting && (state.PlaceholderPlay || launcher.PrivateReady));
        }
        private IList<LobbyMenuItem> MenuRows(int seat)
        {
            var rows = new List<LobbyMenuItem>(); var model = launcher.PrivateLobby;
            string id = model?.Player(seat); if (id == null) return rows;
            rows.Add(new LobbyMenuItem("View profile", () => profile?.Invoke(id)));
            if (model.IsHost(localId) && id != localId && !model.Starting)
                rows.Add(new LobbyMenuItem("Kick", () =>
                {
                    confirm.Body.Clear(); confirm.Body.Add(GuildViews.Text("Remove this player from the room?"));
                    confirm.Body.Add(GuildViews.Button("Kick", () =>
                    {
                        confirm.Close();
                        int currentSeat = launcher.PrivateLobby?.SeatOf(id) ?? -1;
                        if (currentSeat >= 0) launcher.PrivateAction(4, currentSeat);
                    }));
                    confirm.Body.Add(GuildViews.Button("Cancel", confirm.Close)); confirm.Open();
                }));
            return rows;
        }
        public void Update() { if (sheet.IsOpen && !disposed) launcher.Update(); }
        public void Dispose()
        {
            if (disposed) return;
            sheet.Close(); disposed = true; BackendServices.Account.Changed -= AccountChanged;
            launcher.Changed -= Refresh; launcher.Dispose(); room.Dispose(); sheet.RemoveFromHierarchy(); confirm.RemoveFromHierarchy();
        }
    }
}
