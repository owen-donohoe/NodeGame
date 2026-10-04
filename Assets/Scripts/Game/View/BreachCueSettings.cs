using UnityEngine;

namespace NodeWar.View
{
    /// <summary>
    /// Shared look and player preferences for the breach cues: the core bar and
    /// the breacher highlight. One instance, handed by reference to every view
    /// that draws them, so the settings card can change a flag and the next
    /// frame sees it with nothing to notify -- the OpponentRouteSettings shape.
    ///
    /// View configuration only. Nothing here reaches SimulationState.
    ///
    /// The breacher colour is a hot magenta, chosen to be none of: player 0 blue
    /// (0.4, 0.7, 1), player 1 red (1, 0.4, 0.5), the selection / lasso gold
    /// (1, 0.78, 0.25), or the violet (0.6, 0.3, 1) VillagerView already uses to
    /// tint fighters and breaching attackers. It sits between that violet and
    /// player 1's pink, so it leans on brightness, a pulse and the ring rather
    /// than hue alone.
    /// </summary>
    public class BreachCueSettings
    {
        /// <summary>Set once by GameManager from the balance data. Zero means no breach bar this match.</summary>
        public int breachBarMax;

        /// <summary>Written by the HUD from the player's settings. Defaults match GameSettingsData.</summary>
        public bool colourblindMarks = true;
        public bool reducedMotion;

        public Color breacherColor = new Color(1f, 0.1f, 0.85f, 1f);

        public Color p0PipColor = new Color(0.4f, 0.7f, 1f, 1f);
        public Color p1PipColor = new Color(1f, 0.4f, 0.5f, 1f);

        [Header("Highlight ring")]
        public float ringRadius = 0.34f;
        public float ringWidth = 0.07f;

        [Header("Core bar")]
        public float barPulseHz = 3f;
    }
}
