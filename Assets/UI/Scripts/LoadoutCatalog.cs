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
    /// Fixed defaults live in the loadout and become editable by era through
    /// LoadoutRules. Base ownership and usability come from server inventory
    /// and the shared debut policy, with starter ownership before state arrives.
    /// </summary>
    public class LoadoutCatalog
    {
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

        public int Arena => LoadoutRules.ArenaOrFallback(NodeWar.Backend.BackendServices.LastKnownState?.Rank);

        public bool IsSuitOffered(string suitID) => IsSuitOffered(suitID, Arena);
        public bool IsSuitOffered(string suitID, int arena) =>
            FindSuit(suitID) != null && LoadoutRules.IsSuitOffered(suitID, arena);

        public bool IsDistrictOffered(string districtID) => IsDistrictOffered(districtID, Arena);
        public bool IsDistrictOffered(string districtID, int arena) =>
            FindDistrict(districtID) != null && LoadoutRules.IsDistrictOffered(districtID, arena);
        /// <summary>Resolve base ownership from a server snapshot.</summary>
        public UnlockState UnlockForBase(string lobbyId, NodeWar.Backend.PlayerState state) =>
            UnlockStates.ForBase(lobbyId, state);

        /// <summary>A display name, falling back to the ID so a half-filled asset shows as itself.</summary>
        public string SuitName(string suitID)
        {
            SuitDefinition suit = FindSuit(suitID);
            return suit != null && !string.IsNullOrEmpty(suit.displayName) ? suit.displayName : suitID;
        }

        public string DistrictName(string districtID)
        {
            DistrictDefinition node = FindDistrict(districtID);
            return node != null && !string.IsNullOrEmpty(node.displayName) ? node.displayName :
                NodeWar.View.DistrictFallback.Describe((NodeWar.Simulation.DistrictType)NodeWar.Backend.DistrictMigration.SourceType(districtID)).Name ?? districtID;
        }

        /// <summary>
        /// The player's saved loadout as an editor, or an empty one with no
        /// profile - with any entry no slot may hold already dropped, as the
        /// Workshop drops it on load. Without that, Home and the battle sheet
        /// would count an unavailable saved choice as a filled slot the
        /// Workshop shows as empty. Nothing is saved here; the Workshop owns that.
        /// </summary>
        public LoadoutEditor CurrentLoadout() => CurrentLoadout(NodeWar.Backend.BackendServices.LastKnownState);

        public LoadoutEditor CurrentLoadout(NodeWar.Backend.PlayerState state)
        {
            int arena = LoadoutRules.ArenaOrFallback(state?.Rank);
            PlayerProfile profile = PlayerProfile.Instance;
            var editor = LoadoutRules.LocalEditor(profile != null ? profile.Loadout : default, arena);
            editor.DropUnavailable(id => IsSuitOffered(id, arena) && UnlockForBase(id, state).CanUse,
                id => IsDistrictOffered(id, arena) && UnlockForBase(id, state).CanUse);
            return editor;
        }
    }
}
