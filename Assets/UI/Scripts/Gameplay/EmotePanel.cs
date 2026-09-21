using System;
using NodeWar.Core;
using NodeWar.Lobby;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.UI
{
    /// <summary>Cosmetic match messages. No command or simulation state crosses this surface.</summary>
    public class EmotePanel
    {
        private readonly EmoteRateLimiter sendLimiter = new EmoteRateLimiter();
        private readonly EmoteRateLimiter[] receiveLimiters = { new EmoteRateLimiter(), new EmoteRateLimiter() };
        private readonly bool[] muted = new bool[2];
        private readonly Bubble[] bubbles = new Bubble[2];
        private readonly Button[] options = new Button[4];
        private IEmoteChannel channel;
        private Func<int> localPlayer;
        private VisualElement root, layer, scrim, sheet, yours, theirs, opponentAnchor;
        private Button button;
        private Label feedback;
        private IVisualElementScheduledItem refreshJob, sheetJob, feedbackJob;
        private bool open;
        private bool opponentEmotes = true;
        private bool calm;

        private class Bubble
        {
            public VisualElement element;
            public IVisualElementScheduledItem pop, hide, remove;
            public bool visible;

            public void Cancel()
            {
                pop?.Pause();
                hide?.Pause();
                remove?.Pause();
                element.RemoveFromHierarchy();
            }
        }

        public event Action Opening;
        public double NextAllowedTime => sendLimiter.NextAllowedTime(Time.unscaledTimeAsDouble);

        public void Bind(IEmoteChannel value, Func<int> player)
        {
            if (channel != null) channel.EmoteReceived -= Receive;
            channel = value;
            localPlayer = player;
            if (root != null && channel != null) channel.EmoteReceived += Receive;
            Refresh();
        }

        public void Attach(VisualElement hudRoot)
        {
            Detach();
            root = hudRoot;
            layer = root.Q<VisualElement>("hud-emotes");
            scrim = root.Q<VisualElement>("hud-emote-scrim");
            sheet = root.Q<VisualElement>("hud-emote-sheet");
            yours = root.Q<VisualElement>("hud-emote-you");
            theirs = root.Q<VisualElement>("hud-emote-them");
            opponentAnchor = root.Q<VisualElement>(className: "hud__board-space");
            button = root.Q<Button>("hud-emote-button");
            if (layer == null || button == null) return;

            // BuildNodeSheet appends to the HUD too. Emotes must also clear it
            // when the end card is up, so authored sibling order is not enough.
            layer.BringToFront();
            layer.EnableInClassList("hud__emotes--calm", calm);
            button.clicked += Toggle;
            scrim.RegisterCallback<PointerDownEvent>(OnScrimDown);
            BindOption(0, "happy", EmoteType.Happy);
            BindOption(1, "sad", EmoteType.Sad);
            BindOption(2, "angry", EmoteType.Angry);
            BindOption(3, "flag", EmoteType.WhiteFlag);

            feedback = new Label("Opponent muted") { pickingMode = PickingMode.Ignore };
            feedback.AddToClassList("hud__emote-feedback");
            theirs.Add(feedback);
            if (channel != null) channel.EmoteReceived += Receive;
            refreshJob = root.schedule.Execute(Refresh).Every(50);
            Refresh();
        }

        public void Detach()
        {
            if (channel != null) channel.EmoteReceived -= Receive;
            refreshJob?.Pause();
            sheetJob?.Pause();
            feedbackJob?.Pause();
            for (int i = 0; i < bubbles.Length; i++) ClearBubble(i);
            if (button != null) button.clicked -= Toggle;
            if (scrim != null) scrim.UnregisterCallback<PointerDownEvent>(OnScrimDown);
            feedback?.RemoveFromHierarchy();
            open = false;
            root = null;
            layer = null;
            button = null;
        }

        /// <summary>
        /// The node sheet is up. This layer draws above everything so a bubble
        /// can clear the end card, which also puts the dock over the sheet's
        /// bottom-left corner, where it would take taps meant for the sheet. So
        /// the dock steps aside, and an open emote sheet closes, until it goes.
        /// </summary>
        public void SetNodeSheetOpen(bool sheetOpen)
        {
            if (layer == null) return;
            if (sheetOpen && open) Close();
            layer.EnableInClassList("hud__emotes--sheet", sheetOpen);
        }

        public void ApplySettings(GameSettingsData settings)
        {
            opponentEmotes = settings.opponentEmotes;
            calm = settings.reducedMotion;
            layer?.EnableInClassList("hud__emotes--calm", calm);
            Refresh();
        }

        private void BindOption(int index, string name, EmoteType emote)
        {
            options[index] = root.Q<Button>("hud-emote-" + name);
            options[index].clicked += () => Send(emote);
        }

        private void Toggle()
        {
            if (open) { Close(); return; }
            Opening?.Invoke();
            open = true;
            sheetJob?.Pause();
            scrim.pickingMode = PickingMode.Position;
            sheet.pickingMode = PickingMode.Position;
            scrim.AddToClassList("hud__settings-scrim--on");
            sheet.AddToClassList("hud__settings--shown");
            foreach (Button option in options) option.pickingMode = PickingMode.Position;
            sheetJob = root.schedule.Execute(() => sheet.AddToClassList("hud__settings--on")).StartingIn(16);
        }

        private void Close()
        {
            open = false;
            sheetJob?.Pause();
            scrim.pickingMode = PickingMode.Ignore;
            sheet.pickingMode = PickingMode.Ignore;
            scrim.RemoveFromClassList("hud__settings-scrim--on");
            sheet.RemoveFromClassList("hud__settings--on");
            foreach (Button option in options) option.pickingMode = PickingMode.Ignore;
            sheetJob = root.schedule.Execute(() => sheet.RemoveFromClassList("hud__settings--shown")).StartingIn(160);
        }

        private void OnScrimDown(PointerDownEvent evt)
        {
            Close();
            evt.StopPropagation();
        }

        private void Send(EmoteType emote)
        {
            int player = localPlayer != null ? localPlayer() : -1;
            if (channel == null || player < 0 || player > 1) return;
            if (!sendLimiter.TryAccept(Time.unscaledTimeAsDouble)) { Refresh(); return; }
            channel.Send(emote);
            Show(player, emote);
            Close();
            Refresh();
        }

        private void Receive(int player, EmoteType emote)
        {
            if (root == null || localPlayer == null || player < 0 || player > 1 ||
                player == localPlayer() || (byte)emote > (byte)EmoteType.WhiteFlag) return;
            if (!receiveLimiters[player].TryAccept(Time.unscaledTimeAsDouble)) return;
            if (!opponentEmotes || muted[player]) return;
            Show(player, emote);
        }

        private void Show(int player, EmoteType emote)
        {
            ClearBubble(player);
            bool own = player == localPlayer();
            var bubble = new Bubble { element = new VisualElement { pickingMode = PickingMode.Ignore } };
            bubbles[player] = bubble;
            bubble.element.AddToClassList("hud__emote-bubble");
            var icon = new LobbyIcon(IconFor(emote));
            icon.AddToClassList("hud__emote-icon");
            icon.EnableInClassList("hud__emote-icon--flag", emote == EmoteType.WhiteFlag);
            bubble.element.Add(icon);
            (own ? yours : theirs).Add(bubble.element);
            bubble.element.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (player == localPlayer() || !bubble.visible) return;
                muted[player] = true;
                ClearBubble(player);
                feedback.AddToClassList("hud__emote-feedback--on");
                feedbackJob?.Pause();
                feedbackJob = root.schedule.Execute(() =>
                    feedback.RemoveFromClassList("hud__emote-feedback--on")).StartingIn(1500);
                evt.StopPropagation();
            });

            // Timers belong to the persistent root. A replaced bubble is
            // detached; its own scheduler would pause and later replay work.
            bubble.pop = root.schedule.Execute(() =>
            {
                bubble.visible = true;
                bubble.element.AddToClassList("hud__emote-bubble--in");
                Refresh();
            }).StartingIn(16);
            bubble.hide = root.schedule.Execute(() =>
            {
                bubble.visible = false;
                bubble.element.pickingMode = PickingMode.Ignore;
                bubble.element.RemoveFromClassList("hud__emote-bubble--in");
            }).StartingIn(2516);
            bubble.remove = root.schedule.Execute(() => ClearBubble(player)).StartingIn(2736);
        }

        private void ClearBubble(int player)
        {
            bubbles[player]?.Cancel();
            bubbles[player] = null;
        }

        private void Refresh()
        {
            if (layer == null || button == null) return;
            int player = localPlayer != null ? localPlayer() : -1;
            bool ready = channel != null && player >= 0 && player <= 1 &&
                NextAllowedTime <= Time.unscaledTimeAsDouble;
            button.SetEnabled(ready);
            button.EnableInClassList("hud__emote--cooldown", !ready);
            foreach (Button option in options)
            {
                option.SetEnabled(ready);
                option.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
            }

            // The top-right corner of the board space: under the opponent's side
            // of the HUD, but below every readout. Directly under their wall it
            // covered the metal count, and the resources are never to be hidden.
            // Measured, so it follows the safe area and the units card opening.
            Vector2 anchor = layer.WorldToLocal(new Vector2(opponentAnchor.worldBound.xMax,
                opponentAnchor.worldBound.yMin));
            if (!float.IsNaN(anchor.x) && !float.IsNaN(anchor.y))
            {
                theirs.style.left = anchor.x - 46f - 12f;
                theirs.style.top = anchor.y + 8f;
            }
            for (int i = 0; i < bubbles.Length; i++)
            {
                Bubble bubble = bubbles[i];
                if (bubble == null) continue;
                bool own = i == player;
                if (!own && (!opponentEmotes || muted[i])) { ClearBubble(i); continue; }
                VisualElement host = own ? yours : theirs;
                if (bubble.element.parent != host) host.Add(bubble.element);
                bubble.element.pickingMode = !own && bubble.visible ? PickingMode.Position : PickingMode.Ignore;
            }
        }

        private static LobbyIconKind IconFor(EmoteType emote)
        {
            switch (emote)
            {
                case EmoteType.Sad: return LobbyIconKind.Frown;
                case EmoteType.Angry: return LobbyIconKind.Angry;
                case EmoteType.WhiteFlag: return LobbyIconKind.Flag;
                default: return LobbyIconKind.Smile;
            }
        }
    }
}
