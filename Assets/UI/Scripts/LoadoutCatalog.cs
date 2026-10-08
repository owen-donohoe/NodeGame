namespace NodeWar.Lobby
{
    /// <summary>
    /// What the loadout can be built from: the suit and district definitions,
    /// and the rules about which of them a slot may hold.
    ///
    /// One instance for the lobby, handed to every screen that reasons about the
    /// loadout - the Workshop that edits it, Home's preview and the battle sheet
    /// that flag a short side - so the three can never disagree about what
    /// "available" or "owned" means.
    ///
    /// The rules, from docs/ui-migration-inventory.md:
    ///   - a suit flagged isGlobal (Warrior) is granted to everyone by
    ///     GameManager.BuildDraftedSuits, so a slot spent on it buys nothing;
    ///   - Crossroads cannot be mapped to a DistrictType by GameManager, so a
    ///     slot spent on it produces nothing.
    /// Neither is "offered". "Owned" is offered and unlocked, asked of
    /// PlayerProfile even though its unlock checks return true today.
    /// </summary>
    public class LoadoutCatalog
    {
        /// <summary>
        /// Districts no slot may hold. Only Crossroads (inventory finding 4).
        /// When DistrictType gains a Crossroads member, this array empties.
        /// </summary>
        private static readonly string[] UnmappedDistrictIDs = { "node_crossroads" };

        public SuitDefinition[] Suits { get; private set; }
        public DistrictDefinition[] Districts { get; private set; }

        public LoadoutCatalog(SuitDefinition[] suits, DistrictDefinition[] districts)
        {
            Suits = suits != null ? suits : new SuitDefinition[0];
            var canonical = new System.Collections.Generic.List<DistrictDefinition>();
            foreach (int type in NodeWar.Backend.CatalogKeys.CatalogDistrictTypes)
            {
                string id = NodeWar.Backend.CatalogKeys.DistrictLobbyId(type);
                DistrictDefinition definition = null;
                if (districts != null)
                    foreach (var item in districts)
                        if (item != null && item.districtID == id) { definition = item; break; }
                if (definition == null)
                {
                    var descriptor = NodeWar.View.DistrictFallback.Describe((NodeWar.Simulation.DistrictType)type);
                    definition = UnityEngine.ScriptableObject.CreateInstance<DistrictDefinition>();
                    definition.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
                    definition.districtID = id;
                    definition.displayName = descriptor.Name;
                    definition.description = descriptor.Description;
                    definition.category = DistrictCategory.Selectable;
                }
                canonical.Add(definition);
            }
            Districts = canonical.ToArray();
        }

        public SuitDefinition FindSuit(string suitID)
        {
            if (string.IsNullOrEmpty(suitID)) return null;
            for (int i = 0; i < Suits.Length; i++)
                if (Suits[i] != null && Suits[i].suitID == suitID) return Suits[i];
            return null;
        }

        public DistrictDefinition FindDistrict(string districtID)
        {
            if (string.IsNullOrEmpty(districtID)) return null;
            for (int i = 0; i < Districts.Length; i++)
                if (Districts[i] != null && Districts[i].districtID == districtID) return Districts[i];
            return null;
        }

        /// <summary>Whether a slot may hold this suit: it exists and is not granted to everyone.</summary>
        public bool IsSuitOffered(string suitID)
        {
            SuitDefinition suit = FindSuit(suitID);
            return suit != null && !suit.isGlobal;
        }

        /// <summary>Whether a slot may hold this district: it exists and the draft can use it.</summary>
        public bool IsDistrictOffered(string districtID)
        {
            return FindDistrict(districtID) != null && !IsUnmapped(districtID);
        }

        public static bool IsUnmapped(string districtID)
        {
            for (int i = 0; i < UnmappedDistrictIDs.Length; i++)
                if (UnmappedDistrictIDs[i] == districtID) return true;
            return !NodeWar.Simulation.PlacementLegality.IsDraftable((NodeWar.Simulation.DistrictType)NodeWar.Backend.DistrictMigration.SourceType(districtID));
        }

        /// <summary>Suits the player could put in a slot: offered and unlocked.</summary>
        public int OwnedSuitCount()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            int owned = 0;

            for (int i = 0; i < Suits.Length; i++)
            {
                SuitDefinition suit = Suits[i];
                if (suit == null || !IsSuitOffered(suit.suitID)) continue;
                if (profile == null || profile.IsSuitUnlocked(suit.suitID)) owned++;
            }

            return owned;
        }

        /// <summary>Districts the player could put in a slot: offered and unlocked.</summary>
        public int OwnedDistrictCount()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            int owned = 0;

            for (int i = 0; i < Districts.Length; i++)
            {
                DistrictDefinition node = Districts[i];
                if (node == null || !IsDistrictOffered(node.districtID)) continue;
                if (profile == null || profile.IsDistrictUnlocked(node.districtID)) owned++;
            }

            return owned;
        }

        /// <summary>A display name, falling back to the ID so a half-filled asset shows as itself.</summary>
        public string SuitName(string suitID)
        {
            SuitDefinition suit = FindSuit(suitID);
            return suit != null && !string.IsNullOrEmpty(suit.displayName) ? suit.displayName : suitID;
        }

        public string DistrictName(string districtID)
        {
            DistrictDefinition node = FindDistrict(districtID);
            return node != null && !string.IsNullOrEmpty(node.displayName) ? node.displayName : districtID;
        }

        /// <summary>
        /// The player's saved loadout as an editor, or an empty one with no
        /// profile - with any entry no slot may hold already dropped, as the
        /// Workshop drops it on load. Without that, Home and the battle sheet
        /// would count a saved Warrior or Crossroads as a filled slot the
        /// Workshop shows as empty. Nothing is saved here; the Workshop owns that.
        /// </summary>
        public LoadoutEditor CurrentLoadout()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            LoadoutEditor loadout = new LoadoutEditor(profile != null ? profile.Loadout : LoadoutData.CreateEmpty());
            loadout.DropUnavailable(IsSuitOffered, IsDistrictOffered);
            return loadout;
        }
    }
}
