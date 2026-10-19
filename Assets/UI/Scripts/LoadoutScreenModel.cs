namespace NodeWar.Lobby
{
    /// <summary>Passive inspection and explicit edits of chosen slots, preserving source capacity.</summary>
    public sealed class LoadoutScreenModel
    {
        private readonly LoadoutEditor editor;
        public string PreviewId { get; private set; } = "";
        public string PendingDistrictId { get; private set; } = "";
        public bool Replacing => PendingDistrictId.Length > 0;

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

        public void CancelReplace() { PendingDistrictId = ""; }
    }
}
