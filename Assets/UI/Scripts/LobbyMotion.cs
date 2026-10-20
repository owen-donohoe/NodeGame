using System;

namespace NodeWar.Lobby
{
    /// <summary>The lobby's motion contract, shared by the stage and UI.</summary>
    public static class LobbyMotion
    {
        public const float PageSeconds = 0.280f;
        public const float LongSeconds = 0.420f;
        public const float OpenSeconds = 0.220f;
        public const float CloseSeconds = 0.160f;
        public const float PageTravel = 24f;
        public const float ClosedScale = 0.96f;

        public static float OutCubic(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }
        public static float InCubic(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * t;
        }
        public static int PillIndex(LobbyPageID page)
        {
            switch (page)
            {
                case LobbyPageID.Shop: return 0;
                case LobbyPageID.Workshop: return 1;
                case LobbyPageID.Home: return 2;
                case LobbyPageID.Social: return 3;
                case LobbyPageID.Guild: return 4;
                default: throw new ArgumentOutOfRangeException(nameof(page));
            }
        }
        // Moving right through the pills moves the content left.
        public static int ContentDirection(LobbyPageID from, LobbyPageID to) =>
            Math.Sign(PillIndex(from) - PillIndex(to));
    }
}
