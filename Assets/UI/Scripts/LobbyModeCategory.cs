using System;

namespace NodeWar.Lobby
{
    public enum LobbyModeCategory { Ranked, Unranked, Other }

    /// <summary>Which pager category of the game mode page holds each mode, taken from how the mode plays.</summary>
    public static class LobbyModeCategories
    {
        public static readonly LobbyModeCategory[] All = { LobbyModeCategory.Ranked, LobbyModeCategory.Unranked, LobbyModeCategory.Other };

        public static string Title(LobbyModeCategory category) => category.ToString();

        /// <summary>Ranked play is the ranked 1v1 queue; team and free-for-all queues are unranked; bot, LAN and private matches are other.</summary>
        public static LobbyModeCategory Of(LobbyModeSelection selection)
        {
            switch (selection.PlayAction)
            {
                case LobbyPlayAction.Ranked: return LobbyModeCategory.Ranked;
                case LobbyPlayAction.TeamChoice:
                case LobbyPlayAction.FreeForAllChoice: return LobbyModeCategory.Unranked;
                default: return LobbyModeCategory.Other;
            }
        }

        /// <summary>The mode cards a category shows, in display order. Private is a toggle on the 1v1 card, not a card.</summary>
        public static LobbyMode[] ModesIn(LobbyModeCategory category)
        {
            switch (category)
            {
                case LobbyModeCategory.Ranked: return new[] { LobbyMode.Ranked1v1 };
                case LobbyModeCategory.Unranked: return new[] { LobbyMode.Team2v2, LobbyMode.FreeForAll4 };
                case LobbyModeCategory.Other: return new[] { LobbyMode.Bot, LobbyMode.Lan };
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        public static int IndexOf(LobbyModeCategory category) => Array.IndexOf(All, category);
    }
}
