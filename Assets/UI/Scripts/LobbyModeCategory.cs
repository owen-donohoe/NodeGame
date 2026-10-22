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
            => selection.Definition.Category;

        /// <summary>Display order is column first, with two rows per column.</summary>
        public static LobbyMode[] ModesIn(LobbyModeCategory category)
        {
            if (!Enum.IsDefined(typeof(LobbyModeCategory), category)) throw new ArgumentOutOfRangeException(nameof(category));
            var modes = new System.Collections.Generic.List<LobbyMode>();
            foreach (var mode in ModeDefinition.All)
                if (mode.Category == category && mode.Availability != ModeAvailability.Hidden) modes.Add(mode.Id);
            return modes.ToArray();
        }

        public static int IndexOf(LobbyModeCategory category) => Array.IndexOf(All, category);
    }

    /// <summary>Unity-free column/slot geometry shared by the strip and tests.</summary>
    public static class LobbyModeLayout
    {
        public const int Rows = 2;
        // The 50% band includes 12px padding, a 40px title, a 12px title gap and a 12px row gap.
        public static float CardWidth(float groupHeight) => Math.Max(0, (groupHeight - 88) / Rows) * 8f / 10f;
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
