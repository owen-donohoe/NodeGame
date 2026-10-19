namespace NodeWar.Lobby
{
    /// <summary>The stage compacts chosen IDs; empty editor slots still keep their storage index.</summary>
    public static class StageSlotMap
    {
        public static int ToStage(string[] chosen, int chosenSlot)
        {
            if (chosen == null || chosenSlot < 0 || chosenSlot >= chosen.Length || string.IsNullOrEmpty(chosen[chosenSlot])) return -1;
            int stage = 0;
            for (int i = 0; i < chosenSlot; i++) if (!string.IsNullOrEmpty(chosen[i])) stage++;
            return stage;
        }
        public static int ToChosen(string[] chosen, int stageSlot)
        {
            if (chosen == null || stageSlot < 0) return -1;
            int stage = 0;
            for (int i = 0; i < chosen.Length; i++)
                if (!string.IsNullOrEmpty(chosen[i]) && stage++ == stageSlot) return i;
            return -1;
        }
    }
}
