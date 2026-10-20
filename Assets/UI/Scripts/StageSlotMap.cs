namespace NodeWar.Lobby
{
    /// <summary>The stage compacts chosen IDs; empty editor slots still keep their storage index.</summary>
    public static class StageSlotMap
    {
        // Chosen items retain compact indices; grants follow in the same stage order.
        public static string[] WithGranted(string[] chosen, string[] grants)
        {
            var ids = new System.Collections.Generic.List<string>();
            if (chosen != null)
                foreach (string id in chosen) if (!string.IsNullOrEmpty(id)) ids.Add(id);
            if (grants != null)
                foreach (string id in grants) if (!string.IsNullOrEmpty(id) && !ids.Contains(id)) ids.Add(id);
            return ids.ToArray();
        }

        public static string IdAt(string[] chosen, string[] grants, int stageSlot)
        {
            var ids = WithGranted(chosen, grants);
            return stageSlot >= 0 && stageSlot < ids.Length ? ids[stageSlot] : "";
        }

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
