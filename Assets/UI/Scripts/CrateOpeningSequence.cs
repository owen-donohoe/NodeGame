using System;
using System.Collections.Generic;
using NodeWar.Backend;

namespace NodeWar.Lobby
{
    public enum CrateOpeningState { Landing, Ready, Opening, Revealing, Summary, Done }
    public enum RevealStyle { Card, District, Suit, Skin }

    public sealed class CrateOpeningSequence
    {
        public const double LandingSeconds = 0.55;
        public const double OpeningSeconds = 0.45;
        public const double RevealSeconds = 0.6;
        private readonly CrateReward[] rewards;
        private double elapsed;
        public CrateOpeningState State { get; private set; }
        public int RewardIndex { get; private set; } = -1;
        public int RewardCount => rewards.Length;
        public bool CanAccept => State == CrateOpeningState.Summary;
        public double Progress => Math.Min(1, elapsed / (State == CrateOpeningState.Landing ? LandingSeconds :
            State == CrateOpeningState.Opening ? OpeningSeconds : RevealSeconds));

        public CrateOpeningSequence(IReadOnlyList<CrateReward> rewards)
        {
            this.rewards = new CrateReward[rewards == null ? 0 : rewards.Count];
            for (int i = 0; i < this.rewards.Length; i++) this.rewards[i] = rewards[i];
        }

        public CrateReward RewardAt(int index) => rewards[index];

        public static RevealStyle StyleFor(CrateRewardKind kind)
        {
            switch (kind)
            {
                case CrateRewardKind.District: return RevealStyle.District;
                case CrateRewardKind.Suit: return RevealStyle.Suit;
                case CrateRewardKind.Skin: return RevealStyle.Skin;
                default: return RevealStyle.Card;
            }
        }

        public void Advance(double deltaSeconds)
        {
            if (deltaSeconds <= 0 || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds)) return;
            if (State != CrateOpeningState.Landing && State != CrateOpeningState.Opening &&
                State != CrateOpeningState.Revealing) return;
            elapsed += deltaSeconds;
            if (State == CrateOpeningState.Landing && elapsed >= LandingSeconds) Enter(CrateOpeningState.Ready);
            else if (State == CrateOpeningState.Opening && elapsed >= OpeningSeconds)
            {
                RewardIndex = rewards.Length == 0 ? -1 : 0;
                Enter(rewards.Length == 0 ? CrateOpeningState.Summary : CrateOpeningState.Revealing);
            }
            // Time finishes a reveal's animation, never skips an unseen reward.
            else if (State == CrateOpeningState.Revealing) elapsed = Math.Min(elapsed, RevealSeconds);
        }

        public void Tap()
        {
            if (State == CrateOpeningState.Ready) Enter(CrateOpeningState.Opening);
            else if (State == CrateOpeningState.Revealing)
            {
                if (elapsed < RevealSeconds) { elapsed = RevealSeconds; return; }
                if (RewardIndex + 1 < rewards.Length) { RewardIndex++; elapsed = 0; }
                else Enter(CrateOpeningState.Summary);
            }
        }

        public bool Accept()
        {
            if (!CanAccept) return false;
            Enter(CrateOpeningState.Done);
            return true;
        }

        private void Enter(CrateOpeningState state) { State = state; elapsed = 0; }
    }
}
