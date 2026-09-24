using NodeWar.Simulation;
using UnityEngine;

namespace NodeWar.View
{
    /// <summary>
    /// How hard and how long the camera shakes for each moment. Strength is a
    /// multiple of CameraController's shake amplitude; 1 is the biggest the game
    /// asks for.
    /// </summary>
    [System.Serializable]
    public class ScreenShakeSettings
    {
        [Tooltip("Either Core took a breach that did not end the match.")]
        public float coreHitStrength = 0.6f;
        public float coreHitSeconds = 0.25f;

        [Tooltip("You completed a claim on a node.")]
        public float captureStrength = 0.2f;
        public float captureSeconds = 0.15f;

        [Tooltip("You pushed one of your opponent's nodes back to neutral.")]
        public float takeFromOpponentStrength = 0.5f;
        public float takeFromOpponentSeconds = 0.22f;

        [Tooltip("The breach that ended the match.")]
        public float gameOverStrength = 1f;
        public float gameOverSeconds = 0.5f;
    }

    /// <summary>
    /// Decides when the camera shakes, from the moments each tick reports into
    /// its TickEventLog. Read-only against the simulation: it reads the log and
    /// SimulationState.gameOver, and the shake itself is a camera offset that
    /// never reaches either peer's simulation.
    ///
    /// Breaches shake for both players, since either Core being hit matters to
    /// both. Captures shake only for the player who made them. Several moments
    /// in one tick produce one shake, the strongest of them.
    /// </summary>
    public sealed class ScreenShakeDirector : System.IDisposable
    {
        private readonly SimulationState state;
        private readonly NodeWar.Core.ITickProvider tickProvider;
        private readonly ScreenShakeSettings settings;
        private readonly System.Func<int> localPlayer;
        private readonly System.Action<float, float> shake;

        private bool gameOverShaken;

        public ScreenShakeDirector(SimulationState state, NodeWar.Core.ITickProvider tickProvider,
                                   ScreenShakeSettings settings, System.Func<int> localPlayer,
                                   System.Action<float, float> shake)
        {
            this.state = state;
            this.tickProvider = tickProvider;
            this.settings = settings ?? new ScreenShakeSettings();
            this.localPlayer = localPlayer;
            this.shake = shake;

            if (tickProvider != null)
                tickProvider.TickSimulated += OnTickSimulated;
        }

        public void Dispose()
        {
            if (tickProvider != null)
                tickProvider.TickSimulated -= OnTickSimulated;
        }

        private void OnTickSimulated(TickEventLog log)
        {
            if (log == null || shake == null) return;

            int me = localPlayer != null ? localPlayer() : 0;
            float strength = 0f;
            float seconds = 0f;

            for (int i = 0; i < log.Count; i++)
            {
                TickEvent e = log[i];

                switch (e.type)
                {
                    case TickEventType.Breach:
                        // The breach that ends the match is the win-check's, in
                        // the same tick, so gameOver is already set when this runs.
                        if (state != null && state.gameOver)
                        {
                            if (gameOverShaken) break;
                            gameOverShaken = true;
                            Pick(settings.gameOverStrength, settings.gameOverSeconds, ref strength, ref seconds);
                        }
                        else
                        {
                            Pick(settings.coreHitStrength, settings.coreHitSeconds, ref strength, ref seconds);
                        }
                        break;

                    case TickEventType.NodeClaimed:
                        // playerID is the new owner.
                        if (e.playerID == me)
                            Pick(settings.captureStrength, settings.captureSeconds, ref strength, ref seconds);
                        break;

                    case TickEventType.NodeNeutralised:
                        // playerID lost it, value pushed it.
                        if (e.value == me && e.playerID != me)
                            Pick(settings.takeFromOpponentStrength, settings.takeFromOpponentSeconds, ref strength, ref seconds);
                        break;
                }
            }

            if (strength > 0f) shake(strength, seconds);
        }

        private static void Pick(float candidateStrength, float candidateSeconds,
                                 ref float strength, ref float seconds)
        {
            if (candidateStrength <= strength) return;
            strength = candidateStrength;
            seconds = candidateSeconds;
        }
    }
}
