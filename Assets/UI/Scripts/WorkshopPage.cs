using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The Workshop: pick the suits and districts you bring into the draft.
    /// The UI Toolkit replacement for GroupSelectionPanel.
    ///
    /// Slot counts come from LoadoutData.SuitSlots and NodeSlots, so the slots
    /// are built rather than wired - the open 2-vs-3 balance question stays an
    /// edit to those two constants. All the rules about what may occupy a slot
    /// live in LoadoutEditor, which has no UnityEngine reference and is covered
    /// by dotnet/NodeWar.Lobby.Tests.
    ///
    /// Three facts about the game shape this screen, all recorded in
    /// docs/ui-migration-inventory.md:
    ///
    ///   - There is no art. Every icon on all 9 NodeDefinitions and all 5
    ///     SuitDefinitions is null (finding 6), so items show as lettered,
    ///     tinted tiles. See ItemTint.
    ///
    ///   - Warrior is granted to every player regardless of loadout, because
    ///     GameManager.BuildDraftedSuits adds it unconditionally (finding 8).
    ///     Suits flagged isGlobal are therefore shown above the slots and kept
    ///     out of the list: a slot spent on one buys nothing.
    ///
    ///   - Crossroads is discarded. GameManager.MapNodeIDToDistrict has its
    ///     line commented out and DistrictType has no Crossroads member
    ///     (finding 4), so a slot spent on it produces nothing at all. It is
    ///     excluded here rather than offered and dropped in silence. The asset
    ///     and the old panel are untouched; deletion is S5's business.
    ///
    /// Reads PlayerProfile and writes it back through SetLoadout. No simulation
    /// state exists in the lobby scene, so the view/UI boundary has nothing to
    /// bite on yet.
    /// </summary>
    public class WorkshopPage : LobbyPage
    {
        /// <summary>
        /// Which list the page is showing.
        ///
        /// Not GroupSelectionPanel's SelectionTab, even though the two hold the
        /// same two values: that enum sits in GroupSelectionPanel.cs and is
        /// deleted along with it in S5. The new stack owning its own is what
        /// keeps that deletion a one-file change.
        /// </summary>
        private enum Tab
        {
            Suits,
            Districts
        }

        /// <summary>
        /// Districts the picker refuses to offer.
        ///
        /// Only Crossroads, and only because GameManager cannot map it to a
        /// DistrictType (inventory finding 4). This is a hardcoded ID in the UI
        /// on purpose - the honest alternative would be for the UI to ask
        /// GameManager what it can map, and GameManager does not exist in the
        /// lobby scene. When DistrictType gains a Crossroads member, or the
        /// asset goes, this array empties.
        /// </summary>
        private static readonly string[] UnmappedNodeIDs = { "node_crossroads" };

        private readonly SuitDefinition[] allSuits;
        private readonly NodeDefinition[] allNodes;

        private readonly List<SuitDefinition> grantedSuits = new List<SuitDefinition>();
        private readonly List<SuitDefinition> draftableSuits = new List<SuitDefinition>();
        private readonly List<NodeDefinition> draftableNodes = new List<NodeDefinition>();

        private readonly VisualElement grantedBand;
        private readonly VisualElement grantedTiles;
        private readonly Label grantedNames;

        private readonly Label suitsLabel;
        private readonly Label nodesLabel;
        private readonly VisualElement suitSlotHost;
        private readonly VisualElement nodeSlotHost;

        private readonly Button suitsTabButton;
        private readonly Button nodesTabButton;
        private readonly Label hintLabel;
        private readonly ScrollView suitList;
        private readonly ScrollView nodeList;

        private readonly List<SlotView> suitSlots = new List<SlotView>();
        private readonly List<SlotView> nodeSlots = new List<SlotView>();
        private readonly List<ItemRow> suitRows = new List<ItemRow>();
        private readonly List<ItemRow> nodeRows = new List<ItemRow>();

        private LoadoutEditor loadout = new LoadoutEditor(LoadoutData.CreateEmpty());
        private Tab activeTab = Tab.Suits;
        private bool built;

        public WorkshopPage(VisualTreeAsset layout, SuitDefinition[] suits, NodeDefinition[] nodes)
            : base(LobbyPageID.Workshop, Build(layout))
        {
            allSuits = suits != null ? suits : new SuitDefinition[0];
            allNodes = nodes != null ? nodes : new NodeDefinition[0];

            grantedBand = Root.Q<VisualElement>("workshop-granted");
            grantedTiles = Root.Q<VisualElement>("workshop-granted-tiles");
            grantedNames = Root.Q<Label>("workshop-granted-names");

            suitsLabel = Root.Q<Label>("workshop-suits-label");
            nodesLabel = Root.Q<Label>("workshop-nodes-label");
            suitSlotHost = Root.Q<VisualElement>("workshop-suit-slots");
            nodeSlotHost = Root.Q<VisualElement>("workshop-node-slots");

            suitsTabButton = Root.Q<Button>("workshop-tab-suits");
            nodesTabButton = Root.Q<Button>("workshop-tab-nodes");
            hintLabel = Root.Q<Label>("workshop-hint");
            suitList = Root.Q<ScrollView>("workshop-list-suits");
            nodeList = Root.Q<ScrollView>("workshop-list-nodes");

            if (suitsTabButton != null) suitsTabButton.clicked += () => SetTab(Tab.Suits);
            if (nodesTabButton != null) nodesTabButton.clicked += () => SetTab(Tab.Districts);

            PartitionDefinitions();
        }

        private static VisualElement Build(VisualTreeAsset layout)
        {
            VisualElement root = new VisualElement();
            root.name = "page-workshop";

            if (layout != null)
            {
                layout.CloneTree(root);
            }
            else
            {
                // Same degradation as every other page: labelled, inert, and
                // not an exception.
                VisualElement box = new VisualElement();
                box.AddToClassList("placeholder");
                box.style.flexGrow = 1;

                Label note = new Label("Workshop layout missing - assign WorkshopPage.uxml");
                note.AddToClassList("placeholder__label");

                box.Add(note);
                root.Add(box);
            }

            return root;
        }

        public override void OnShow()
        {
            if (!built)
            {
                BuildGrantedBand();
                BuildSlots();
                BuildRows();
                built = true;
            }

            LoadFromProfile();
            RefreshAll();
        }

        /// <summary>
        /// A second save on the way out. Every change already persists as it
        /// happens, so this only covers the case where SetLoadout is made
        /// deferred later; it costs one JSON write per visit.
        /// </summary>
        public override void OnHide()
        {
            SaveToProfile();
        }

        // ===== DEFINITIONS =====

        /// <summary>
        /// Splits the definition assets into what the slots may hold and what
        /// they may not. Runs once - the assets are serialized fields and
        /// cannot change at runtime.
        /// </summary>
        private void PartitionDefinitions()
        {
            for (int i = 0; i < allSuits.Length; i++)
            {
                SuitDefinition suit = allSuits[i];
                if (suit == null || string.IsNullOrEmpty(suit.suitID)) continue;

                if (suit.isGlobal)
                    grantedSuits.Add(suit);
                else
                    draftableSuits.Add(suit);
            }

            for (int i = 0; i < allNodes.Length; i++)
            {
                NodeDefinition node = allNodes[i];
                if (node == null || string.IsNullOrEmpty(node.nodeID)) continue;
                if (IsUnmapped(node.nodeID)) continue;

                draftableNodes.Add(node);
            }
        }

        private static bool IsUnmapped(string nodeID)
        {
            for (int i = 0; i < UnmappedNodeIDs.Length; i++)
            {
                if (UnmappedNodeIDs[i] == nodeID) return true;
            }
            return false;
        }

        private bool IsSuitOffered(string suitID)
        {
            return FindSuit(suitID) != null;
        }

        private bool IsNodeOffered(string nodeID)
        {
            return FindNode(nodeID) != null;
        }

        private SuitDefinition FindSuit(string suitID)
        {
            if (string.IsNullOrEmpty(suitID)) return null;

            for (int i = 0; i < draftableSuits.Count; i++)
            {
                if (draftableSuits[i].suitID == suitID) return draftableSuits[i];
            }
            return null;
        }

        private NodeDefinition FindNode(string nodeID)
        {
            if (string.IsNullOrEmpty(nodeID)) return null;

            for (int i = 0; i < draftableNodes.Count; i++)
            {
                if (draftableNodes[i].nodeID == nodeID) return draftableNodes[i];
            }
            return null;
        }

        // ===== BUILDING =====

        private void BuildGrantedBand()
        {
            if (grantedBand == null) return;

            // No suit sets isGlobal: say nothing rather than show an empty band.
            if (grantedSuits.Count == 0)
            {
                grantedBand.AddToClassList("workshop__granted--hidden");
                return;
            }

            string names = "";

            for (int i = 0; i < grantedSuits.Count; i++)
            {
                SuitDefinition suit = grantedSuits[i];

                if (grantedTiles != null)
                {
                    ItemTile tile = new ItemTile();
                    tile.SetItem(suit.suitID, suit.displayName);
                    tile.Root.AddToClassList("tile--granted");
                    grantedTiles.Add(tile.Root);
                }

                names += (names.Length > 0 ? ", " : "") + DisplayNameOf(suit);
            }

            if (grantedNames != null) grantedNames.text = names;
        }

        private void BuildSlots()
        {
            BuildSlotRow(suitSlotHost, suitSlots, LoadoutData.SuitSlots, Tab.Suits);
            BuildSlotRow(nodeSlotHost, nodeSlots, LoadoutData.NodeSlots, Tab.Districts);
        }

        private void BuildSlotRow(VisualElement host, List<SlotView> into, int count, Tab tab)
        {
            if (host == null) return;

            for (int i = 0; i < count; i++)
            {
                int slot = i;

                SlotView view = new SlotView(() => OnSlotClicked(tab, slot));
                into.Add(view);
                host.Add(view.Root);
            }
        }

        private void BuildRows()
        {
            BuildRowList(suitList, suitRows, Tab.Suits);
            BuildRowList(nodeList, nodeRows, Tab.Districts);
        }

        private void BuildRowList(ScrollView list, List<ItemRow> into, Tab tab)
        {
            if (list == null) return;

            int count = tab == Tab.Suits ? draftableSuits.Count : draftableNodes.Count;

            if (count == 0)
            {
                list.Add(BuildEmptyListNote(tab));
                return;
            }

            for (int i = 0; i < count; i++)
            {
                string id;
                string displayName;
                string description;

                if (tab == Tab.Suits)
                {
                    SuitDefinition suit = draftableSuits[i];
                    id = suit.suitID;
                    displayName = DisplayNameOf(suit);
                    description = suit.description;
                }
                else
                {
                    NodeDefinition node = draftableNodes[i];
                    id = node.nodeID;
                    displayName = DisplayNameOf(node);
                    description = node.description;
                }

                string equipID = id;
                ItemRow row = new ItemRow(id, displayName, description,
                                          () => OnRowClicked(tab, equipID));

                into.Add(row);
                list.Add(row.Root);
            }
        }

        /// <summary>
        /// What the list says when there is nothing to list. Only reachable
        /// when the definition arrays are unassigned, which means the Editor
        /// setup has not been re-run since those fields were added - so the
        /// message names the fix.
        /// </summary>
        private static VisualElement BuildEmptyListNote(Tab tab)
        {
            VisualElement box = new VisualElement();
            box.AddToClassList("placeholder");
            box.AddToClassList("workshop__empty");

            Label note = new Label(
                "No " + (tab == Tab.Suits ? "suit" : "district") + " definitions assigned.\n" +
                "Run Tools > Node War > Set Up UI Toolkit Lobby.");
            note.AddToClassList("placeholder__label");

            box.Add(note);
            return box;
        }

        // ===== PROFILE =====

        private void LoadFromProfile()
        {
            PlayerProfile profile = PlayerProfile.Instance;

            loadout = new LoadoutEditor(
                profile != null ? profile.Loadout : LoadoutData.CreateEmpty());

            // A saved loadout can hold a suit that has since become globally
            // granted, or Crossroads. Both are slots already producing nothing;
            // clearing them hands the slots back rather than showing an item
            // the list has no row for.
            int cleared = loadout.DropUnavailable(IsSuitOffered, IsNodeOffered);

            if (cleared > 0)
            {
                Debug.Log("[Workshop] Cleared " + cleared +
                          " loadout entr" + (cleared == 1 ? "y" : "ies") +
                          " that the draft cannot use.");
                SaveToProfile();
            }
        }

        private void SaveToProfile()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            if (profile == null) return;

            profile.SetLoadout(loadout.ToLoadout());
        }

        // ===== INTERACTION =====

        private void OnRowClicked(Tab tab, string itemID)
        {
            int slot = tab == Tab.Suits
                ? loadout.EquipSuit(itemID)
                : loadout.EquipNode(itemID);

            // Refused: already equipped, or no slot free. Rows in either state
            // are disabled, so this is a guard rather than a path.
            if (slot == LoadoutEditor.NoSlot) return;

            // Persist per change rather than only on the way out. A phone can
            // be killed at any moment and there is no "back" to rely on, so a
            // selection that survives only until OnHide is a selection that
            // gets lost.
            SaveToProfile();
            RefreshAll();
        }

        private void OnSlotClicked(Tab tab, int slot)
        {
            string removed = tab == Tab.Suits
                ? loadout.ClearSuitSlot(slot)
                : loadout.ClearNodeSlot(slot);

            // An empty slot is not a dead tap: it sends you to the list that
            // fills it.
            if (string.IsNullOrEmpty(removed))
            {
                SetTab(tab);
                return;
            }

            SaveToProfile();
            RefreshAll();
        }

        private void SetTab(Tab tab)
        {
            activeTab = tab;
            RefreshAll();
        }

        // ===== REFRESH =====

        private void RefreshAll()
        {
            RefreshSlots();
            RefreshTabs();
            RefreshRows();
            RefreshHint();
        }

        private void RefreshSlots()
        {
            for (int i = 0; i < suitSlots.Count; i++)
            {
                string id = loadout.SuitAt(i);
                SuitDefinition suit = FindSuit(id);
                suitSlots[i].Set(id, suit != null ? DisplayNameOf(suit) : id);
            }

            for (int i = 0; i < nodeSlots.Count; i++)
            {
                string id = loadout.NodeAt(i);
                NodeDefinition node = FindNode(id);
                nodeSlots[i].Set(id, node != null ? DisplayNameOf(node) : id);
            }

            if (suitsLabel != null)
                suitsLabel.text = "Suits " + FilledCount(true) + "/" + loadout.SuitSlotCount;

            if (nodesLabel != null)
                nodesLabel.text = "Districts " + FilledCount(false) + "/" + loadout.NodeSlotCount;
        }

        private int FilledCount(bool suits)
        {
            int count = suits ? loadout.SuitSlotCount : loadout.NodeSlotCount;
            int filled = 0;

            for (int i = 0; i < count; i++)
            {
                string id = suits ? loadout.SuitAt(i) : loadout.NodeAt(i);
                if (!string.IsNullOrEmpty(id)) filled++;
            }

            return filled;
        }

        private void RefreshTabs()
        {
            bool suitsActive = activeTab == Tab.Suits;

            if (suitsTabButton != null)
                suitsTabButton.EnableInClassList("workshop__tab--active", suitsActive);

            if (nodesTabButton != null)
                nodesTabButton.EnableInClassList("workshop__tab--active", !suitsActive);

            if (suitList != null)
                suitList.EnableInClassList("workshop__list--hidden", !suitsActive);

            if (nodeList != null)
                nodeList.EnableInClassList("workshop__list--hidden", suitsActive);
        }

        /// <summary>
        /// Row state is recomputed here rather than fixed at build time, so an
        /// unlock that lands while the lobby is open is picked up on the next
        /// visit. It also means a null PlayerProfile at construction does not
        /// leave every row permanently locked.
        /// </summary>
        private void RefreshRows()
        {
            PlayerProfile profile = PlayerProfile.Instance;

            for (int i = 0; i < suitRows.Count; i++)
            {
                ItemRow row = suitRows[i];

                bool locked = profile != null && !profile.IsSuitUnlocked(row.ItemID);
                bool equipped = loadout.IsSuitEquipped(row.ItemID);

                row.SetState(locked, equipped, loadout.SuitSlotsFull);
            }

            for (int i = 0; i < nodeRows.Count; i++)
            {
                ItemRow row = nodeRows[i];

                bool locked = profile != null && !profile.IsNodeUnlocked(row.ItemID);
                bool equipped = loadout.IsNodeEquipped(row.ItemID);

                row.SetState(locked, equipped, loadout.NodeSlotsFull);
            }
        }

        private void RefreshHint()
        {
            if (hintLabel == null) return;

            bool full = activeTab == Tab.Suits ? loadout.SuitSlotsFull : loadout.NodeSlotsFull;

            hintLabel.EnableInClassList("workshop__hint--hidden", !full);
        }

        // ===== SHARED =====

        /// <summary>
        /// The name to show. Falls back to the ID when displayName is blank, so
        /// a half-filled definition asset is visible as itself rather than as an
        /// empty row.
        /// </summary>
        private static string DisplayNameOf(SuitDefinition suit)
        {
            return !string.IsNullOrEmpty(suit.displayName) ? suit.displayName : suit.suitID;
        }

        private static string DisplayNameOf(NodeDefinition node)
        {
            return !string.IsNullOrEmpty(node.displayName) ? node.displayName : node.nodeID;
        }

        // ===== ELEMENTS =====

        /// <summary>
        /// One loadout slot. Filled slots unequip on tap; empty ones jump to
        /// the list that fills them.
        /// </summary>
        private class SlotView
        {
            public Button Root { get; private set; }

            private readonly ItemTile tile;
            private readonly Label nameLabel;

            public SlotView(System.Action onClick)
            {
                Root = new Button();
                Root.AddToClassList("workshop__slot");

                tile = new ItemTile();
                tile.Root.AddToClassList("tile--slot");

                nameLabel = new Label();
                nameLabel.AddToClassList("caption");
                nameLabel.AddToClassList("workshop__slot-name");
                nameLabel.pickingMode = PickingMode.Ignore;

                Root.Add(tile.Root);
                Root.Add(nameLabel);

                if (onClick != null) Root.clicked += onClick;
            }

            public void Set(string itemID, string displayName)
            {
                bool filled = !string.IsNullOrEmpty(itemID);

                if (filled)
                    tile.SetItem(itemID, displayName);
                else
                    tile.SetEmpty();

                nameLabel.text = filled ? displayName : "Empty";
                Root.EnableInClassList("workshop__slot--filled", filled);
            }
        }

        /// <summary>
        /// One row in the available-items list: tile, name, what it does, and
        /// what tapping it will do.
        ///
        /// The description falls back to the raw ID when a definition has none.
        /// Without art the name is the only handle a player has on an item, and
        /// one of the nine district assets currently carries the wrong
        /// displayName - Camp.asset reads "Watchtower" - so having the ID
        /// somewhere on screen is what makes those two rows distinguishable.
        /// </summary>
        private class ItemRow
        {
            public string ItemID { get; private set; }

            public Button Root { get; private set; }

            private readonly Label statusLabel;

            public ItemRow(string itemID, string displayName, string description,
                           System.Action onClick)
            {
                ItemID = itemID;

                Root = new Button();
                Root.AddToClassList("workshop__row");

                ItemTile tile = new ItemTile();
                tile.SetItem(itemID, displayName);
                tile.Root.AddToClassList("tile--row");

                VisualElement text = new VisualElement();
                text.AddToClassList("workshop__row-text");
                text.pickingMode = PickingMode.Ignore;

                Label name = new Label(displayName);
                name.AddToClassList("body");
                name.AddToClassList("workshop__row-name");
                text.Add(name);

                Label subtitle = new Label(
                    !string.IsNullOrEmpty(description) ? description : itemID);
                subtitle.AddToClassList("caption");
                subtitle.AddToClassList("workshop__row-subtitle");
                text.Add(subtitle);

                statusLabel = new Label();
                statusLabel.AddToClassList("caption");
                statusLabel.AddToClassList("workshop__row-status");
                statusLabel.pickingMode = PickingMode.Ignore;

                Root.Add(tile.Root);
                Root.Add(text);
                Root.Add(statusLabel);

                if (onClick != null) Root.clicked += onClick;
            }

            /// <summary>
            /// An equipped row is hidden, matching GroupSelectionPanel. Locked
            /// and slots-full rows stay visible but say why they cannot be
            /// tapped - a row that silently ignores a tap is the thing this
            /// replaces.
            /// </summary>
            public void SetState(bool locked, bool equipped, bool slotsFull)
            {
                Root.EnableInClassList("workshop__row--hidden", equipped);
                Root.EnableInClassList("workshop__row--locked", locked);

                bool blocked = locked || slotsFull;

                Root.SetEnabled(!blocked);

                if (locked)
                    statusLabel.text = "LOCKED";
                else if (slotsFull)
                    statusLabel.text = "SLOTS FULL";
                else
                    statusLabel.text = "EQUIP";
            }
        }
    }
}
