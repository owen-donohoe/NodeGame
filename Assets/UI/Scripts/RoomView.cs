using System;
using System.Collections.Generic;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;
namespace NodeWar.Lobby
{
    /// <summary>Shared private/LAN board, seats, colours, host drag and player bubbles.</summary>
    public sealed class RoomView : IDisposable
    {
        private readonly VisualElement arena = new VisualElement();
        private readonly VisualElement[] seats = new VisualElement[4];
        private readonly LobbyContextMenu menu;
        private readonly Action invite;
        private readonly Action<int, int> swap;
        private readonly Func<int, IList<LobbyMenuItem>> menuRows;
        private readonly Func<string> identity;
        private readonly string emptyLabel;
        private string localId => identity();
        private PrivateLobbyModel rendered;
        private int lastRevision = -1;
        private int dragSeat = -1, dragPointer = -1;
        private Vector2 dragOrigin, dragDelta;
        private VisualElement dragCard, dragOwner, returningCard;
        private IVisualElementScheduledItem returnTick;
        private bool dragged, suppressClick;

        public VisualElement Root => arena;
        public RoomView(LobbyContextMenu menu, Func<string> identity, Action<int, int> swap,
            Func<int, IList<LobbyMenuItem>> menuRows, Action invite = null, string emptyLabel = "Empty seat")
        {
            this.menu = menu; this.identity = identity; this.swap = swap; this.menuRows = menuRows;
            this.invite = invite; this.emptyLabel = emptyLabel;
            arena.AddToClassList("private-arena");
        }
        public void Clear() { ResetDrag(); rendered = null; lastRevision = -1; arena.Clear(); }
        public void Render(PrivateLobbyModel model)
        {
            if (model == null) { Clear(); return; }
            if (rendered != null && rendered.Mode == model.Mode && lastRevision == model.Revision)
            {
                rendered = model;
                for (int i = 0; i < seats.Length; i++) seats[i]?.Q(className: "private-more")?.SetEnabled(!model.Starting);
                if (model.Starting) ResetDrag();
                return;
            }
            ResetDrag(); rendered = model; lastRevision = model.Revision;
            arena.Clear(); Array.Clear(seats, 0, seats.Length);
            arena.EnableInClassList("private-arena--ffa", model.Mode == LobbyMode.FreeForAll4);
            var board = new VisualElement(); board.AddToClassList("private-board");
            board.Add(Text("Board", "private-board-label")); arena.Add(board);
            for (int i = 0; i < model.Capacity; i++) BuildSeat(i, model);
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
                var empty = Button(emptyLabel, () => invite?.Invoke());
                empty.SetEnabled(invite != null);
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
            var model = rendered;
            if (model == null) return;
            if (model.Player(seat) != null) ShowMenu(seats[seat], seat);
        }
        // Read live: seat handlers capture the model they were built from, which a newer snapshot may replace.
        private bool Frozen => rendered != null && rendered.Starting;
        private void Swap(int a, int b) => swap?.Invoke(a, b);
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
            returnTick = arena.schedule.Execute(() =>
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
        public void ResetDrag()
        {
            returnTick?.Pause();
            if (returningCard != null) RestoreSeat(returningCard);
            returningCard = null; EndDrag(false);
        }
        private void ShowMenu(VisualElement card, int index) => menu.ShowBubble(card, menuRows(index));

        public void Dispose() { ResetDrag(); arena.RemoveFromHierarchy(); }
    }
}
