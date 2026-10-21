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
        private readonly Button start, copy;
        private readonly VisualElement[] seats = new VisualElement[4];
        private readonly MkPanel confirm = new MkPanel { name = "private-kick" };
        private string localId;
        private int dragSeat = -1, dragPointer = -1;
        private Vector2 dragOrigin, dragDelta;
        private VisualElement dragCard, dragOwner, returningCard;
        private IVisualElementScheduledItem returnTick;
        private bool dragged, suppressClick, disposed;
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
            panel.Closed += () => { ResetDrag(); confirm.Close(); launcher.Cancel(); };
            arena.AddToClassList("private-arena"); panel.Body.Add(arena);
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
            ResetDrag(); rendered = null; lastRevision = -1; arena.Clear();
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
            if (starting) { ResetDrag(); confirm.Close(); }
            for (int i = 0; i < seats.Length; i++) seats[i]?.Q(className: "private-more")?.SetEnabled(!starting);
            if (model == null) return;
            if (rendered != null && rendered.Mode == model.Mode && lastRevision == model.Revision) return;
            rendered = model; lastRevision = model.Revision;
            ResetDrag();
            TrackedText.SetText(panel.Title, ModeDefinition.For(model.Mode).DisplayName);
            arena.Clear(); Array.Clear(seats, 0, seats.Length);
            arena.EnableInClassList("private-arena--ffa", model.Mode == LobbyMode.FreeForAll4);
            var board = new VisualElement(); board.AddToClassList("private-board");
            board.Add(Text("Board", "private-board-label")); arena.Add(board);
            for (int i = 0; i < model.Capacity; i++) BuildSeat(i, model);
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
                var empty = Button("+ Invite", InvitePlayers);
                empty.AddToClassList("private-invite-seat"); card.Add(empty);
            }
            else
            {
                string name = model.Name(index) ?? "Player";
                var face = Button("", () => TapSeat(index)); face.Clear(); face.AddToClassList("private-face");
                face.Add(Text(name.Length > 0 ? name.Substring(0, 1).ToUpperInvariant() : "?", "private-avatar"));
                var who = new VisualElement(); who.AddToClassList("private-who");
                // Flexible names fill the available width; they must not get a content width.
                var nameLabel = new Label(HandleDisplay.Constrained(name)) { enableRichText = false, pickingMode = PickingMode.Ignore };
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
                face.RegisterCallback<PointerDownEvent>(evt =>
                {
                    suppressClick = false;
                    if (evt.button != 0 || !evt.isPrimary || Frozen || !model.IsHost(localId)) return;
                    ResetDrag();
                    dragSeat = index; dragPointer = evt.pointerId; dragCard = card; dragOwner = face;
                    dragOrigin = arena.WorldToLocal(evt.position); dragDelta = Vector2.zero;
                }, TrickleDown.TrickleDown);
                face.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (dragSeat != index || evt.pointerId != dragPointer) return;
                    if (Frozen || !model.IsHost(localId)) { ResetDrag(); return; }
                    dragDelta = arena.WorldToLocal(evt.position) - dragOrigin;
                    if (!dragged && dragDelta.sqrMagnitude < 64) return;
                    if (!dragged)
                    {
                        dragged = true; suppressClick = true;
                        LobbyMouseDrag.Capture(face, dragPointer);
                        card.BringToFront(); card.AddToClassList("private-seat--dragging");
                        card.style.scale = new Scale(Vector3.one * 1.04f);
                    }
                    card.style.translate = new Translate(dragDelta.x, dragDelta.y);
                    evt.StopImmediatePropagation();
                }, TrickleDown.TrickleDown);
                face.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (dragSeat != index || evt.pointerId != dragPointer) return;
                    bool wasDragged = dragged;
                    int target = -1;
                    for (int j = 0; j < model.Capacity; j++)
                        if (j != index && seats[j].worldBound.Contains(evt.position)) { target = j; break; }
                    EndDrag(wasDragged);
                    if (!wasDragged) return;
                    if (target >= 0 && !Frozen && model.IsHost(localId)) Swap(index, target);
                    evt.StopImmediatePropagation();
                }, TrickleDown.TrickleDown);
                face.RegisterCallback<PointerCancelEvent>(evt => { if (evt.pointerId == dragPointer) EndDrag(true); });
                face.RegisterCallback<PointerCaptureOutEvent>(evt => { if (evt.pointerId == dragPointer) EndDrag(true); });
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
            if (model.Player(seat) != null) openPlayer?.Invoke(model.Player(seat));
        }
        // Read live: seat handlers capture the model they were built from, which a newer snapshot may replace.
        private bool Frozen => launcher.PrivateLobby != null && launcher.PrivateLobby.Starting;
        private void Swap(int a, int b) => launcher.PrivateAction(2, a, b);
        private void InvitePlayers()
        {
            if (string.IsNullOrEmpty(launcher.JoinCode)) { toast?.Show("The room is still being created"); return; }
            invite?.Invoke(launcher.JoinCode);
        }
        private void EndDrag(bool springBack)
        {
            var card = dragCard; var owner = dragOwner; int pointer = dragPointer;
            bool lifted = dragged;
            dragSeat = dragPointer = -1; dragCard = dragOwner = null; dragged = false;
            // Clear identity before release: capture-out must not end the gesture twice.
            if (pointer >= 0 && owner != null && owner.HasPointerCapture(pointer)) owner.ReleasePointer(pointer);
            if (card == null) return;
            card.RemoveFromClassList("private-seat--dragging");
            if (!springBack || !lifted) { RestoreSeat(card); return; }
            returningCard = card;
            var x = new LobbyMotion.Spring(dragDelta.x) { Target = 0 };
            var y = new LobbyMotion.Spring(dragDelta.y) { Target = 0 };
            var scale = new LobbyMotion.Spring(1.04f) { Target = 1 };
            double sampledAt = Time.unscaledTimeAsDouble;
            returnTick = panel.schedule.Execute(() =>
            {
                double now = Time.unscaledTimeAsDouble; float dt = (float)(now - sampledAt); sampledAt = now;
                x.Step(dt, LobbyMotion.PopDamping); y.Step(dt, LobbyMotion.PopDamping); scale.Step(dt, LobbyMotion.PopDamping);
                card.style.translate = new Translate(x.Position, y.Position);
                card.style.scale = new Scale(Vector3.one * scale.Position);
                if (x.Settled && y.Settled && scale.Settled)
                { RestoreSeat(card); returnTick.Pause(); returningCard = null; }
            }).Every(16);
        }
        private static void RestoreSeat(VisualElement card)
        {
            card.style.translate = new Translate(0, 0); card.style.scale = new Scale(Vector3.one);
        }
        private void ResetDrag()
        {
            returnTick?.Pause();
            if (returningCard != null) RestoreSeat(returningCard);
            returningCard = null; EndDrag(false);
        }
        private void ShowMenu(VisualElement card, int index) => menu.Show(card, card.worldBound.center, MenuRows(index));
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
            panel.RemoveFromHierarchy(); confirm.RemoveFromHierarchy();
        }
    }
}
