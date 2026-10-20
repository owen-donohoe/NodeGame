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
        private readonly VisualElement arena = new VisualElement();
        private readonly Label code = Text("Creating room…", "private-code");
        private readonly Label meta = Text("", "private-meta");
        private readonly Button start, copy, move;
        private readonly VisualElement[] seats = new VisualElement[4];
        private readonly MkPanel confirm = new MkPanel { name = "private-kick" };
        private string localId;
        private int selected = -1, dragSeat = -1;
        private Vector2 dragOrigin;
        private bool moving, dragged, suppressClick, disposed;
        private int lastRevision = -1;
        private PrivateLobbyModel rendered;
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
            panel.Closed += () => { confirm.Close(); launcher.Cancel(); selected = -1; moving = false; };
            arena.AddToClassList("private-arena"); panel.Body.Add(arena);
            move = Button("Move seats", () => { moving = !moving; selected = -1; RefreshSelection(); });
            move.AddToClassList("private-move");
            var codeRow = new VisualElement(); codeRow.AddToClassList("private-code-row");
            codeRow.Add(code);
            copy = Button("Copy", () =>
            {
                if (string.IsNullOrEmpty(launcher.JoinCode)) return;
                GUIUtility.systemCopyBuffer = launcher.JoinCode; toast?.Show("Code copied");
            });
            codeRow.Add(copy); panel.Body.Add(codeRow);
            panel.Body.Add(Button("Invite players", InvitePlayers));
            panel.Body.Add(meta);
            start = Button("Start", () => launcher.PrivateAction(5));
            start.AddToClassList("private-start"); panel.Body.Add(start);
            panel.Body.Add(Button("Leave", panel.Close));
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
            rendered = null; lastRevision = -1; moving = false; selected = -1; arena.Clear();
            TrackedText.SetText(panel.Title, "1v1");
        }

        private void Refresh()
        {
            if (disposed || !panel.IsOpen) return;
            var model = launcher.PrivateLobby;
            bool host = model != null && model.IsHost(localId);
            move.style.display = host ? DisplayStyle.Flex : DisplayStyle.None;
            start.style.display = host ? DisplayStyle.Flex : DisplayStyle.None;
            start.SetEnabled(model != null && model.CanStart(localId));
            copy.SetEnabled(!string.IsNullOrEmpty(launcher.JoinCode));
            TrackedText.SetText(code, launcher.JoinCode ?? "Creating room…");
            bool failed = launcher.CurrentPhase == MatchLauncher.Phase.Failed;
            string message = failed ? launcher.FailureMessage : model == null ? "Connecting to host…"
                : model.Capacity == 4 ? "Coming soon: 4-player matches"
                : model.CanStart(model.HostId) ? host ? "Ready when you are" : "Waiting for the host to start"
                : "Waiting for an invited player";
            TrackedText.SetText(meta, message);
            start.SetEnabled(!failed && launcher.PrivateReady);
            if (model == null) return;
            if (rendered != null && rendered.Mode == model.Mode && lastRevision == model.Revision) return;
            rendered = model; lastRevision = model.Revision;
            selected = -1;
            TrackedText.SetText(panel.Title, model.Mode == LobbyMode.Team2v2 ? "2v2" : model.Mode == LobbyMode.FreeForAll4 ? "1v1v1v1" : "1v1");
            arena.Clear(); Array.Clear(seats, 0, seats.Length);
            arena.EnableInClassList("private-arena--ffa", model.Mode == LobbyMode.FreeForAll4);
            var board = new VisualElement(); board.AddToClassList("private-board");
            board.Add(Text("Board", "private-board-label")); board.Add(move); arena.Add(board);
            for (int i = 0; i < model.Capacity; i++) BuildSeat(i, model);
            RefreshSelection();
        }

        private void BuildSeat(int index, PrivateLobbyModel model)
        {
            var card = new VisualElement { name = "private-seat-" + index };
            seats[index] = card;
            card.AddToClassList("private-seat"); card.AddToClassList("private-seat--" + model.Position(index).ToString().ToLowerInvariant());
            card.AddToClassList("private-seat--p" + model.Colour(index));
            string id = model.Player(index);
            if (id == null)
            {
                card.AddToClassList("private-seat--empty");
                card.generateVisualContent += ctx => DrawEmptySeat(ctx, card);
                var empty = Button("+ Invite", () => { if (moving) TapSeat(index); else InvitePlayers(); });
                empty.AddToClassList("private-invite-seat"); card.Add(empty);
            }
            else
            {
                string name = model.Name(index) ?? "Player";
                var face = Button("", () => TapSeat(index)); face.Clear(); face.AddToClassList("private-face");
                face.Add(Text(name.Length > 0 ? name.Substring(0, 1).ToUpperInvariant() : "?", "private-avatar"));
                var who = new VisualElement(); who.AddToClassList("private-who");
                // Flexible names fill the available width; they must not get a content width.
                var nameLabel = new Label(name) { enableRichText = false, pickingMode = PickingMode.Ignore };
                nameLabel.AddToClassList("private-name"); who.Add(nameLabel);
                var seatMeta = new VisualElement(); seatMeta.AddToClassList("private-seat-meta-row");
                if (model.IsHost(id))
                {
                    var crown = new VisualElement { pickingMode = PickingMode.Ignore };
                    crown.AddToClassList("private-crown"); seatMeta.Add(crown);
                }
                seatMeta.Add(Text(model.IsHost(id) ? "HOST" : "Seat " + (index + 1), "private-seat-meta"));
                who.Add(seatMeta);
                face.Add(who); card.Add(face);
                var more = Button("⋯", () => ShowMenu(card, index)); more.AddToClassList("private-more"); card.Add(more);
                menu.Attach(face, () => MenuRows(index));
                face.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0 || !model.IsHost(localId)) return;
                    dragSeat = index; dragOrigin = evt.position; dragged = false;
                });
                face.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (dragSeat != index || !model.IsHost(localId)) return;
                    if (((Vector2)evt.position - dragOrigin).sqrMagnitude < 100) return;
                    dragged = true; face.CapturePointer(evt.pointerId); card.AddToClassList("private-seat--selected");
                    evt.StopPropagation();
                });
                face.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (dragSeat != index) return;
                    dragSeat = -1;
                    if (face.HasPointerCapture(evt.pointerId)) face.ReleasePointer(evt.pointerId);
                    if (!dragged) return;
                    suppressClick = true;
                    for (int j = 0; j < model.Capacity; j++)
                        if (j != index && seats[j].worldBound.Contains(evt.position)) { Swap(index, j); break; }
                    RefreshSelection(); panel.schedule.Execute(() => suppressClick = false);
                    evt.StopImmediatePropagation();
                });
                face.RegisterCallback<PointerCancelEvent>(_ => { dragSeat = -1; dragged = false; RefreshSelection(); });
            }
            arena.Add(card);
        }
        private static void DrawEmptySeat(MeshGenerationContext context, VisualElement card)
        {
            var painter = context.painter2D;
            painter.strokeColor = new Color32(77, 61, 45, 255); painter.lineWidth = 2;
            var r = card.contentRect;
            for (float x = 8; x < r.width - 4; x += 12)
            {
                painter.BeginPath(); painter.MoveTo(new Vector2(x, 1)); painter.LineTo(new Vector2(Mathf.Min(x + 6, r.width - 4), 1)); painter.Stroke();
                painter.BeginPath(); painter.MoveTo(new Vector2(x, r.height - 1)); painter.LineTo(new Vector2(Mathf.Min(x + 6, r.width - 4), r.height - 1)); painter.Stroke();
            }
            for (float y = 8; y < r.height - 4; y += 12)
            {
                painter.BeginPath(); painter.MoveTo(new Vector2(1, y)); painter.LineTo(new Vector2(1, Mathf.Min(y + 6, r.height - 4))); painter.Stroke();
                painter.BeginPath(); painter.MoveTo(new Vector2(r.width - 1, y)); painter.LineTo(new Vector2(r.width - 1, Mathf.Min(y + 6, r.height - 4))); painter.Stroke();
            }
        }
        private void TapSeat(int seat)
        {
            if (suppressClick) return;
            var model = launcher.PrivateLobby;
            if (model == null) return;
            if (moving && model.IsHost(localId))
            {
                if (selected < 0) selected = seat;
                else { int first = selected; selected = -1; Swap(first, seat); }
                RefreshSelection(); return;
            }
            if (model.Player(seat) != null) openPlayer?.Invoke(model.Player(seat));
        }
        private void Swap(int a, int b) => launcher.PrivateAction(2, a, b);
        private void InvitePlayers()
        {
            if (string.IsNullOrEmpty(launcher.JoinCode)) { toast?.Show("The room is still being created"); return; }
            invite?.Invoke(launcher.JoinCode);
        }
        private void RefreshSelection()
        {
            for (int i = 0; i < seats.Length; i++) seats[i]?.EnableInClassList("private-seat--selected", selected == i);
            move.tooltip = moving ? "Tap two seats to swap. Dragging also swaps seats." : "Move seats: tap two seats or drag a player";
            move.EnableInClassList("private-move--active", moving);
        }
        private void ShowMenu(VisualElement card, int index) => menu.Show(card, card.worldBound.center, MenuRows(index));
        private IList<LobbyMenuItem> MenuRows(int seat)
        {
            var model = launcher.PrivateLobby;
            var rows = new List<LobbyMenuItem>();
            if (model == null || model.Player(seat) == null) return rows;
            rows.Add(new LobbyMenuItem("View player", () => openPlayer?.Invoke(model.Player(seat))));
            if (model.IsHost(localId))
            {
                if (!model.IsHost(model.Player(seat))) rows.Add(new LobbyMenuItem("Make host", () => launcher.PrivateAction(3, seat)));
                rows.Add(new LobbyMenuItem("Move", () => { moving = true; selected = seat; RefreshSelection(); }));
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
            panel.RemoveFromHierarchy(); confirm.RemoveFromHierarchy();
        }
    }
}
