namespace NodeWar.Lobby
{
    /// <summary>Passive inspection and explicit edits of chosen slots, preserving source capacity.</summary>
    public sealed class LoadoutScreenModel
    {
        private readonly LoadoutEditor editor;
        public string PreviewId { get; private set; } = "";

        public LoadoutScreenModel(LoadoutData chosen) { editor = new LoadoutEditor(chosen); }
        public void Preview(string lobbyId) { PreviewId = lobbyId ?? ""; }
        public bool UsePreview(bool canUse, bool isSuit)
        {
            if (!canUse) return false;
            return (isSuit ? editor.EquipSuit(PreviewId) : editor.EquipDistrict(PreviewId)) != LoadoutEditor.NoSlot;
        }
        public bool ClearChosen(bool isSuit, int slot) => !string.IsNullOrEmpty(
            isSuit ? editor.ClearSuitSlot(slot) : editor.ClearDistrictSlot(slot));
        public LoadoutData ToData() => editor.ToLoadout();
    }
}
