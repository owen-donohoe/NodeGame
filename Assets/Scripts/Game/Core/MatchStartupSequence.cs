using System;

namespace NodeWar.Core
{
    /// <summary>Startup decisions shared by the draft and testing entry paths.</summary>
    public sealed class MatchStartupSequence
    {
        public enum MatchPhase { PreDraft, Drafting, PostDraft, Countdown, Playing }

        private readonly Action unpause;
        private bool driverUnpaused;

        public MatchPhase Phase { get; private set; } = MatchPhase.PreDraft;

        public MatchStartupSequence(Action unpause)
        {
            this.unpause = unpause ?? throw new ArgumentNullException(nameof(unpause));
        }

        public void StartDraft() => Phase = MatchPhase.Drafting;

        public void CompleteDraft() => Phase = MatchPhase.PostDraft;

        /// <summary>The presentation either reports completion later or falls back immediately.</summary>
        public void BeginPostDraftTransition(bool waitsForCompletion, Action startPresentation)
        {
            Phase = MatchPhase.Countdown;
            startPresentation();
            if (!waitsForCompletion) CompleteTransition();
        }

        /// <summary>
        /// Testing mode is Playing during initialization, as in GameManager's original
        /// path, but its driver stays paused until the board and startup wave are ready.
        /// The wave is animation only; no completion event is expected.
        /// </summary>
        public void SkipDraftAndInitialize(Action initializeAndPlayWave)
        {
            Phase = MatchPhase.Playing;
            initializeAndPlayWave();
            CompleteTransition();
        }

        public void CompleteTransition()
        {
            Phase = MatchPhase.Playing;
            if (driverUnpaused) return;
            driverUnpaused = true;
            unpause();
        }
    }
}
