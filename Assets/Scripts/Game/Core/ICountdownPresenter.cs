namespace NodeWar.Core
{
    /// <summary>
    /// Whatever shows the "3 - 2 - 1 - GO" before the tick loop starts.
    ///
    /// MatchTransitionController holds the sequence and needs to wait for the
    /// countdown to finish, but must not care which UI stack drew it. The uGUI
    /// CountdownUI prefab is the default; when the UI Toolkit HUD is on, it
    /// hands itself over instead, so only one countdown ever appears.
    /// </summary>
    public interface ICountdownPresenter
    {
        /// <summary>
        /// Runs the countdown and calls back when it is done. The caller waits
        /// on that callback before unpausing the tick loop, so it must always
        /// arrive - even if the countdown is cut short.
        /// </summary>
        void PlayCountdown(System.Action onComplete);
    }
}
