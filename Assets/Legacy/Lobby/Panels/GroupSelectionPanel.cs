using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace NodeWar.Lobby
{
    public enum SelectionTab
    {
        Suits,
        Nodes
    }

    public class GroupSelectionPanel : LobbyPanel
    {
        [Header("Equipped Slots")]
        [SerializeField] private GroupSlotDisplay suitSlot0;
        [SerializeField] private GroupSlotDisplay suitSlot1;
        [SerializeField] private GroupSlotDisplay suitSlot2;
        [SerializeField] private GroupSlotDisplay nodeSlot0;
        [SerializeField] private GroupSlotDisplay nodeSlot1;

        [Header("Tabs")]
        [SerializeField] private Button suitsTabButton;
        [SerializeField] private Button nodesTabButton;
        [SerializeField] private Image suitsTabImage;
        [SerializeField] private Image nodesTabImage;

        [Header("Item Grid (single ScrollRect)")]
        [SerializeField] private ScrollRect itemScrollRect;
        [SerializeField] private RectTransform itemGridContent;

        [Header("Item Prefab")]
        [SerializeField] private GameObject selectableItemPrefab;

        [Header("Data")]
        [SerializeField] private SuitDefinition[] allSuits;
        [SerializeField] private NodeDefinition[] allNodes;

        [Header("Navigation")]
        [SerializeField] private Button backButton;

        [Header("Tab Colors")]
        [SerializeField] private Color tabActiveColor = new Color(0.25f, 0.35f, 0.55f, 1f);
        [SerializeField] private Color tabInactiveColor = new Color(0.14f, 0.14f, 0.19f, 1f);

        // Runtime state
        private SelectionTab activeTab = SelectionTab.Suits;
        private float savedSuitScrollPos = 1f;
        private float savedNodeScrollPos = 1f;

        private List<SelectableItemDisplay> suitItems = new List<SelectableItemDisplay>();
        private List<SelectableItemDisplay> nodeItems = new List<SelectableItemDisplay>();
        private bool listsBuilt = false;

        private void Awake()
        {
            suitSlot0.Initialize(OnSuitUnequipped);
            suitSlot1.Initialize(OnSuitUnequipped);
            suitSlot2.Initialize(OnSuitUnequipped);
            nodeSlot0.Initialize(OnNodeUnequipped);
            nodeSlot1.Initialize(OnNodeUnequipped);

            suitsTabButton.onClick.AddListener(() => SwitchTab(SelectionTab.Suits));
            nodesTabButton.onClick.AddListener(() => SwitchTab(SelectionTab.Nodes));

            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);
        }

        public override void OnShow()
        {
            if (!listsBuilt)
            {
                BuildAllItems();
                listsBuilt = true;
            }

            LoadFromProfile();
            SwitchTab(activeTab);
        }

        public override void OnHide()
        {
            SaveToProfile();
        }

        // ===== TAB SWITCHING =====

        private void SwitchTab(SelectionTab tab)
        {
            // Save current scroll position before switching
            if (activeTab == SelectionTab.Suits)
                savedSuitScrollPos = itemScrollRect.verticalNormalizedPosition;
            else
                savedNodeScrollPos = itemScrollRect.verticalNormalizedPosition;

            activeTab = tab;

            // Deselect all items (collapse any open USE buttons)
            DeselectAll(suitItems);
            DeselectAll(nodeItems);

            // Update tab button visuals
            suitsTabImage.color = (tab == SelectionTab.Suits) ? tabActiveColor : tabInactiveColor;
            nodesTabImage.color = (tab == SelectionTab.Nodes) ? tabActiveColor : tabInactiveColor;

            // Show/hide items based on active tab + equipped state
            RefreshListVisibility();

            // Restore scroll position for this tab
            // Must wait one frame for layout to rebuild after visibility changes
            StartCoroutine(RestoreScrollNextFrame());
        }

        private System.Collections.IEnumerator RestoreScrollNextFrame()
        {
            yield return null; // wait for layout rebuild

            if (activeTab == SelectionTab.Suits)
                itemScrollRect.verticalNormalizedPosition = savedSuitScrollPos;
            else
                itemScrollRect.verticalNormalizedPosition = savedNodeScrollPos;
        }

        // ===== BUILDING ITEMS =====

        private void BuildAllItems()
        {
            PlayerProfile profile = PlayerProfile.Instance;

            // Build suit items
            for (int i = 0; i < allSuits.Length; i++)
            {
                SuitDefinition def = allSuits[i];
                bool locked = (profile != null) ? !profile.IsSuitUnlocked(def.suitID) : true;

                GameObject go = Instantiate(selectableItemPrefab, itemGridContent);
                SelectableItemDisplay display = go.GetComponent<SelectableItemDisplay>();

                display.Initialize(def.suitID, def.displayName, def.icon, locked,
                    OnSuitUseClicked,
                    (selected) => DeselectAllExcept(suitItems, selected));

                suitItems.Add(display);
            }

            // Build node items
            for (int i = 0; i < allNodes.Length; i++)
            {
                NodeDefinition def = allNodes[i]; 
                bool locked = (profile != null) ? !profile.IsNodeUnlocked(def.nodeID) : true;

                GameObject go = Instantiate(selectableItemPrefab, itemGridContent);
                SelectableItemDisplay display = go.GetComponent<SelectableItemDisplay>();

                display.Initialize(def.nodeID, def.displayName, def.icon, locked,
                                    OnNodeUseClicked,
                                    (selected) => DeselectAllExcept(nodeItems, selected));

                nodeItems.Add(display);
            }
        }

        private void DeselectAllExcept(List<SelectableItemDisplay> items, SelectableItemDisplay except)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != except)
                    items[i].Deselect();
            }
        }

        // ===== EQUIP / UNEQUIP =====

        private void OnSuitUseClicked(string suitID)
        {
            if (suitSlot0.IsEmpty)
                EquipSuit(suitSlot0, suitID);
            else if (suitSlot1.IsEmpty)
                EquipSuit(suitSlot1, suitID);
            else if (suitSlot2.IsEmpty)
                EquipSuit(suitSlot2, suitID);

            RefreshListVisibility();
            DeselectAll(suitItems);
        }

        private void OnNodeUseClicked(string nodeID)
        {
            if (nodeSlot0.IsEmpty)
                EquipNode(nodeSlot0, nodeID);
            else if (nodeSlot1.IsEmpty)
                EquipNode(nodeSlot1, nodeID);

            RefreshListVisibility();
            DeselectAll(nodeItems);
        }

        private void OnSuitUnequipped(string suitID)
        {
            RefreshListVisibility();
        }

        private void OnNodeUnequipped(string nodeID)
        {
            RefreshListVisibility();
        }

        private void EquipSuit(GroupSlotDisplay slot, string suitID)
        {
            SuitDefinition def = FindSuit(suitID);
            if (def == null) return;
            slot.SetItem(def.suitID, def.displayName, def.icon);
        }

        private void EquipNode(GroupSlotDisplay slot, string nodeID)
        {
            NodeDefinition def = FindNode(nodeID);
            if (def == null) return;
            slot.SetItem(def.nodeID, def.displayName, def.icon);
        }

        // ===== VISIBILITY =====

        private void RefreshListVisibility()
        {
            string suitEquipped0 = suitSlot0.EquippedID;
            string suitEquipped1 = suitSlot1.EquippedID;
            string suitEquipped2 = suitSlot2.EquippedID;

            for (int i = 0; i < suitItems.Count; i++)
            {
                string id = suitItems[i].ItemID;
                bool isEquipped = (id == suitEquipped0 || id == suitEquipped1 || id == suitEquipped2);
                bool isActiveTab = (activeTab == SelectionTab.Suits);
                suitItems[i].SetVisible(isActiveTab && !isEquipped);
            }

            string nodeEquipped0 = nodeSlot0.EquippedID;
            string nodeEquipped1 = nodeSlot1.EquippedID;

            for (int i = 0; i < nodeItems.Count; i++)
            {
                string id = nodeItems[i].ItemID;
                bool isEquipped = (id == nodeEquipped0 || id == nodeEquipped1);
                bool isActiveTab = (activeTab == SelectionTab.Nodes);
                nodeItems[i].SetVisible(isActiveTab && !isEquipped);
            }
        }

        private void DeselectAll(List<SelectableItemDisplay> items)
        {
            for (int i = 0; i < items.Count; i++)
                items[i].Deselect();
        }

        // ===== PERSISTENCE =====

        private void LoadFromProfile()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            if (profile == null) return;

            LoadoutData loadout = LoadoutData.Normalized(profile.Loadout);

            GroupSlotDisplay[] suitSlots = SuitSlots;
            GroupSlotDisplay[] nodeSlots = NodeSlots;

            for (int i = 0; i < suitSlots.Length; i++) suitSlots[i].SetEmpty();
            for (int i = 0; i < nodeSlots.Length; i++) nodeSlots[i].SetEmpty();

            int suitCount = Mathf.Min(suitSlots.Length, loadout.suitIDs.Length);
            for (int i = 0; i < suitCount; i++)
            {
                if (!string.IsNullOrEmpty(loadout.suitIDs[i]))
                    EquipSuit(suitSlots[i], loadout.suitIDs[i]);
            }

            int nodeCount = Mathf.Min(nodeSlots.Length, loadout.nodeIDs.Length);
            for (int i = 0; i < nodeCount; i++)
            {
                if (!string.IsNullOrEmpty(loadout.nodeIDs[i]))
                    EquipNode(nodeSlots[i], loadout.nodeIDs[i]);
            }
        }

        private void SaveToProfile()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            if (profile == null) return;

            GroupSlotDisplay[] suitSlots = SuitSlots;
            GroupSlotDisplay[] nodeSlots = NodeSlots;

            LoadoutData loadout = new LoadoutData
            {
                suitIDs = new string[suitSlots.Length],
                nodeIDs = new string[nodeSlots.Length]
            };

            for (int i = 0; i < suitSlots.Length; i++)
                loadout.suitIDs[i] = suitSlots[i].EquippedID ?? "";
            for (int i = 0; i < nodeSlots.Length; i++)
                loadout.nodeIDs[i] = nodeSlots[i].EquippedID ?? "";

            // SetLoadout normalizes, so a mismatch between the number of slot
            // displays wired in the scene and LoadoutData's slot counts is
            // reconciled there rather than persisted.
            profile.SetLoadout(loadout);
        }

        /// <summary>
        /// The serialized slot fields as an array. The displays stay individual
        /// [SerializeField]s because they are wired in the scene one by one;
        /// this is just the indexed view the loadout code wants.
        /// </summary>
        private GroupSlotDisplay[] SuitSlots
        {
            get { return new GroupSlotDisplay[] { suitSlot0, suitSlot1, suitSlot2 }; }
        }

        private GroupSlotDisplay[] NodeSlots
        {
            get { return new GroupSlotDisplay[] { nodeSlot0, nodeSlot1 }; }
        }

        // ===== LOOKUP =====

        private SuitDefinition FindSuit(string suitID)
        {
            for (int i = 0; i < allSuits.Length; i++)
            {
                if (allSuits[i].suitID == suitID) return allSuits[i];
            }
            return null;
        }

        private NodeDefinition FindNode(string nodeID)
        {
            for (int i = 0; i < allNodes.Length; i++)
            {
                if (allNodes[i].nodeID == nodeID) return allNodes[i];
            }
            return null;
        }

        private void OnBackClicked()
        {
            SaveToProfile();
            lobbyManager.ShowPanel(PanelType.Homepage);
        }

        private void OnDestroy()
        {
            suitsTabButton.onClick.RemoveAllListeners();
            nodesTabButton.onClick.RemoveAllListeners();
            if (backButton != null) backButton.onClick.RemoveAllListeners();
        }
    }
}