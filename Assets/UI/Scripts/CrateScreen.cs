using System;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    public sealed class CrateScreen
    {
        private const string RouteID = "crate-opening";
        private readonly LobbyOverlayRouter router;
        private readonly VisualElement content;
        private readonly VisualElement crate;
        private readonly VisualElement reward;
        private readonly Label rewardLabel;
        private readonly Label prompt;
        private readonly VisualElement summary;
        private readonly Button accept;
        private readonly IVisualElementScheduledItem animation;
        private CrateOpeningSequence sequence;
        private CrateOpeningState renderedState;
        private int renderedReward = -1;
        private double lastTime;
        public VisualElement Root { get; }
        public bool IsOpen { get; private set; }

        public CrateScreen(LobbyOverlayRouter router)
        {
            this.router = router ?? throw new ArgumentNullException(nameof(router));
            Root = new PaperCard { name = "crate-screen", RotatePaper = false };
            Resources.Load<VisualTreeAsset>("Lobby/CrateScreen").CloneTree(Root);
            Root.AddToClassList("lb-crate-screen");
            Root.AddToClassList("lb-no-swipe");
            content = Root.Q("crate-content");
            crate = Root.Q("crate-box");
            reward = Root.Q("crate-reward");
            rewardLabel = Root.Q<Label>("crate-reward-label");
            prompt = Root.Q<Label>("crate-prompt");
            summary = Root.Q("crate-summary");
            accept = Root.Q<Button>("crate-accept");
            accept.clicked += Accept;
            Root.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button != 0 || !IsOpen) return;
                Tap();
                evt.StopPropagation();
            });
            Root.RegisterCallback<DetachFromPanelEvent>(evt => { if (IsOpen) Close(); });
            animation = Root.schedule.Execute(Update).Every(16);
            animation.Pause();
            Root.style.display = DisplayStyle.None;
        }

        public void Open(CrateSize size, ICrateInventory inventory)
        {
            if (IsOpen || inventory == null || !inventory.TryOpen(size, out var rewards)) return;
            sequence = new CrateOpeningSequence(rewards);
            summary.Clear();
            for (int i = 0; i < sequence.RewardCount; i++)
                summary.Add(new Label(Describe(sequence.RewardAt(i))) { enableRichText = false });
            if (sequence.RewardCount == 0) summary.Add(new Label("No rewards"));
            crate.Q<Label>().text = size + " crate";
            renderedState = (CrateOpeningState)(-1);
            renderedReward = -1;
            IsOpen = true;
            Root.style.display = DisplayStyle.Flex;
            Root.BringToFront();
            router.Open(new LobbyOverlay(RouteID, OverlayKind.Fullscreen, true, true, HandleBack), Close);
            lastTime = Time.unscaledTimeAsDouble;
            Render();
            animation.Resume();
        }

        private bool HandleBack()
        {
            if (sequence.CanAccept) Accept();
            else Tap();
            return true;
        }

        private void Tap()
        {
            if (!IsOpen) return;
            sequence.Tap();
            Render();
        }

        private void Accept()
        {
            if (IsOpen && sequence.Accept()) Close();
        }

        private void Close()
        {
            IsOpen = false;
            animation.Pause();
            Root.style.display = DisplayStyle.None;
            router.Closed(RouteID);
        }

        private void Update()
        {
            if (!IsOpen) return;
            double now = Time.unscaledTimeAsDouble;
            sequence.Advance(now - lastTime);
            lastTime = now;
            Render();
        }

        private void Render()
        {
            var state = sequence.State;
            if (state != renderedState || sequence.RewardIndex != renderedReward)
            {
                renderedState = state;
                renderedReward = sequence.RewardIndex;
                bool showingReward = state == CrateOpeningState.Revealing;
                reward.style.display = showingReward ? DisplayStyle.Flex : DisplayStyle.None;
                summary.style.display = state == CrateOpeningState.Summary ? DisplayStyle.Flex : DisplayStyle.None;
                accept.style.display = state == CrateOpeningState.Summary ? DisplayStyle.Flex : DisplayStyle.None;
                crate.style.display = state == CrateOpeningState.Summary ? DisplayStyle.None : DisplayStyle.Flex;
                prompt.text = state == CrateOpeningState.Ready ? "Tap to open" : showingReward ? "Tap to continue" :
                    state == CrateOpeningState.Summary ? "Your rewards" : "";
                if (showingReward)
                {
                    CrateReward item = sequence.RewardAt(renderedReward);
                    rewardLabel.text = Describe(item);
                    RevealStyle style = CrateOpeningSequence.StyleFor(item.Kind);
                    reward.EnableInClassList("lb-reveal-card", style == RevealStyle.Card);
                    reward.EnableInClassList("lb-reveal-district", style == RevealStyle.District);
                    reward.EnableInClassList("lb-reveal-suit", style == RevealStyle.Suit);
                    reward.EnableInClassList("lb-reveal-skin", style == RevealStyle.Skin);
                }
            }
            float t = (float)sequence.Progress;
            float eased = 1f - (1f - t) * (1f - t);
            crate.style.translate = new Translate(0f, state == CrateOpeningState.Landing ? -280f * (1f - eased) : 0f);
            crate.style.scale = new Scale(Vector3.one * (state == CrateOpeningState.Opening ? 1f + 0.15f * t : 1f));
            float pan = state == CrateOpeningState.Opening ? eased : state == CrateOpeningState.Revealing ? 1f : 0f;
            content.style.translate = new Translate(0f, 70f * pan);
            reward.style.translate = new Translate(0f, -170f * eased);
            reward.style.opacity = eased;
            reward.style.scale = new Scale(Vector3.one * Mathf.Lerp(0.6f, 1f, eased));
        }

        private static string Describe(CrateReward reward)
        {
            return reward.Kind == CrateRewardKind.Coins || reward.Kind == CrateRewardKind.Premium
                ? reward.Amount + " " + (reward.Kind == CrateRewardKind.Premium ? "Laurels" : "Coins")
                : reward.Kind + ": " + reward.Id;
        }
    }
}
