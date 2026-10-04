using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The Workshop: pick the districts and suits you bring into the draft.
    /// The lobby-prototype.html layout, with inventory choices below the slots.
    ///
    /// One side at a time. A segmented pill at the top and a floating round
    /// button bottom-right choose Districts or Suits; both call
    /// <see cref="SetTab"/>, which owns the state and re-syncs both, so the
    /// two controls can never disagree. The slots for that side sit under the
    /// pill, "Ready" appears once they are all full, and a three-across grid of
    /// cards scrolls beneath. Tap a card to take it, tap a filled slot to
    /// give it back.
    ///
    /// The rules are unchanged from the previous Workshop, and still not
    /// decided here:
    ///   - slot counts come from LoadoutData.NodeSlots and SuitSlots, and what
    ///     may occupy a slot lives in LoadoutEditor, which has no UnityEngine
    ///     reference and is covered by dotnet/NodeWar.Lobby.Tests;
    ///   - every change is saved at once through PlayerProfile.SetLoadout - a
    ///     phone can be killed at any moment, and there is no back to rely on;
    ///   - the chosen side is remembered on PlayerProfile.WorkshopTabIndex,
    ///     because the Lobby scene is rebuilt after every match;
    ///   - locked cards ask PlayerProfile.IsSuitUnlocked / IsNodeUnlocked, even
    ///     though both return true today, so this is right when they are real.
    ///
    /// Two facts about the game shape the grid (docs/ui-migration-inventory.md):
    ///   - suits flagged isGlobal (Warrior) are granted to everyone by
    ///     GameManager.BuildDraftedSuits. They are shown as cards, in the
    ///     prototype's grid, but say "Always granted" and refuse a slot, which
    ///     would buy nothing;
    ///   - Crossroads is excluded: GameManager cannot map it to a DistrictType,
    ///     so a slot spent on it produces nothing.
    ///
    /// No art exists, so a card's art is its family colour with a monogram.
    /// </summary>
    public class WorkshopPage : LobbyPage
    {
        /// <summary>
        /// Which side is shown. Districts is zero, so it is both the default
        /// and what an older profile without the field deserialises to.
        /// </summary>
        private enum Tab
        {
            Districts = 0,
            Suits = 1
        }

        private const int GridColumns = 3;

        /// <summary>One item as a card: a district or a suit.</summary>
        private class Item
        {
            public string ID;
            public string Name;
            public string Description;
            public string Note;
            public ItemFamily.Family Family;
            public bool Granted;
        }

        private readonly LoadoutCatalog catalog;
        private readonly LobbyToast toast;
        private readonly LobbyContextMenu menu;

        private readonly List<Item> districts = new List<Item>();
        private readonly List<Item> suits = new List<Item>();

        private readonly Button segDistricts;
        private readonly Button segSuits;
        private readonly VisualElement slotWrap;
        private readonly VisualElement slotHost;
        private readonly ScrollView grid;
        private readonly VisualElement veil;
        private readonly VisualElement picker;
        private readonly LobbyIcon pickIcon;
        private readonly Label pickLabel;
        private readonly Button pickDistricts;
        private readonly Button pickSuits;
        private readonly Label details;
        private readonly VisualElement eraRow;
        private readonly VisualElement skinRow;
        private readonly Label eraMessage;

        private PlayerState playerState;
        private Item inspectedItem;
        private bool isOpen;
        private int inventoryVisit;
        private int selectionVisit;
        private bool inventoryBusy;
        private string inventoryMessage = "";
        private Task inventoryIdle = Task.CompletedTask;

        private LoadoutEditor loadout = new LoadoutEditor(LoadoutData.CreateEmpty());
        private Tab activeTab = Tab.Districts;
        private bool pickerOpen;

        public WorkshopPage(VisualTreeAsset layout, LoadoutCatalog catalog, LobbyToast toast, LobbyContextMenu menu)
            : base(LobbyPageID.Workshop, Build(layout))
        {
            this.catalog = catalog != null ? catalog : new LoadoutCatalog(null, null);
            this.toast = toast;
            this.menu = menu;

            segDistricts = Root.Q<Button>("workshop-seg-districts");
            segSuits = Root.Q<Button>("workshop-seg-suits");
            slotWrap = Root.Q<VisualElement>("workshop-slotwrap");
            slotHost = Root.Q<VisualElement>("workshop-slots");
            grid = Root.Q<ScrollView>("workshop-grid");
            veil = Root.Q<VisualElement>("workshop-veil");
            picker = Root.Q<VisualElement>("workshop-picker");
            pickIcon = Root.Q<LobbyIcon>("workshop-pick-icon");
            pickLabel = Root.Q<Label>("workshop-pick-label");
            pickDistricts = Root.Q<Button>("workshop-pick-districts");
            pickSuits = Root.Q<Button>("workshop-pick-suits");
            details = Root.Q<Label>("workshop-details");
            eraRow = Root.Q<VisualElement>("workshop-era-row");
            skinRow = Root.Q<VisualElement>("workshop-skin-row");
            eraMessage = Root.Q<Label>("workshop-era-message");
            Root.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target == Root) CloseInventory();
            });

            if (segDistricts != null) segDistricts.clicked += () => SetTab(Tab.Districts);
            if (segSuits != null) segSuits.clicked += () => SetTab(Tab.Suits);
            if (pickDistricts != null) pickDistricts.clicked += () => SetTab(Tab.Districts);
            if (pickSuits != null) pickSuits.clicked += () => SetTab(Tab.Suits);

            Button pickButton = Root.Q<Button>("workshop-pickbtn");
            if (pickButton != null) pickButton.clicked += () => SetPickerOpen(!pickerOpen);

            if (veil != null)
            {
                veil.RegisterCallback<PointerDownEvent>(evt =>
                {
                    SetPickerOpen(false);
                    evt.StopPropagation();
                });
            }

            CollectItems();
            SetPickerOpen(false);
        }

        private static VisualElement Build(VisualTreeAsset layout)
        {
            VisualElement root = new VisualElement();
            root.name = "page-workshop";
            root.AddToClassList("lb-page-flush");
            root.pickingMode = PickingMode.Ignore;

            if (layout != null)
            {
                layout.CloneTree(root);
            }
            else
            {
                Label note = new Label("Workshop layout missing - assign WorkshopPage.uxml");
                note.AddToClassList("lb-stub__body");
                root.Add(note);
            }

            return root;
        }

        public override void OnShow()
        {
            isOpen = true;
            int visit = ++inventoryVisit;
            playerState = null;
            LoadFromProfile();
            Inspect(null);
            Render();
            _ = LoadInventoryAsync(visit);
        }

        /// <summary>
        /// A second save on the way out. Every change already persists as it
        /// happens; this only covers SetLoadout being made deferred later.
        /// </summary>
        public override void OnHide()
        {
            CloseInventory();
            SetPickerOpen(false);
            SaveToProfile();
        }

        // ===== DEFINITIONS =====

        /// <summary>
        /// Turns the definition assets into cards, once - they are serialized
        /// fields and cannot change at runtime. Districts are ordered by family
        /// (round, square, triangle, as the prototype lists them), keeping the
        /// assets' own order within a family.
        /// </summary>
        private void CollectItems()
        {
            // The catalog never hands back a null array.
            NodeDefinition[] allNodes = catalog.Nodes;
            for (int i = 0; i < allNodes.Length; i++)
            {
                NodeDefinition node = allNodes[i];
                if (node == null || string.IsNullOrEmpty(node.nodeID)) continue;
                if (!catalog.IsNodeOffered(node.nodeID)) continue;

                ItemFamily.Family family = ItemFamily.ForNode(node.nodeID);
                districts.Add(new Item
                {
                    ID = node.nodeID,
                    Name = catalog.NodeName(node.nodeID),
                    Description = node.description,
                    Note = ItemFamily.NoteFor(family),
                    Family = family
                });
            }

            SortByFamily(districts);

            SuitDefinition[] allSuits = catalog.Suits;
            for (int i = 0; i < allSuits.Length; i++)
            {
                SuitDefinition suit = allSuits[i];
                if (suit == null || string.IsNullOrEmpty(suit.suitID)) continue;

                suits.Add(new Item
                {
                    ID = suit.suitID,
                    Name = catalog.SuitName(suit.suitID),
                    Description = suit.description,
                    Note = suit.isGlobal ? "Always granted" : suit.description,
                    Family = ItemFamily.Family.Combat,
                    Granted = suit.isGlobal
                });
            }
        }

        /// <summary>Stable insertion sort: the lists are a handful of items.</summary>
        private static void SortByFamily(List<Item> items)
        {
            for (int i = 1; i < items.Count; i++)
            {
                Item current = items[i];
                int key = ItemFamily.SortKey(current.Family);
                int j = i - 1;

                while (j >= 0 && ItemFamily.SortKey(items[j].Family) > key)
                {
                    items[j + 1] = items[j];
                    j--;
                }

                items[j + 1] = current;
            }
        }

        private static Item Find(List<Item> items, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < items.Count; i++)
                if (items[i].ID == id) return items[i];
            return null;
        }

        // ===== PROFILE =====

        private void LoadFromProfile()
        {
            PlayerProfile profile = PlayerProfile.Instance;

            loadout = new LoadoutEditor(profile != null ? profile.Loadout : LoadoutData.CreateEmpty());
            activeTab = profile != null && profile.WorkshopTabIndex == (int)Tab.Suits ? Tab.Suits : Tab.Districts;

            // A saved loadout can hold a suit that has since become globally
            // granted, or Crossroads. Both are slots producing nothing; clearing
            // them hands the slots back.
            int cleared = loadout.DropUnavailable(catalog.IsSuitOffered, catalog.IsNodeOffered);

            if (cleared > 0)
            {
                Debug.Log("[Workshop] Cleared " + cleared + " loadout entr" +
                          (cleared == 1 ? "y" : "ies") + " that the draft cannot use.");
                SaveToProfile();
            }
        }

        private void SaveToProfile()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            if (profile != null) profile.SetLoadout(loadout.ToLoadout());
        }

        // ===== STATE =====

        /// <summary>
        /// The one owner of which side is shown. Both switchers call this, and
        /// it re-renders both, the slots and the grid, then closes the picker.
        /// </summary>
        private void SetTab(Tab tab)
        {
            if (tab != activeTab)
            {
                activeTab = tab;
                Inspect(null);

                PlayerProfile profile = PlayerProfile.Instance;
                if (profile != null) profile.WorkshopTabIndex = (int)tab;

                Render();
                if (grid != null) grid.scrollOffset = Vector2.zero;
            }

            SetPickerOpen(false);
        }

        private void SetPickerOpen(bool open)
        {
            pickerOpen = open;

            if (picker != null) picker.EnableInClassList("lb-picker--open", open);

            if (veil != null)
            {
                veil.EnableInClassList("lb-pickveil--on", open);
                veil.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
            }

            // The options fade out rather than vanish, so they must also stop
            // taking taps while closed.
            SetPickable(pickDistricts, open);
            SetPickable(pickSuits, open);
        }

        private static void SetPickable(VisualElement element, bool pickable)
        {
            if (element != null) element.pickingMode = pickable ? PickingMode.Position : PickingMode.Ignore;
        }

        // ===== RENDER =====

        private void Render()
        {
            RenderSwitchers();
            RenderSlots();
            RenderGrid();
        }

        private void RenderSwitchers()
        {
            bool suitsOn = activeTab == Tab.Suits;

            if (segDistricts != null) segDistricts.EnableInClassList("lb-wsseg__btn--on", !suitsOn);
            if (segSuits != null) segSuits.EnableInClassList("lb-wsseg__btn--on", suitsOn);
            if (pickDistricts != null) pickDistricts.EnableInClassList("lb-pickopt--on", !suitsOn);
            if (pickSuits != null) pickSuits.EnableInClassList("lb-pickopt--on", suitsOn);

            // The floating button's face always shows the current side.
            if (pickIcon != null) pickIcon.Kind = suitsOn ? LobbyIconKind.Suit : LobbyIconKind.District;
            if (pickLabel != null) pickLabel.text = suitsOn ? "SUITS" : "DIST";
        }

        private void RenderSlots()
        {
            if (slotHost == null) return;

            bool suitsOn = activeTab == Tab.Suits;
            int count = suitsOn ? loadout.SuitSlotCount : loadout.NodeSlotCount;

            slotHost.Clear();

            bool allFull = count > 0;

            for (int i = 0; i < count; i++)
            {
                string id = suitsOn ? loadout.SuitAt(i) : loadout.NodeAt(i);
                Item item = Find(suitsOn ? suits : districts, id);
                bool filled = !string.IsNullOrEmpty(id);
                if (!filled) allFull = false;

                int slot = i;
                Button button = new Button();
                button.AddToClassList("ui-reset-button");
                button.AddToClassList("lb-slot");
                if (i == count - 1) button.AddToClassList("lb-slot--last");
                button.EnableInClassList("lb-slot--filled", filled);

                VisualElement ring = new VisualElement();
                ring.AddToClassList("lb-slot__ring");
                ring.pickingMode = PickingMode.Ignore;
                button.Add(ring);

                if (filled)
                {
                    string name = item != null ? item.Name : id;
                    button.Add(MakeLabel(ItemTint.MonogramFor(name, id), "lb-slot__letter", "ui-w600"));
                    button.Add(MakeLabel(name, "lb-slot__text", "ui-w600"));
                }
                else
                {
                    button.Add(MakeLabel("Slot\n" + (i + 1), "lb-slot__text", "ui-w500"));
                }

                button.clicked += () => OnSlotClicked(slot);

                slotHost.Add(button);
            }

            if (slotWrap != null) slotWrap.EnableInClassList("lb-slotwrap--ready", allFull);
        }

        private void RenderGrid()
        {
            if (grid == null) return;

            List<Item> items = activeTab == Tab.Suits ? suits : districts;

            grid.Clear();

            if (items.Count == 0)
            {
                // Only reachable when the definition arrays are unassigned, which
                // means the Editor setup has not been re-run - so name the fix.
                Label note = new Label("No " + (activeTab == Tab.Suits ? "suit" : "district") +
                                       " definitions assigned.\nRun Tools > Node War > Set Up UI Toolkit Lobby.");
                note.AddToClassList("lb-stub__body");
                grid.Add(note);
                return;
            }

            PlayerProfile profile = PlayerProfile.Instance;

            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];

                bool locked = IsLocked(profile, item);
                bool used = activeTab == Tab.Suits ? loadout.IsSuitEquipped(item.ID) : loadout.IsNodeEquipped(item.ID);

                VisualElement wrap = new VisualElement();
                wrap.AddToClassList("ui-sticker");
                wrap.AddToClassList("lb-gcard-wrap");
                if (i % GridColumns != GridColumns - 1) wrap.AddToClassList("lb-gcard-wrap--gap");
                wrap.EnableInClassList("lb-gcard-wrap--used", used);
                wrap.EnableInClassList("lb-gcard-wrap--locked", locked);
                wrap.pickingMode = PickingMode.Ignore;

                VisualElement shadow = new VisualElement();
                shadow.AddToClassList("ui-sticker__shadow");
                shadow.pickingMode = PickingMode.Ignore;
                wrap.Add(shadow);

                Button card = new Button();
                card.AddToClassList("ui-reset-button");
                card.AddToClassList("lb-gcard");

                VisualElement art = new VisualElement();
                art.AddToClassList("lb-gcard__art");
                art.AddToClassList(ItemFamily.ClassFor(item.Family));
                art.pickingMode = PickingMode.Ignore;
                if (locked)
                    art.Add(new LobbyIcon(LobbyIconKind.Lock));
                else
                    art.Add(MakeLabel(ItemTint.MonogramFor(item.Name, item.ID), "lb-gcard__letter", "ui-w600"));
                card.Add(art);

                card.Add(MakeLabel(item.Name, "lb-gcard__name", "ui-w600"));
                card.Add(MakeLabel(item.Note, "lb-gcard__note", "ui-w500"));

                card.clicked += () => OnCardClicked(item);

                if (menu != null)
                {
                    menu.Attach(card, () => new List<LobbyMenuItem>
                    {
                        new LobbyMenuItem("View details", () => Inspect(item)),
                        new LobbyMenuItem("Add to loadout", () => OnCardClicked(item)),
                        new LobbyMenuItem("Compare", () => Say("Comparing arrives in a later update")),
                    });
                }

                wrap.Add(card);
                grid.Add(wrap);
            }
        }

        private bool IsLocked(PlayerProfile profile, Item item)
        {
            if (profile == null || item.Granted) return false;

            return activeTab == Tab.Suits
                ? !profile.IsSuitUnlocked(item.ID)
                : !profile.IsNodeUnlocked(item.ID);
        }

        // ===== INTERACTION =====

        /// <summary>
        /// Tap a card to take the first free slot. Every refusal says why - the
        /// cards stay tappable in every state so none of them is silently dead.
        /// </summary>
        private void OnCardClicked(Item item)
        {
            Inspect(item);
            bool suitsOn = activeTab == Tab.Suits;

            if (item.Granted)
            {
                Say(item.Name + " is always granted. It costs no slot");
                return;
            }

            if (IsLocked(PlayerProfile.Instance, item))
            {
                Say(item.Name + " is locked");
                return;
            }

            bool equipped = suitsOn ? loadout.IsSuitEquipped(item.ID) : loadout.IsNodeEquipped(item.ID);
            if (equipped)
            {
                Say("Already in your loadout");
                return;
            }

            int slot = suitsOn ? loadout.EquipSuit(item.ID) : loadout.EquipNode(item.ID);
            if (slot == LoadoutEditor.NoSlot)
            {
                Say("Loadout full. Tap a slot to clear it");
                return;
            }

            SaveToProfile();
            Render();
        }

        /// <summary>A filled slot gives its item back; an empty one says how to fill it.</summary>
        private void OnSlotClicked(int slot)
        {
            string removed = activeTab == Tab.Suits ? loadout.ClearSuitSlot(slot) : loadout.ClearNodeSlot(slot);

            if (string.IsNullOrEmpty(removed))
            {
                Say("Tap a card below to fill this slot");
                return;
            }

            Inspect(Find(activeTab == Tab.Suits ? suits : districts, removed));
            SaveToProfile();
            Render();
        }

        private void CloseInventory()
        {
            isOpen = false;
            inventoryVisit++;
        }

        private bool IsInventoryActive(int visit) => isOpen && inventoryVisit == visit;

        private void Inspect(Item item)
        {
            if (inspectedItem != item)
            {
                selectionVisit++;
                inspectedItem = item;
                if (!inventoryBusy && playerState != null) inventoryMessage = "";
            }
            RenderInventory();
        }

        private async Task LoadInventoryAsync(int visit)
        {
            inventoryBusy = true;
            inventoryMessage = "Loading inventory...";
            RenderInventory();
            try
            {
                // A reopened page must not fetch a snapshot before an earlier save finishes.
                await inventoryIdle;
                if (!IsInventoryActive(visit)) return;
                PlayerState result = await BackendServices.PlayerState.GetAsync();
                if (!IsInventoryActive(visit)) return;
                if (result == null) throw new InvalidOperationException("Missing player state");
                playerState = result;
                inventoryMessage = "";
            }
            catch (Exception)
            {
                if (IsInventoryActive(visit)) inventoryMessage = "Couldn't load inventory. Loadout editing is still available.";
            }
            finally
            {
                if (IsInventoryActive(visit))
                {
                    inventoryBusy = false;
                    RenderInventory();
                }
            }
        }

        private async Task EquipInventoryAsync(string baseId, string id, bool skin)
        {
            if (!isOpen || inventoryBusy || inspectedItem == null ||
                LoadoutTypes.CatalogBaseForLobbyId(inspectedItem.ID) != baseId) return;
            EraChips.Chip[] chips = skin ? EraChips.SkinsForItem(playerState, baseId) : EraChips.ForItem(playerState, baseId);
            if (!Array.Exists(chips, chip => chip.ID == id && chip.Selectable)) return;

            int visit = inventoryVisit;
            int selection = selectionVisit;
            var completion = new TaskCompletionSource<bool>();
            inventoryIdle = completion.Task;
            inventoryBusy = true;
            inventoryMessage = "Saving...";
            RenderInventory();
            try
            {
                var changes = new EquippedRecord();
                var map = new Dictionary<string, string> { [baseId] = id };
                if (skin) changes.Skins = map;
                else changes.Variants = map;
                PlayerState result = await BackendServices.Inventory.EquipAsync(changes);
                if (!IsInventoryActive(visit) || selectionVisit != selection) return;
                if (result == null) throw new InvalidOperationException("Missing player state");
                playerState = result;
                inventoryMessage = "";
            }
            catch (Exception)
            {
                if (IsInventoryActive(visit) && selectionVisit == selection)
                    inventoryMessage = "Couldn't equip. Please try again.";
            }
            finally
            {
                completion.TrySetResult(true);
                if (IsInventoryActive(visit))
                {
                    inventoryBusy = false;
                    if (selectionVisit != selection)
                    {
                        // Discard the old selection's response and get a fresh snapshot.
                        playerState = null;
                        _ = LoadInventoryAsync(visit);
                    }
                    else RenderInventory();
                }
            }
        }

        private void RenderInventory()
        {
            if (details != null) details.text = inspectedItem == null
                ? "Select a card to inspect its eras" : DetailsOf(inspectedItem);
            string baseId = inspectedItem == null ? null : LoadoutTypes.CatalogBaseForLobbyId(inspectedItem.ID);
            RenderChips(eraRow, EraChips.ForItem(playerState, baseId), baseId, false);
            RenderChips(skinRow, EraChips.SkinsForItem(playerState, baseId), baseId, true);
            if (eraMessage != null)
            {
                eraMessage.text = inventoryMessage;
                eraMessage.style.display = string.IsNullOrEmpty(inventoryMessage) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        private void RenderChips(VisualElement row, EraChips.Chip[] chips, string baseId, bool skin)
        {
            if (row == null) return;
            row.Clear();
            row.style.display = chips.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            row.SetEnabled(!inventoryBusy);
            foreach (EraChips.Chip chip in chips)
            {
                string label = skin ? "Skin: " + chip.ID.Substring(chip.ID.LastIndexOf('.') + 1) : "Era " + chip.Era;
                if (chip.Equipped) label += "\nEquipped";
                if (!skin && !chip.Owned) label += "\nArena " + chip.Era;
                else if (!skin && !chip.Usable) label += "\nNeeds arena " + chip.Era;
                Button button = new Button { text = label };
                button.AddToClassList("ui-reset-button");
                button.AddToClassList("lb-era-chip");
                button.EnableInClassList("lb-era-chip--locked", !chip.Owned || !chip.Usable);
                button.EnableInClassList("lb-era-chip--equipped", chip.Equipped);
                button.SetEnabled(chip.Selectable);
                button.clicked += async () => await EquipInventoryAsync(baseId, chip.ID, skin);
                row.Add(button);
            }
        }

        private static string DetailsOf(Item item)
        {
            return string.IsNullOrEmpty(item.Description) ? item.Name : item.Name + ": " + item.Description;
        }

        private void Say(string message)
        {
            if (toast != null) toast.Show(message);
        }

        private static Label MakeLabel(string text, string styleClass, string weightClass)
        {
            Label label = new Label(text);
            label.AddToClassList(styleClass);
            label.AddToClassList(weightClass);
            label.pickingMode = PickingMode.Ignore;
            return label;
        }
    }
}
