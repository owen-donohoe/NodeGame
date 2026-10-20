using System;

namespace NodeWar.Lobby
{
    public enum LobbyModeCategory { Ranked, Unranked, Other }

    /// <summary>Stable groups in the horizontally scrolling mode strip.</summary>
    public static class LobbyModeCategories
    {
        public static readonly LobbyModeCategory[] All = { LobbyModeCategory.Ranked, LobbyModeCategory.Unranked, LobbyModeCategory.Other };

        public static string Title(LobbyModeCategory category) => category.ToString();

        /// <summary>Private changes entry routing, not the selected mode's group.</summary>
        public static LobbyModeCategory Of(LobbyModeSelection selection)
        {
            switch (selection.Mode)
            {
                case LobbyMode.Ranked1v1: return LobbyModeCategory.Ranked;
                case LobbyMode.Unranked1v1:
                case LobbyMode.Team2v2:
                case LobbyMode.FreeForAll4: return LobbyModeCategory.Unranked;
                default: return LobbyModeCategory.Other;
            }
        }

        /// <summary>Display order is column first, with two rows per column.</summary>
        public static LobbyMode[] ModesIn(LobbyModeCategory category)
        {
            switch (category)
            {
                case LobbyModeCategory.Ranked: return new[] { LobbyMode.Ranked1v1 };
                case LobbyModeCategory.Unranked: return new[] { LobbyMode.Unranked1v1, LobbyMode.Team2v2, LobbyMode.FreeForAll4 };
                case LobbyModeCategory.Other: return new[] { LobbyMode.Bot, LobbyMode.Lan };
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        public static int IndexOf(LobbyModeCategory category) => Array.IndexOf(All, category);
    }

    /// <summary>Unity-free column/slot geometry shared by the strip and tests.</summary>
    public static class LobbyModeLayout
    {
        public const int Rows = 2;
        public static int Columns(int modes) => Math.Max(1, (modes + Rows - 1) / Rows);
        public static int Slots(int modes) => Columns(modes) * Rows;
        public static int ModeIndex(int column, int row) => column * Rows + row;
        public static bool IsEmpty(int modes, int column, int row) => ModeIndex(column, row) >= modes;
        public static float GroupWidth(int modes, float columnWidth, float gap) => Columns(modes) * columnWidth + (Columns(modes) - 1) * gap;
        public static float GroupStart(int group, float columnWidth, float gap, float groupGap)
        {
            float start = 0;
            for (int i = 0; i < group; i++)
                start += GroupWidth(LobbyModeCategories.ModesIn(LobbyModeCategories.All[i]).Length, columnWidth, gap) + groupGap;
            return start;
        }
        public static int NearestGroup(float offset, float columnWidth, float gap, float groupGap)
        {
            int nearest = 0;
            for (int i = 1; i < LobbyModeCategories.All.Length; i++)
                if (Math.Abs(offset - GroupStart(i, columnWidth, gap, groupGap)) < Math.Abs(offset - GroupStart(nearest, columnWidth, gap, groupGap))) nearest = i;
            return nearest;
        }
    }
}
