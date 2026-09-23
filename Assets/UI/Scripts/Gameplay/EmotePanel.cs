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
        private bool matchMuted;
        private readonly Bubble[] bubbles = new Bubble[2];
        private readonly Button[] options = new Button[4];
        private IEmoteChannel channel;
        private Func<int> localPlayer;
        private VisualElement root, layer, scrim, sheet, yours, theirs, opponentAnchor;
        private Button button, muteButton;
        private Label muteLabel;
        private IVisualElementScheduledItem refreshJob, sheetJob;
        private bool open;
        private bool opponentEmotes = true;
        private bool calm;

        /// <summary>Gap between the emote tab and the options sheet above it.</summary>
        private const float SheetGap = 8f;

        private class Bubble
        {
            public VisualElement element;
            public IVisualElementScheduledItem pop, hide, remove;

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
        private bool IsMuted => !opponentEmotes || matchMuted;

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
            muteButton = root.Q<Button>("hud-emote-mute");
            muteLabel = root.Q<Label>("hud-emote-mute-label");
            if (layer == null || button == null) return;

            // BuildNodeSheet appends to the HUD too. Emotes must also clear it
            // when the end card is up, so authored sibling order is not enough.
            layer.BringToFront();
            layer.EnableInClassList("hud__emotes--calm", calm);
            button.clicked += Toggle;
            muteButton.clicked += ToggleMute;
            scrim.RegisterCallback<PointerDownEvent>(OnScrimDown);
            BindOption(0, "happy", EmoteType.Happy);
            BindOption(1, "sad", EmoteType.Sad);
            BindOption(2, "angry", EmoteType.Angry);
            BindOption(3, "flag", EmoteType.WhiteFlag);

            if (channel != null) channel.EmoteReceived += Receive;
            refreshJob = root.schedule.Execute(Refresh).Every(50);
            Refresh();
        }

        public void Detach()
        {
            if (channel != null) channel.EmoteReceived -= Receive;
            refreshJob?.Pause();
            sheetJob?.Pause();
            for (int i = 0; i < bubbles.Length; i++) ClearBubble(i);
            if (button != null) button.clicked -= Toggle;
            if (muteButton != null) muteButton.clicked -= ToggleMute;
            if (scrim != null) scrim.UnregisterCallback<PointerDownEvent>(OnScrimDown);
            open = false;
            root = null;
            layer = null;
            button = null;
            muteButton = null;
        }

        /// <summary>
        /// The node sheet is up. The tab the emote button lives in moved onto
        /// the resource sheet, so the node sheet covers it along with the rest
        /// of that sheet - there is nothing left to step aside. What remains is
        /// the options sheet, which draws in this layer above everything so a
        /// bubble can clear the end card, and would otherwise stand over the
        /// node sheet. It closes instead.
        /// </summary>
        public void SetNodeSheetOpen(bool sheetOpen)
        {
            if (layer == null) return;
            if (sheetOpen && open) Close();
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

        /// <summary>
        /// Puts the options sheet directly above the emote tab, in this
        /// layer's coordinates. The tab is a child of the resource sheet and
        /// so sits inside the safe area; this layer spans the raw panel. On a
        /// device with a bottom inset those two differ, and a USS constant
        /// would part the sheet from the tab it is supposed to rise out of.
        ///
        /// Same world-to-panel move DraftScreenController.PositionConfirm
        /// makes, and for the same reason: one element has to follow another
        /// that no selector can reach.
        /// </summary>
        private void PositionSheet()
        {
            if (sheet == null || button == null || layer == null) return;

            VisualElement tab = button.parent;
            if (tab == null) return;

            Rect tabBound = tab.worldBound;
            Rect layerBound = layer.worldBound;
            if (float.IsNaN(tabBound.yMin) || layerBound.height <= 0f) return;

            sheet.style.left = tabBound.xMin - layerBound.xMin;
            sheet.style.bottom = layerBound.yMax - tabBound.yMin + SheetGap;
        }

        private void Toggle()
        {
            if (open) { Close(); return; }
            Opening?.Invoke();
            PositionSheet();
            open = true;
            sheetJob?.Pause();
            scrim.pickingMode = PickingMode.Position;
            sheet.pickingMode = PickingMode.Position;
            scrim.AddToClassList("hud__settings-scrim--on");
            sheet.AddToClassList("hud__settings--shown");
            foreach (Button option in options) option.pickingMode = PickingMode.Position;
            muteButton.pickingMode = PickingMode.Position;
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
            muteButton.pickingMode = PickingMode.Ignore;
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
            if (IsMuted || channel == null || player < 0 || player > 1) return;
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
            if (IsMuted) return;
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

            // Timers belong to the persistent root. A replaced bubble is
            // detached; its own scheduler would pause and later replay work.
            bubble.pop = root.schedule.Execute(() =>
            {
                bubble.element.AddToClassList("hud__emote-bubble--in");
                Refresh();
            }).StartingIn(16);
            bubble.hide = root.schedule.Execute(() =>
            {
                bubble.element.RemoveFromClassList("hud__emote-bubble--in");
            }).StartingIn(2516);
            bubble.remove = root.schedule.Execute(() => ClearBubble(player)).StartingIn(2736);
        }

        private void ClearBubble(int player)
        {
            bubbles[player]?.Cancel();
            bubbles[player] = null;
        }

        private void ToggleMute()
        {
            // A saved mute can only be lifted in Settings. This toggle never
            // writes to the profile and survives only this match's HUD rebuilds.
            if (!opponentEmotes) return;
            matchMuted = !matchMuted;
            Refresh();
        }

        private void Refresh()
        {
            if (layer == null || button == null) return;
            int player = localPlayer != null ? localPlayer() : -1;
            bool ready = !IsMuted && channel != null && player >= 0 && player <= 1 &&
                NextAllowedTime <= Time.unscaledTimeAsDouble;
            // Keep the popup reachable during mute and cooldown so the player
            // can always inspect or change the match mute.
            layer.EnableInClassList("hud__emotes--muted", IsMuted);
            button.tooltip = IsMuted ? "Emotes muted" : "Emotes";
            muteButton.SetEnabled(opponentEmotes);
            muteButton.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
            muteLabel.text = !opponentEmotes ? "Muted in Settings" :
                matchMuted ? "Unmute this match" : "Mute this match";
            foreach (Button option in options)
            {
                option.SetEnabled(ready);
                option.EnableInClassList("hud__emote-option--disabled", !ready);
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
                if (IsMuted) { ClearBubble(i); continue; }
                VisualElement host = own ? yours : theirs;
                if (bubble.element.parent != host) host.Add(bubble.element);
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
