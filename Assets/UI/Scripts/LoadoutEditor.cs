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
    /// Slot counts come from LoadoutData.SuitSlots / NodeSlots, so answering the
    /// open 2-vs-3 balance question stays an edit to those constants.
    /// </summary>
    public class LoadoutEditor
    {
        /// <summary>Returned by the Equip methods when nothing was equipped.</summary>
        public const int NoSlot = -1;

        private readonly string[] suitIDs;
        private readonly string[] nodeIDs;

        /// <summary>
        /// Starts from an existing loadout, normalised - so a `default` struct,
        /// a save written at different slot counts and a peer's loadout all
        /// arrive here the same shape.
        /// </summary>
        public LoadoutEditor(LoadoutData source)
        {
            LoadoutData normalized = LoadoutData.Normalized(source);
            suitIDs = normalized.suitIDs;
            nodeIDs = normalized.nodeIDs;
        }

        public int SuitSlotCount { get { return suitIDs.Length; } }
        public int NodeSlotCount { get { return nodeIDs.Length; } }

        public bool SuitSlotsFull { get { return FirstEmptySuitSlot() == NoSlot; } }
        public bool NodeSlotsFull { get { return FirstEmptyNodeSlot() == NoSlot; } }

        /// <summary>The ID in a suit slot, or "" when empty. Out of range gives "".</summary>
        public string SuitAt(int slot)
        {
            return InRange(suitIDs, slot) ? suitIDs[slot] : "";
        }

        public string NodeAt(int slot)
        {
            return InRange(nodeIDs, slot) ? nodeIDs[slot] : "";
        }

        public bool IsSuitEquipped(string suitID)
        {
            return IndexOf(suitIDs, suitID) != NoSlot;
        }

        public bool IsNodeEquipped(string nodeID)
        {
            return IndexOf(nodeIDs, nodeID) != NoSlot;
        }

        public int FirstEmptySuitSlot()
        {
            return FirstEmpty(suitIDs);
        }

        public int FirstEmptyNodeSlot()
        {
            return FirstEmpty(nodeIDs);
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

        public int EquipNode(string nodeID)
        {
            return Equip(nodeIDs, nodeID);
        }

        /// <summary>Empties a suit slot. Returns what was in it, or "".</summary>
        public string ClearSuitSlot(int slot)
        {
            return Clear(suitIDs, slot);
        }

        public string ClearNodeSlot(int slot)
        {
            return Clear(nodeIDs, slot);
        }

        /// <summary>
        /// Drops any equipped ID that is no longer offered - a suit that has
        /// become globally granted, or a district the picker excludes. Returns
        /// how many entries were cleared, so the caller can decide whether the
        /// profile is worth rewriting.
        ///
        /// This is what stops an existing save from showing an item in a slot
        /// that the list has no row for. Both cases exist today: a profile can
        /// hold suit_warrior, which every player is granted regardless
        /// (GameManager.BuildDraftedSuits), and node_crossroads, which
        /// MapNodeIDToDistrict discards (inventory findings 8 and 4). Either is
        /// a slot already producing nothing; clearing it hands the slot back.
        /// </summary>
        public int DropUnavailable(System.Func<string, bool> suitOffered,
                                   System.Func<string, bool> nodeOffered)
        {
            int cleared = 0;

            cleared += DropFrom(suitIDs, suitOffered);
            cleared += DropFrom(nodeIDs, nodeOffered);

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
                nodeIDs = (string[])nodeIDs.Clone()
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
