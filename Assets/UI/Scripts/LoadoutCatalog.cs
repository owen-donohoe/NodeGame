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
        private static readonly string[] UnmappedNodeIDs = { "node_crossroads" };

        public SuitDefinition[] Suits { get; private set; }
        public NodeDefinition[] Nodes { get; private set; }

        public LoadoutCatalog(SuitDefinition[] suits, NodeDefinition[] nodes)
        {
            Suits = suits != null ? suits : new SuitDefinition[0];
            Nodes = nodes != null ? nodes : new NodeDefinition[0];
        }

        public SuitDefinition FindSuit(string suitID)
        {
            if (string.IsNullOrEmpty(suitID)) return null;
            for (int i = 0; i < Suits.Length; i++)
                if (Suits[i] != null && Suits[i].suitID == suitID) return Suits[i];
            return null;
        }

        public NodeDefinition FindNode(string nodeID)
        {
            if (string.IsNullOrEmpty(nodeID)) return null;
            for (int i = 0; i < Nodes.Length; i++)
                if (Nodes[i] != null && Nodes[i].nodeID == nodeID) return Nodes[i];
            return null;
        }

        /// <summary>Whether a slot may hold this suit: it exists and is not granted to everyone.</summary>
        public bool IsSuitOffered(string suitID)
        {
            SuitDefinition suit = FindSuit(suitID);
            return suit != null && !suit.isGlobal;
        }

        /// <summary>Whether a slot may hold this district: it exists and the draft can use it.</summary>
        public bool IsNodeOffered(string nodeID)
        {
            return FindNode(nodeID) != null && !IsUnmapped(nodeID);
        }

        public static bool IsUnmapped(string nodeID)
        {
            for (int i = 0; i < UnmappedNodeIDs.Length; i++)
                if (UnmappedNodeIDs[i] == nodeID) return true;
            return false;
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
        public int OwnedNodeCount()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            int owned = 0;

            for (int i = 0; i < Nodes.Length; i++)
            {
                NodeDefinition node = Nodes[i];
                if (node == null || !IsNodeOffered(node.nodeID)) continue;
                if (profile == null || profile.IsNodeUnlocked(node.nodeID)) owned++;
            }

            return owned;
        }

        /// <summary>A display name, falling back to the ID so a half-filled asset shows as itself.</summary>
        public string SuitName(string suitID)
        {
            SuitDefinition suit = FindSuit(suitID);
            return suit != null && !string.IsNullOrEmpty(suit.displayName) ? suit.displayName : suitID;
        }

        public string NodeName(string nodeID)
        {
            NodeDefinition node = FindNode(nodeID);
            return node != null && !string.IsNullOrEmpty(node.displayName) ? node.displayName : nodeID;
        }

        /// <summary>The player's saved loadout as an editor, or an empty one with no profile.</summary>
        public static LoadoutEditor CurrentLoadout()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            return new LoadoutEditor(profile != null ? profile.Loadout : LoadoutData.CreateEmpty());
        }
    }
}
