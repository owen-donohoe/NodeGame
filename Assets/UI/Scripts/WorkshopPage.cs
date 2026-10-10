using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodeWar.Backend;
using NodeWar.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>Chosen slots with passive inspection and explicit Use.</summary>
    public class WorkshopPage : LobbyPage
    {
        private enum Tab { Districts = 0, Suits = 1 }
        private class Item
        {
            public string ID, Name, Description, Note;
            public ItemFamily.Family Family;
            public bool Granted;
        }
        private readonly LoadoutCatalog catalog;
        private readonly LobbyToast toast;
        private readonly LobbyContextMenu menu;
        private readonly List<Item> districts = new List<Item>(), suits = new List<Item>();
        private readonly Button segDistricts, segSuits, primary, variants;
        private readonly VisualElement districtSlots, suitSlots, unlocked, notUnlocked, preview, stats;
        private readonly Label title, description, notice, eraMessage;
        private readonly ScrollView grid;
        private PlayerState playerState;
        private Item inspectedItem;
        private bool isOpen, inventoryBusy;
        private int inventoryVisit, selectionVisit;
        private string inventoryMessage = "";
        private Task inventoryIdle = Task.CompletedTask;
        private LoadoutScreenModel loadout = new LoadoutScreenModel(LoadoutData.CreateEmpty());
        private Tab activeTab;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private UnlockStatus? debugUnlockStatus;
        public void SetDebugUnlockStatus(UnlockStatus? status) { debugUnlockStatus = status; Render(); RenderInventory(); }
#endif
        private UnlockState DisplayUnlock(UnlockState state)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugUnlockStatus.HasValue) return state.WithDisplayStatus(debugUnlockStatus.Value);
#endif
            return state;
        }
        public WorkshopPage(VisualTreeAsset layout, LoadoutCatalog catalog, LobbyToast toast, LobbyContextMenu menu, LobbySheet sheet = null)
            : base(LobbyPageID.Workshop, Build(layout))
        {
            this.catalog = catalog != null ? catalog : new LoadoutCatalog(null, null);
            this.toast = toast; this.menu = menu;
            segDistricts = Root.Q<Button>("workshop-tab-districts");
            segSuits = Root.Q<Button>("workshop-tab-suits");
            primary = Root.Q<Button>("workshop-info-use");
            variants = Root.Q<Button>("workshop-open-variants");
            districtSlots = Root.Q<VisualElement>("workshop-district-slots");
            suitSlots = Root.Q<VisualElement>("workshop-suit-slots");
            unlocked = Root.Q<VisualElement>("workshop-unlocked");
            notUnlocked = Root.Q<VisualElement>("workshop-not-unlocked");
            preview = Root.Q<VisualElement>("workshop-info-preview");
            stats = Root.Q<VisualElement>("workshop-info-stats");
            title = Root.Q<Label>("workshop-info-title");
            description = Root.Q<Label>("workshop-info-description");
            notice = Root.Q<Label>("workshop-info-unlock");
            eraMessage = Root.Q<Label>("workshop-era-message");
            grid = Root.Q<ScrollView>("workshop-grid");
            if (segDistricts != null) segDistricts.clicked += () => SetTab(Tab.Districts);
            if (segSuits != null) segSuits.clicked += () => SetTab(Tab.Suits);
            if (primary != null) primary.clicked += UsePreview;
            // Variants and Skins intentionally remain no-ops until P1.6/P1.8.
            Root.RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == Root) CloseInventory(); });
            CollectItems();
        }
        private static VisualElement Build(VisualTreeAsset layout)
        {
            var root = new VisualElement { name = "page-workshop", pickingMode = PickingMode.Ignore };
            root.AddToClassList("lb-page-flush");
            if (layout != null) layout.CloneTree(root);
            else root.Add(new Label("Workshop layout missing - assign WorkshopPage.uxml"));
            return root;
        }
        public override void OnShow()
        {
            isOpen = true;
            int visit = ++inventoryVisit;
            playerState = BackendServices.LastKnownState;
            CollectItems(); LoadFromProfile(); Inspect(null); Render();
            _ = LoadInventoryAsync(visit);
        }
        public override void OnHide() { CloseInventory(); }
        private void CollectItems()
        {
            districts.Clear();
            suits.Clear();
            int arena = LoadoutRules.ArenaOrFallback(playerState?.Rank);
            // The catalog never hands back a null array.
            DistrictDefinition[] allDistricts = catalog.Districts;
            for (int i = 0; i < allDistricts.Length; i++)
            {
                DistrictDefinition node = allDistricts[i];
                if (node == null || string.IsNullOrEmpty(node.districtID)) continue;
                if (!catalog.IsDistrictOffered(node.districtID, 2)) continue;

                ItemFamily.Family family = ItemFamily.ForDistrict(node.districtID);
                districts.Add(new Item
                {
                    ID = node.districtID,
                    Name = catalog.DistrictName(node.districtID),
                    Description = node.description,
                    Note = ItemFamily.NoteFor(family),
                    Family = family,
                    Granted = !catalog.IsDistrictOffered(node.districtID, arena)
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
                    Note = !catalog.IsSuitOffered(suit.suitID, arena) ? "Always granted" : suit.description,
                    Family = ItemFamily.Family.Combat,
                    Granted = !catalog.IsSuitOffered(suit.suitID, arena)
                });
            }
        }

        private void SortByFamily(List<Item> items)
        {
            items.Sort((a, b) =>
            {
                int order = DisplayUnlock(catalog.UnlockForBase(a.ID, playerState)).Status.CompareTo(
                    DisplayUnlock(catalog.UnlockForBase(b.ID, playerState)).Status);
                if (order == 0) order = ItemFamily.SortKey(a.Family).CompareTo(ItemFamily.SortKey(b.Family));
                if (order == 0) order = StringComparer.Ordinal.Compare(a.Name, b.Name);
                return order != 0 ? order : StringComparer.Ordinal.Compare(a.ID, b.ID);
            });
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

            string preview = loadout.PreviewId;
            loadout = new LoadoutScreenModel(catalog.CurrentLoadout(playerState).ToLoadout());
            loadout.Preview(preview);
            activeTab = profile != null && profile.WorkshopTabIndex == (int)Tab.Suits ? Tab.Suits : Tab.Districts;
            // Until rank is confirmed, fallback display must not trim a saved high deck.
            if (playerState?.Rank != null) SaveToProfile();
        }
        private void SaveToProfile()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            if (profile != null) profile.SetLoadout(loadout.ToData());
        }

        private void SetTab(Tab tab)
        {
            activeTab = tab;
            var profile = PlayerProfile.Instance;
            if (profile != null) profile.WorkshopTabIndex = (int)tab;
            Render();
            if (grid != null) grid.scrollOffset = Vector2.zero;
        }
        private void Render()
        {
            segDistricts?.EnableInClassList("ws-tab--on", activeTab == Tab.Districts);
            segSuits?.EnableInClassList("ws-tab--on", activeTab == Tab.Suits);
            var items = activeTab == Tab.Suits ? suits : districts;
            SortByFamily(items);
            Item item = Find(items, loadout.PreviewId);
            Inspect(item ?? (items.Count > 0 ? items[0] : null));
            RenderSlots(); RenderCards(); RenderInfo();
        }
        private void RenderSlots() { RenderSlotRow(districtSlots, false); RenderSlotRow(suitSlots, true); }
        private void RenderSlotRow(VisualElement host, bool isSuit)
        {
            if (host == null) return;
            host.Clear();
            int arena = LoadoutRules.ArenaOrFallback(playerState?.Rank);
            string[] locks = isSuit ? LoadoutRules.LockedSuits(arena) : LoadoutRules.LockedDistricts(arena);
            var data = loadout.ToData();
            string[] chosen = isSuit ? data.suitIDs : data.districtIDs;
            int count = isSuit ? LoadoutRules.DisplaySuitSlots : LoadoutRules.DisplayDistrictSlots;
            for (int i = 0; i < count; i++)
            {
                bool locked = i < locks.Length;
                int slot = i - locks.Length;
                string id = locked ? locks[i] : chosen[slot];
                Item item = Find(isSuit ? suits : districts, id);
                var button = new Button();
                button.AddToClassList("ui-reset-button"); button.AddToClassList("ws-slot");
                button.Add(MakeArt(item, locked, false));
                button.Add(MakeLabel(string.IsNullOrEmpty(id) ? "+" : item?.Name ?? id, "ws-name", "ui-w600"));
                if (locked)
                {
                    button.Add(new LobbyIcon(LobbyIconKind.Lock, LobbyIconContext.Workshop));
                    button.SetEnabled(false);
                }
                else button.clicked += () => { if (loadout.ClearChosen(isSuit, slot)) { SaveToProfile(); Render(); } };
                host.Add(button);
            }
        }
        private VisualElement MakeArt(Item item, bool neutral, bool navy)
        {
            var clip = new VisualElement { pickingMode = PickingMode.Ignore };
            clip.AddToClassList("ws-art-clip");
            var art = new VisualElement { pickingMode = PickingMode.Ignore };
            art.AddToClassList("ws-art");
            if (item != null) art.AddToClassList(ItemFamily.ClassFor(item.Family));
            art.EnableInClassList("ws-art--neutral", neutral); art.EnableInClassList("ws-art--navy", navy);
            art.Add(MakeLabel(item == null ? "+" : ItemTint.MonogramFor(item.Name, item.ID), "ws-monogram", "ui-w600"));
            clip.Add(art); return clip;
        }
        private void RenderCards()
        {
            if (unlocked == null || notUnlocked == null) return;
            unlocked.Clear(); notUnlocked.Clear();
            var items = activeTab == Tab.Suits ? suits : districts;
            SortByFamily(items);
            foreach (Item item in items)
            {
                var state = DisplayUnlock(catalog.UnlockForBase(item.ID, playerState));
                var card = new Button(() => PreviewItem(item.ID));
                card.AddToClassList("ui-reset-button"); card.AddToClassList("ws-card");
                card.EnableInClassList("ws-card--preview", loadout.PreviewId == item.ID);
                card.Add(MakeArt(item, state.Status != UnlockStatus.Owned, false));
                card.Add(MakeLabel(item.Name, "ws-name", "ui-w600"));
                if (item.Granted) card.Add(MakeLabel("Always granted", "ws-name", "ui-w500"));
                if (menu != null) menu.Attach(card, () => new List<LobbyMenuItem> { new LobbyMenuItem("View details", () => PreviewItem(item.ID)) });
                (state.Status == UnlockStatus.Owned ? unlocked : notUnlocked).Add(card);
            }
        }
        private void PreviewItem(string id) { Inspect(Find(activeTab == Tab.Suits ? suits : districts, id)); RenderCards(); }
        private bool IsSelected(Item item)
        {
            if (item == null) return false;
            var data = loadout.ToData();
            return item.Granted || Array.IndexOf(activeTab == Tab.Suits ? data.suitIDs : data.districtIDs, item.ID) >= 0;
        }
        private void UsePreview()
        {
            if (inspectedItem == null || IsSelected(inspectedItem) || inventoryBusy) return;
            // Actual eligibility is independent of the debug display override.
            if (!catalog.UnlockForBase(inspectedItem.ID, playerState).CanUse || inspectedItem.Granted) return;
            if (!loadout.UsePreview(true, activeTab == Tab.Suits)) { Say("Loadout full. Tap a slot to clear it"); return; }
            SaveToProfile(); Render();
        }
        private void RenderInfo()
        {
            if (preview == null) return;
            preview.Clear(); stats?.Clear();
            Item item = inspectedItem;
            var state = item == null ? default : DisplayUnlock(catalog.UnlockForBase(item.ID, playerState));
            preview.Add(MakeArt(item, state.Status != UnlockStatus.Owned, state.Status == UnlockStatus.CannotUnlock));
            if (title != null) title.text = item?.Name ?? "Select a card";
            string text = item?.Description ?? "";
            string baseId = LoadoutTypes.CatalogBaseForLobbyId(item?.ID);
            var data = loadout.ToData(); var balance = TreeBalance();
            if (item != null && activeTab == Tab.Suits && Enum.TryParse(item.ID.Substring(5), true, out SuitType suit))
            {
                int type = (int)suit;
                int era = type >= 0 && type < data.suitEras.Length ? data.suitEras[type] : 0;
                foreach (var row in LoadoutInfoModel.SuitStats(balance, baseId, era))
                {
                    var line = new VisualElement(); line.AddToClassList("ws-stat");
                    line.Add(MakeLabel(row.Label + " " + row.Value + " (" + (row.Delta > 0 ? "+" : "") + row.Delta + ")", "ws-fact", "ui-w500"));
                    var track = new VisualElement(); track.AddToClassList("ws-histogram");
                    var zero = new VisualElement(); zero.AddToClassList("ws-zero"); track.Add(zero);
                    var bar = new VisualElement(); bar.AddToClassList("ws-bar"); bar.EnableInClassList("ws-bar--worse", row.Improvement < 0);
                    float width = Mathf.Abs(row.Improvement) / (float)Mathf.Max(1, Mathf.Abs(row.Value) + Mathf.Abs(row.Delta)) * 50f;
                    bar.style.width = Length.Percent(width); bar.style.left = Length.Percent(row.Improvement < 0 ? 50f - width : 50f);
                    track.Add(bar); line.Add(track); stats?.Add(line);
                }
            }
            else if (item != null && Enum.TryParse(item.ID.Substring(5), true, out DistrictType district))
            {
                int type = (int)district;
                int era = type >= 0 && type < data.districtEras.Length ? data.districtEras[type] : 0;
                foreach (var fact in NodeWar.UI.DistrictInfoFacts.Describe(balance, district, era))
                    stats?.Add(MakeLabel(fact.Label + ": " + fact.Value + " " + fact.Unit, "ws-fact", "ui-w500"));
                text = NodeWar.View.DistrictFallback.Describe(district).Description;
            }
            if (description != null) description.text = text;
            if (notice != null) notice.text = item != null && (state.Status == UnlockStatus.CannotUnlock || state.Status == UnlockStatus.Owned && !state.CanUse)
                ? "! This is unlockable in era " + state.UnlockEra : "";
            if (primary != null)
            {
                primary.text = item == null ? "Use" : IsSelected(item) ? "Selected" : state.Status == UnlockStatus.Owned ? "Use" : "Not owned";
                primary.SetEnabled(item != null && !inventoryBusy);
            }
            if (variants != null) variants.style.display = LoadoutChoices.Variants(baseId, playerState).Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
        private void RenderInventory()
        {
            if (eraMessage != null) { eraMessage.text = inventoryMessage; eraMessage.style.display = string.IsNullOrEmpty(inventoryMessage) ? DisplayStyle.None : DisplayStyle.Flex; }
            RenderCards(); RenderInfo();
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
            loadout.Preview(item?.ID);
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
                CollectItems();
                LoadFromProfile();
                Render();
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
            LoadoutChoice[] chips = skin ? LoadoutChoices.Skins(baseId, null, playerState) : LoadoutChoices.Variants(baseId, playerState);
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
                CollectItems();
                LoadFromProfile();
                Render();
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

        private static GameBalanceData TreeBalance()
        {
            NodeWar.Config.GameBalance shared = NodeWar.Config.GameBalance.LoadShared();
            return shared != null ? shared.Data : GameBalanceData.Default();
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
