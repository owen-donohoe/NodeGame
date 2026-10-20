namespace NodeWar.Lobby
{
    /// <summary>
    /// The Workshop's working copy of a loadout, and every rule about what may
    /// go in it.
    ///
    /// Separated from WorkshopPage because this part has no UI in it at all:
    /// which slot an equip lands in, what happens when the slots are full, and
    /// whether an ID is already spent are decisions, not layout. Keeping them
    /// UnityEngine-free means dotnet/NodeWar.Lobby.Tests can link this file and
    /// pin the behaviour, which matters more than usual here - GroupSelectionPanel
    /// expresses the same rules across five [SerializeField] slot displays and a
    /// visibility pass, where they cannot be tested at all.
    ///
    /// Every era uses three suit/five district slots; the arena selects its fixed items.
    /// </summary>
    public class LoadoutEditor
    {
        /// <summary>Returned by the Equip methods when nothing was equipped.</summary>
        public const int NoSlot = -1;

        private readonly string[] suitIDs;
        private readonly string[] districtIDs;
        private readonly string[] lockedSuits;
        private readonly string[] lockedDistricts;

        // Type-indexed equipment and skins ride along untouched; only the chosen slots are edited.
        private readonly int[] suitEras;
        private readonly int[] districtEras;
        private readonly string[] skinIDs;

        /// <summary>
        /// Starts from an existing loadout at the offline arena fallback.
        /// </summary>
        public LoadoutEditor(LoadoutData source) : this(source, 1) { }

        public LoadoutEditor(LoadoutData source, int arena)
        {
            LoadoutData normalized = LoadoutData.Normalized(source, arena);
            lockedSuits = LoadoutRules.LockedSuits(arena);
            lockedDistricts = LoadoutRules.LockedDistricts(arena);
            suitIDs = normalized.suitIDs;
            districtIDs = normalized.districtIDs;
            suitEras = normalized.suitEras;
            districtEras = normalized.districtEras;
            skinIDs = normalized.skinIDs;
        }

        public int SuitSlotCount { get { return suitIDs.Length; } }
        public int DistrictSlotCount { get { return districtIDs.Length; } }

        public bool SuitSlotsFull { get { return FirstEmptySuitSlot() == NoSlot; } }
        public bool DistrictSlotsFull { get { return FirstEmptyDistrictSlot() == NoSlot; } }

        public int FilledSuitCount { get { return CountFilled(suitIDs); } }
        public int FilledDistrictCount { get { return CountFilled(districtIDs); } }

        /// <summary>The ID in a suit slot, or "" when empty. Out of range gives "".</summary>
        public string SuitAt(int slot)
        {
            return InRange(suitIDs, slot) ? suitIDs[slot] : "";
        }

        public string DistrictAt(int slot)
        {
            return InRange(districtIDs, slot) ? districtIDs[slot] : "";
        }

        public bool IsSuitEquipped(string suitID)
        {
            return IndexOf(suitIDs, suitID) != NoSlot;
        }

        public bool IsDistrictEquipped(string districtID)
        {
            return IndexOf(districtIDs, districtID) != NoSlot;
        }

        public int FirstEmptySuitSlot()
        {
            return FirstEmpty(suitIDs);
        }

        public int FirstEmptyDistrictSlot()
        {
            return FirstEmpty(districtIDs);
        }

        /// <summary>
        /// Puts a suit in the first empty slot. Returns the slot it went into,
        /// or NoSlot if it was refused - because the ID is blank, because it is
        /// already equipped, or because every slot is taken.
        ///
        /// Refusing a duplicate rather than allowing it is deliberate: two slots
        /// spent on one suit is indistinguishable in the draft from one, so it
        /// is a slot silently thrown away. The list hides equipped items anyway,
        /// but the rule belongs with the data rather than with the view that
        /// currently happens to enforce it.
        /// </summary>
        public int EquipSuit(string suitID)
        {
            return Equip(suitIDs, suitID);
        }

        public int EquipDistrict(string districtID)
        {
            return Equip(districtIDs, districtID);
        }

        public bool ReplaceSuitSlot(int slot, string suitID)
        {
            if (IsSuitSlotLocked(slot) || !InRange(suitIDs, slot) || string.IsNullOrEmpty(suitIDs[slot]) ||
                string.IsNullOrEmpty(suitID) || IsSuitEquipped(suitID)) return false;
            suitIDs[slot] = suitID;
            return true;
        }

        public bool ReplaceDistrictSlot(int slot, string districtID)
        {
            if (IsDistrictSlotLocked(slot) || !InRange(districtIDs, slot) || string.IsNullOrEmpty(districtIDs[slot]) ||
                string.IsNullOrEmpty(districtID) || IsDistrictEquipped(districtID)) return false;
            districtIDs[slot] = districtID;
            return true;
        }

        /// <summary>Empties a suit slot. Returns what was in it, or "".</summary>
        public string ClearSuitSlot(int slot)
        {
            return IsSuitSlotLocked(slot) ? "" : Clear(suitIDs, slot);
        }

        public string ClearDistrictSlot(int slot)
        {
            return IsDistrictSlotLocked(slot) ? "" : Clear(districtIDs, slot);
        }

        public bool IsSuitSlotLocked(int slot) => System.Array.IndexOf(lockedSuits, SuitAt(slot)) >= 0;
        public bool IsDistrictSlotLocked(int slot) => System.Array.IndexOf(lockedDistricts, DistrictAt(slot)) >= 0;

        public void ClearChoices()
        {
            for (int i = 0; i < SuitSlotCount; i++) ClearSuitSlot(i);
            for (int i = 0; i < DistrictSlotCount; i++) ClearDistrictSlot(i);
        }

        /// <summary>
        /// Drops unavailable editable choices, preserving the era's locked items.
        /// Returns the cleared count so persistence can avoid redundant writes.
        /// </summary>
        public int DropUnavailable(System.Func<string, bool> suitOffered,
                                   System.Func<string, bool> districtOffered)
        {
            int cleared = 0;

            cleared += DropFrom(suitIDs, id => System.Array.IndexOf(lockedSuits, id) >= 0 || suitOffered == null || suitOffered(id));
            cleared += DropFrom(districtIDs, id => System.Array.IndexOf(lockedDistricts, id) >= 0 || districtOffered == null || districtOffered(id));

            return cleared;
        }

        /// <summary>
        /// A LoadoutData carrying the current selection. Fresh arrays, so the
        /// caller cannot reach back into this editor's state through them.
        /// </summary>
        public LoadoutData ToLoadout()
        {
            return LoadoutData.Normalized(new LoadoutData
            {
                suitIDs = (string[])suitIDs.Clone(),
                districtIDs = (string[])districtIDs.Clone(),
                suitEras = (int[])suitEras.Clone(),
                districtEras = (int[])districtEras.Clone(),
                skinIDs = (string[])skinIDs.Clone()
            });
        }

        // ===== SHARED =====

        private static int Equip(string[] slots, string id)
        {
            if (string.IsNullOrEmpty(id)) return NoSlot;
            if (IndexOf(slots, id) != NoSlot) return NoSlot;

            int slot = FirstEmpty(slots);
            if (slot == NoSlot) return NoSlot;

            slots[slot] = id;
            return slot;
        }

        private static string Clear(string[] slots, int slot)
        {
            if (!InRange(slots, slot)) return "";

            string previous = slots[slot];
            slots[slot] = "";
            return previous;
        }

        private static int DropFrom(string[] slots, System.Func<string, bool> offered)
        {
            if (offered == null) return 0;

            int cleared = 0;

            for (int i = 0; i < slots.Length; i++)
            {
                if (string.IsNullOrEmpty(slots[i])) continue;
                if (offered(slots[i])) continue;

                slots[i] = "";
                cleared++;
            }

            return cleared;
        }

        private static int CountFilled(string[] slots)
        {
            int filled = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!string.IsNullOrEmpty(slots[i])) filled++;
            }
            return filled;
        }

        private static int FirstEmpty(string[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (string.IsNullOrEmpty(slots[i])) return i;
            }
            return NoSlot;
        }

        private static int IndexOf(string[] slots, string id)
        {
            if (string.IsNullOrEmpty(id)) return NoSlot;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == id) return i;
            }
            return NoSlot;
        }

        private static bool InRange(string[] slots, int slot)
        {
            return slot >= 0 && slot < slots.Length;
        }
    }
}
