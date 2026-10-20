namespace NodeWar.Lobby
{
    /// <summary>Passive inspection and explicit edits of chosen slots, preserving source capacity.</summary>
    public sealed class LoadoutScreenModel
    {
        private readonly LoadoutEditor editor;
        public string PreviewId { get; private set; } = "";
        public string PendingDistrictId { get; private set; } = "";
        public string PendingSuitId { get; private set; } = "";
        public bool ReplacingSuit => PendingSuitId.Length > 0;
        public bool Replacing => PendingDistrictId.Length > 0 || ReplacingSuit;
        public bool BrowsingSuits { get; private set; }
        public bool PreviewIsSuit { get; private set; }
        public void Browse(bool suits) { BrowsingSuits = suits; CancelReplace(); }
        public void Inspect(string id, bool suit) { PreviewIsSuit = suit; Preview(id); }

        // One entry point for sheet Use: fill first, otherwise wait for a stage pick.
        public bool Use(bool canUse, bool busy)
        {
            if (!canUse || busy) return false;
            if (UsePreview(true, PreviewIsSuit)) return true;
            if (PreviewIsSuit) BeginSuitReplace(true, false);
            else BeginReplace(true, false);
            return false;
        }

        public static bool NeedsEquipment(LoadoutChoice choice) => !choice.Equipped && choice.Selectable;
        public static bool ValidChoice(LoadoutChoice[] choices, string id) =>
            choices.Length == 0 || System.Array.Exists(choices, c => c.ID == id && (c.Equipped || c.Selectable));

        public LoadoutScreenModel(LoadoutData chosen) { editor = new LoadoutEditor(chosen); }
        public void Preview(string lobbyId)
        {
            string id = lobbyId ?? "";
            if (PreviewId != id) CancelReplace();
            PreviewId = id;
        }
        public bool UsePreview(bool canUse, bool isSuit)
        {
            if (!canUse) return false;
            return (isSuit ? editor.EquipSuit(PreviewId) : editor.EquipDistrict(PreviewId)) != LoadoutEditor.NoSlot;
        }
        public bool ClearChosen(bool isSuit, int slot) => !string.IsNullOrEmpty(
            isSuit ? editor.ClearSuitSlot(slot) : editor.ClearDistrictSlot(slot));
        public LoadoutData ToData() => editor.ToLoadout();

        public bool BeginReplace(bool canUse, bool busy)
        {
            if (!canUse || busy || string.IsNullOrEmpty(PreviewId) || editor.IsDistrictEquipped(PreviewId) || !editor.DistrictSlotsFull) return false;
            PendingDistrictId = PreviewId;
            return true;
        }

        public bool CompleteReplace(int slot, bool canUse, bool busy)
        {
            string id = PendingDistrictId;
            CancelReplace();
            return !busy && canUse && id.Length > 0 && PreviewId == id && editor.ReplaceDistrictSlot(slot, id);
        }

        public bool BeginSuitReplace(bool canUse, bool busy)
        {
            if (!canUse || busy || string.IsNullOrEmpty(PreviewId) || editor.IsSuitEquipped(PreviewId) || !editor.SuitSlotsFull) return false;
            PendingSuitId = PreviewId;
            return true;
        }

        public bool CompleteSuitReplace(int slot, bool canUse, bool busy)
        {
            string id = PendingSuitId;
            CancelReplace();
            return !busy && canUse && id.Length > 0 && PreviewId == id && editor.ReplaceSuitSlot(slot, id);
        }

        public void CancelReplace() { PendingDistrictId = ""; PendingSuitId = ""; }
    }
}
