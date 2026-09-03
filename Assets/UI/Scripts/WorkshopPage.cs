using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The Workshop: pick the suits and districts you bring into the draft.
    /// The UI Toolkit replacement for GroupSelectionPanel.
    ///
    /// Two panels. The loadout card on top is what you have - districts down
    /// the left, suits down the right, staggered so the two stacks read as two
    /// stacks. The picker card below is what you can take, with a big tab under
    /// each column and a two-across scrolling grid.
    ///
    /// Slot counts come from LoadoutData.NodeSlots and SuitSlots, so the slots
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
    ///     out of the grid: a slot spent on one buys nothing.
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
        /// Which grid the picker is showing.
        ///
        /// Districts is first so it is zero, which makes it both the requested
        /// default and what an older profile without the field deserialises to.
        ///
        /// Not GroupSelectionPanel's SelectionTab, even though the two hold the
        /// same two values: that enum sits in GroupSelectionPanel.cs and is
        /// deleted along with it in S5. The new stack owning its own is what
        /// keeps that deletion a one-file change.
        /// </summary>
        private enum Tab
        {
            Districts = 0,
            Suits = 1
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
        private readonly ScrollView suitGrid;
        private readonly ScrollView nodeGrid;

        private readonly List<SlotView> suitSlots = new List<SlotView>();
        private readonly List<SlotView> nodeSlots = new List<SlotView>();
        private readonly List<ItemCell> suitCells = new List<ItemCell>();
        private readonly List<ItemCell> nodeCells = new List<ItemCell>();

        private LoadoutEditor loadout = new LoadoutEditor(LoadoutData.CreateEmpty());
        private Tab activeTab = Tab.Districts;
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
            suitGrid = Root.Q<ScrollView>("workshop-list-suits");
            nodeGrid = Root.Q<ScrollView>("workshop-list-nodes");

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
                BuildCells();
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
            BuildSlotColumn(nodeSlotHost, nodeSlots, LoadoutData.NodeSlots, Tab.Districts);
            BuildSlotColumn(suitSlotHost, suitSlots, LoadoutData.SuitSlots, Tab.Suits);
        }

        private void BuildSlotColumn(VisualElement host, List<SlotView> into, int count, Tab tab)
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

        private void BuildCells()
        {
            BuildCellGrid(nodeGrid, nodeCells, Tab.Districts);
            BuildCellGrid(suitGrid, suitCells, Tab.Suits);
        }

        private void BuildCellGrid(ScrollView grid, List<ItemCell> into, Tab tab)
        {
            if (grid == null) return;

            int count = tab == Tab.Suits ? draftableSuits.Count : draftableNodes.Count;

            if (count == 0)
            {
                grid.Add(BuildEmptyGridNote(tab));
                return;
            }

            for (int i = 0; i < count; i++)
            {
                string id;
                string displayName;

                if (tab == Tab.Suits)
                {
                    SuitDefinition suit = draftableSuits[i];
                    id = suit.suitID;
                    displayName = DisplayNameOf(suit);
                }
                else
                {
                    NodeDefinition node = draftableNodes[i];
                    id = node.nodeID;
                    displayName = DisplayNameOf(node);
                }

                string equipID = id;
                ItemCell cell = new ItemCell(id, displayName, () => OnCellClicked(tab, equipID));

                into.Add(cell);
                grid.Add(cell.Root);
            }
        }

        /// <summary>
        /// What the grid says when there is nothing to show. Only reachable
        /// when the definition arrays are unassigned, which means the Editor
        /// setup has not been re-run since those fields were added - so the
        /// message names the fix.
        /// </summary>
        private static VisualElement BuildEmptyGridNote(Tab tab)
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

            activeTab = profile != null ? ToTab(profile.WorkshopTabIndex) : Tab.Districts;

            // A saved loadout can hold a suit that has since become globally
            // granted, or Crossroads. Both are slots already producing nothing;
            // clearing them hands the slots back rather than showing an item
            // the grid has no cell for.
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

        /// <summary>
        /// Anything that is not a tab we know about becomes Districts - the
        /// default - rather than throwing on a profile written by a later build.
        /// </summary>
        private static Tab ToTab(int index)
        {
            return index == (int)Tab.Suits ? Tab.Suits : Tab.Districts;
        }

        // ===== INTERACTION =====

        private void OnCellClicked(Tab tab, string itemID)
        {
            int slot = tab == Tab.Suits
                ? loadout.EquipSuit(itemID)
                : loadout.EquipNode(itemID);

            // Refused: already equipped, or no slot free. Cells in either state
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

            // An empty slot is not a dead tap: it switches the picker to the
            // grid that fills it.
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

            PlayerProfile profile = PlayerProfile.Instance;
            if (profile != null) profile.WorkshopTabIndex = (int)tab;

            RefreshAll();
        }

        // ===== REFRESH =====

        private void RefreshAll()
        {
            RefreshSlots();
            RefreshTabs();
            RefreshCells();
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

            if (suitGrid != null)
                suitGrid.EnableInClassList("workshop__grid--hidden", !suitsActive);

            if (nodeGrid != null)
                nodeGrid.EnableInClassList("workshop__grid--hidden", suitsActive);
        }

        /// <summary>
        /// Cell state is recomputed here rather than fixed at build time, so an
        /// unlock that lands while the lobby is open is picked up on the next
        /// visit. It also means a null PlayerProfile at construction does not
        /// leave every cell permanently locked.
        /// </summary>
        private void RefreshCells()
        {
            PlayerProfile profile = PlayerProfile.Instance;

            for (int i = 0; i < suitCells.Count; i++)
            {
                ItemCell cell = suitCells[i];

                bool locked = profile != null && !profile.IsSuitUnlocked(cell.ItemID);
                bool equipped = loadout.IsSuitEquipped(cell.ItemID);

                cell.SetState(locked, equipped, loadout.SuitSlotsFull);
            }

            for (int i = 0; i < nodeCells.Count; i++)
            {
                ItemCell cell = nodeCells[i];

                bool locked = profile != null && !profile.IsNodeUnlocked(cell.ItemID);
                bool equipped = loadout.IsNodeEquipped(cell.ItemID);

                cell.SetState(locked, equipped, loadout.NodeSlotsFull);
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
        /// empty cell.
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
        /// One loadout slot. Filled slots unequip on tap; empty ones switch the
        /// picker to the grid that fills them.
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
        /// One cell in the two-across picker grid: tile, name, and what tapping
        /// it will do.
        ///
        /// No description. The definitions carry one and it is worth reading,
        /// but at two cells across on a phone a two-line effect note triples the
        /// height of every cell and drops the grid to two visible rows. The
        /// place for it is a detail view, not the picker.
        /// </summary>
        private class ItemCell
        {
            public string ItemID { get; private set; }

            public Button Root { get; private set; }

            private readonly Label statusLabel;

            public ItemCell(string itemID, string displayName, System.Action onClick)
            {
                ItemID = itemID;

                Root = new Button();
                Root.AddToClassList("workshop__cell");

                ItemTile tile = new ItemTile();
                tile.SetItem(itemID, displayName);
                tile.Root.AddToClassList("tile--cell");

                Label name = new Label(displayName);
                name.AddToClassList("body");
                name.AddToClassList("workshop__cell-name");
                name.pickingMode = PickingMode.Ignore;

                statusLabel = new Label();
                statusLabel.AddToClassList("caption");
                statusLabel.AddToClassList("workshop__cell-status");
                statusLabel.pickingMode = PickingMode.Ignore;

                Root.Add(tile.Root);
                Root.Add(name);
                Root.Add(statusLabel);

                if (onClick != null) Root.clicked += onClick;
            }

            /// <summary>
            /// An equipped cell leaves the grid, matching GroupSelectionPanel.
            /// Locked and slots-full cells stay visible but say why they cannot
            /// be tapped - a cell that silently ignores a tap is the thing this
            /// replaces.
            /// </summary>
            public void SetState(bool locked, bool equipped, bool slotsFull)
            {
                Root.EnableInClassList("workshop__cell--hidden", equipped);
                Root.EnableInClassList("workshop__cell--locked", locked);

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
