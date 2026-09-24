using UnityEngine;

namespace NodeWar.View
{
    /// <summary>
    /// The two player colours for world-space presentation: draft pieces and
    /// the ownership tint on node outlines.
    ///
    /// The same values as --pal-player-0 and --pal-player-1 in
    /// Assets/UI/Styles/Tokens.uss, so a piece on the board and its owner's mark
    /// on the HUD read as one colour. Change them together.
    /// </summary>
    public static class PlayerColors
    {
        public static readonly Color Player0 = new Color(0.40f, 0.60f, 1.00f);
        public static readonly Color Player1 = new Color(1.00f, 0.40f, 0.40f);

        /// <summary>Nobody's. The grey an unowned node's outline starts from.</summary>
        public static readonly Color Neutral = new Color(0.62f, 0.62f, 0.66f);

        public static Color For(int playerID)
        {
            if (playerID == 0) return Player0;
            if (playerID == 1) return Player1;
            return Neutral;
        }
    }
}
