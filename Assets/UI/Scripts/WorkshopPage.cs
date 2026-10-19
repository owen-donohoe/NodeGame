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
            public string ID, Name, Description;
            public ItemFamily.Family Family;
            public bool Granted;
        }
        private readonly LoadoutCatalog catalog;
        private readonly LobbyToast toast;
        private readonly LobbyContextMenu menu;
        private readonly List<Item> districts = new List<Item>(), suits = new List<Item>();
        private readonly Button segDistricts, segSuits, primary, variants;
        private readonly VisualElement unlocked, preview, stats;
        private readonly Label title, description, notice, eraMessage;
        private readonly ScrollView grid;
        private readonly LobbySheet sheet;
        private readonly VisualElement itemInfo;
        private readonly Button ownedFilter, sortFilter;
        private readonly Button[] presets = new Button[LoadoutPresets.Count];
        private readonly Label presetLabel, browserTitle;
        private bool closingForUse;
        private bool showAll;
        private LoadoutSort sort;
        private PlayerState playerState;
        private Item inspectedItem;
        private readonly WorkshopInventorySession session = new WorkshopInventorySession();
        private readonly VisualElement panelHost;
        private readonly MkSheet inventorySheet = new MkSheet();
        private string panelBaseId, panelChoiceId;
        private bool panelSkins;
        private readonly Button skins;
        private SkinBand activeBand;
        private bool inventoryBusy => session.Busy;
        private string inventoryMessage = "";
        private Task inventoryIdle = Task.CompletedTask;
        private LoadoutScreenModel loadout = new LoadoutScreenModel(LoadoutData.CreateEmpty());
        private Tab activeTab;
        public bool IsOpen => session.IsOpen;
        public VisualElement StageWindow => Root.Q<VisualElement>("workshop-window");
        public event Action Hidden;
        public event Action<bool> ViewChanged;
        public bool ReplacingSuit => loadout.ReplacingSuit;
        public bool Replacing => loadout.Replacing;
        public LoadoutData StageLoadout => loadout.ToData();
        public bool ViewingSuit => activeTab == Tab.Suits;
        public string ViewedId => inspectedItem?.ID;
        public event Action<bool, string> ItemViewed;
        public event Action LoadoutChanged;
        public event Func<bool> DistrictUseRequested;
        public event Action<int> DistrictSlotPicked;
        public event Action ReplacementChanged;
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
            this.toast = toast; this.menu = menu; this.sheet = sheet;
            segDistricts = Root.Q<Button>("workshop-tab-districts");
            segSuits = Root.Q<Button>("workshop-tab-suits");
            primary = Root.Q<Button>("workshop-info-use");
            variants = Root.Q<Button>("workshop-open-variants");
            skins = Root.Q<Button>("workshop-open-skins");
            unlocked = Root.Q<VisualElement>("workshop-unlocked");
            preview = Root.Q<VisualElement>("workshop-info-preview");
            stats = Root.Q<VisualElement>("workshop-info-stats");
            title = Root.Q<Label>("workshop-info-title");
            description = Root.Q<Label>("workshop-info-description");
            notice = Root.Q<Label>("workshop-info-unlock");
            eraMessage = Root.Q<Label>("workshop-era-message");
            grid = Root.Q<ScrollView>("workshop-grid");
            panelHost = Root.Q<VisualElement>("workshop-panel-host");
            ownedFilter = Root.Q<Button>("workshop-owned");
            sortFilter = Root.Q<Button>("workshop-sort");
            presetLabel = Root.Q<Label>("workshop-preset-label");
            browserTitle = Root.Q<Label>("workshop-browser-title");
            ownedFilter.clicked += () => { showAll = !showAll; RenderCards(); };
            sortFilter.clicked += () => { sort = (LoadoutSort)(((int)sort + 1) % 3); RenderCards(); };
            for (int i = 0; i < presets.Length; i++)
            {
                int index = i;
                presets[i] = Root.Q<Button>("workshop-preset-" + i);
                presets[i].clicked += () => SwitchPreset(index);
            }
            itemInfo = Root.Q<VisualElement>("workshop-info");
            itemInfo.RemoveFromHierarchy();
            itemInfo.styleSheets.Add(Resources.Load<StyleSheet>("Lobby/Workshop"));
            itemInfo.styleSheets.Add(Resources.Load<StyleSheet>("Lobby/PaperLobby"));
            if (sheet != null) sheet.Closed += SheetClosed;
            Root.Q<Button>("workshop-replace-cancel").clicked += CancelReplacement;
            RenderReplacement();
            if (segDistricts != null) segDistricts.clicked += () => SetTab(Tab.Districts);
            if (segSuits != null) segSuits.clicked += () => SetTab(Tab.Suits);
            if (primary != null) primary.clicked += UsePreview;
            if (variants != null) variants.clicked += OpenVariants;
            if (skins != null) skins.clicked += OpenSkins;
            Root.RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == Root) CloseInventory(); });
            CollectItems();
        }
        private static VisualElement Build(VisualTreeAsset layout)
        {
            var root = new VisualElement { name = "page-workshop", pickingMode = PickingMode.Ignore };
            root.AddToClassList("lb-page-flush");
            layout = Resources.Load<VisualTreeAsset>("Lobby/WorkshopPage") ?? layout;
            if (layout != null) layout.CloneTree(root);
            else root.Add(new Label("Workshop layout missing - assign WorkshopPage.uxml"));
            return root;
        }
        public override void OnShow()
        {
            int visit = session.BeginVisit();
            CloseItemPanel();
            playerState = BackendServices.LastKnownState;
            CollectItems(); LoadFromProfile(); Inspect(null); Render();
            _ = LoadInventoryAsync(visit);
        }
        public override void OnHide() { CloseInventory(); Hidden?.Invoke(); }
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
                    Family = ItemFamily.Family.Combat,
                    Granted = !catalog.IsSuitOffered(suit.suitID, arena)
                });
            }
        }

        private LoadoutTileOrder Order(Item item)
        {
            var state = DisplayUnlock(catalog.UnlockForBase(item.ID, playerState));
            return new LoadoutTileOrder(item.ID, item.Name, item.Family.ToString(), state.Status == UnlockStatus.Owned, state.UnlockEra);
        }
        private void SortByFamily(List<Item> items) => items.Sort((a, b) => LoadoutBrowse.Compare(Order(a), Order(b), sort));
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
            CancelReplacement();
            PlayerProfile profile = PlayerProfile.Instance;

            string preview = loadout.PreviewId;
            loadout = new LoadoutScreenModel(catalog.CurrentLoadout(playerState).ToLoadout());
            loadout.Preview(preview);
            activeTab = profile != null && profile.WorkshopTabIndex == (int)Tab.Suits ? Tab.Suits : Tab.Districts;
            // Until rank is confirmed, fallback display must not trim a saved high deck.
            if (playerState?.Rank != null) SaveToProfile();
        }
        private void SwitchPreset(int index)
        {
            if (!session.IsOpen || inventoryBusy) return;
            var profile = PlayerProfile.Instance;
            if (profile == null || profile.data.activeLoadoutPreset == index) return;
            CancelReplacement();
            CloseItemPanel();
            int arena = LoadoutRules.ArenaOrFallback(playerState?.Rank);
            var chosen = LoadoutPresets.Switch(ref profile.data, index, arena,
                id => catalog.IsSuitOffered(id, arena) && catalog.UnlockForBase(id, playerState).CanUse,
                id => catalog.IsDistrictOffered(id, arena) && catalog.UnlockForBase(id, playerState).CanUse);
            loadout = new LoadoutScreenModel(chosen);
            profile.Save();
            LoadoutChanged?.Invoke();
            Render();
        }
        private void SaveToProfile()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            if (profile != null) profile.SetLoadout(loadout.ToData());
            LoadoutChanged?.Invoke();
        }

        private void SetTab(Tab tab)
        {
            CancelReplacement();
            if (sheet != null && sheet.IsShowing(itemInfo)) sheet.Close();
            CloseItemPanel();
            activeTab = tab;
            ViewChanged?.Invoke(tab == Tab.Suits);
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
            Inspect(item);
            RenderCards();
            var profile = PlayerProfile.Instance;
            int selected = profile != null ? profile.data.activeLoadoutPreset : 0;
            for (int i = 0; i < presets.Length; i++)
            {
                presets[i].EnableInClassList("ui-chip--action", selected == i);
                presets[i].SetEnabled(!inventoryBusy);
            }
            int arena = LoadoutRules.ArenaOrFallback(playerState?.Rank);
            if (presetLabel != null) presetLabel.text = "Loadout " + (selected + 1) + " \u00b7 " + RankTable.Names[Mathf.Clamp(arena, 0, RankTable.Names.Count - 1)];
        }
        private VisualElement MakeArt(Item item, bool neutral, bool navy)
        {
            var clip = new VisualElement { pickingMode = PickingMode.Ignore };
            clip.AddToClassList("ws-art-clip");
            var art = new PaperCard { pickingMode = PickingMode.Ignore };
            art.AddToClassList("ws-art");
            art.AddToClassList("ui-tile");
            if (item != null) art.AddToClassList(ItemTint.ClassFor(item.ID));
            art.EnableInClassList("ws-art--neutral", neutral); art.EnableInClassList("ws-art--navy", navy);
            art.Add(MakeLabel(item == null ? "+" : ItemTint.MonogramFor(item.Name, item.ID), "ws-monogram", "ui-tile__monogram"));
            clip.Add(art); return clip;
        }
        private void RenderCards()
        {
            if (unlocked == null) return;
            unlocked.Clear();
            ownedFilter.text = showAll ? "All" : "Owned";
            sortFilter.text = "Sort: " + (sort == LoadoutSort.OwnedFirst ? "Owned first" : sort == LoadoutSort.Name ? "Name" : "Role");
            browserTitle.text = activeTab == Tab.Suits ? "Suits" : "Districts";
            var items = activeTab == Tab.Suits ? suits : districts;
            SortByFamily(items);
            foreach (Item item in items)
            {
                var state = DisplayUnlock(catalog.UnlockForBase(item.ID, playerState));
                if (!LoadoutBrowse.Includes(showAll, state.Status == UnlockStatus.Owned)) continue;
                var card = new PaperButton(() => PreviewItem(item.ID));
                card.AddToClassList("ui-reset-button"); card.AddToClassList("ws-card");
                card.EnableInClassList("ws-card--preview", loadout.PreviewId == item.ID);
                card.Add(MakeArt(item, state.Status != UnlockStatus.Owned, false));
                card.Add(MakeLabel(item.Name, "ws-name", "ui-w600"));
                card.EnableInClassList("ws-card--locked", state.Status != UnlockStatus.Owned);
                if (IsSelected(item)) card.Add(MakeLabel("In hand", "ws-mark", "ui-w700"));
                else if (state.Status != UnlockStatus.Owned) card.Add(MakeLabel("Arena " + state.UnlockEra, "ws-mark", "ws-mark--arena"));
                if (menu != null) menu.Attach(card, () =>
                {
                    var actions = new List<LobbyMenuItem> { new LobbyMenuItem("View details", () => PreviewItem(item.ID)) };
                    if (IsSelected(item) && !item.Granted)
                        actions.Add(new LobbyMenuItem("Remove from loadout", () =>
                        {
                            if (inventoryBusy || !session.IsOpen) return;
                            CancelReplacement();
                            bool suit = activeTab == Tab.Suits;
                            var chosen = suit ? StageLoadout.suitIDs : StageLoadout.districtIDs;
                            if (loadout.ClearChosen(suit, Array.IndexOf(chosen, item.ID))) { SaveToProfile(); Render(); }
                        }));
                    return actions;
                });
                unlocked.Add(card);
            }
            if (unlocked.childCount == 0) unlocked.Add(MakeLabel("Nothing here. Try All.", "ws-empty", "ui-w500"));
        }
        public void InspectStageItem(bool suit, int stageIndex)
        {
            if (!session.IsOpen || Replacing) return;
            var chosen = suit ? StageLoadout.suitIDs : StageLoadout.districtIDs;
            int slot = StageSlotMap.ToChosen(chosen, stageIndex);
            if (slot < 0) return;
            SetTab(suit ? Tab.Suits : Tab.Districts);
            PreviewItem(chosen[slot]);
        }

        private void PreviewItem(string id)
        {
            Inspect(Find(activeTab == Tab.Suits ? suits : districts, id));
            RenderCards();
            if (inspectedItem != null) sheet?.Open(itemInfo, true);
        }
        private void SheetClosed(VisualElement content)
        {
            if (content != itemInfo) return;
            CloseItemPanel();
            if (!Replacing && !closingForUse) { Inspect(null); RenderCards(); }
        }
        private bool IsSelected(Item item)
        {
            if (item == null) return false;
            var data = loadout.ToData();
            return item.Granted || Array.IndexOf(activeTab == Tab.Suits ? data.suitIDs : data.districtIDs, item.ID) >= 0;
        }
        private void UsePreview()
        {
            if (inspectedItem == null || IsSelected(inspectedItem) || inventoryBusy || Replacing) return;
            // Actual eligibility is independent of the debug display override.
            if (!catalog.UnlockForBase(inspectedItem.ID, playerState).CanUse || inspectedItem.Granted) return;
            closingForUse = true;
            if (sheet != null && sheet.IsShowing(itemInfo)) sheet.Close();
            closingForUse = false;
            if (activeTab == Tab.Districts && DistrictUseRequested?.Invoke() == true)
            {
                if (loadout.BeginReplace(true, inventoryBusy)) { RenderReplacement(); ReplacementChanged?.Invoke(); }
                else CancelReplacement();
                return;
            }
            if (!loadout.UsePreview(true, activeTab == Tab.Suits))
            {
                if (activeTab == Tab.Suits && loadout.BeginSuitReplace(true, inventoryBusy))
                { RenderReplacement(); ReplacementChanged?.Invoke(); }
                return;
            }
            SaveToProfile(); Render();
        }

        public void CompleteReplacement(int slot)
        {
            bool canUse = catalog.UnlockForBase(loadout.PendingDistrictId, playerState).CanUse;
            bool replaced = loadout.CompleteReplace(slot, canUse, inventoryBusy || !session.IsOpen);
            RenderReplacement();
            ReplacementChanged?.Invoke();
            if (replaced) { SaveToProfile(); Render(); }
        }

        public void CompleteSuitReplacement(int slot)
        {
            bool canUse = catalog.UnlockForBase(loadout.PendingSuitId, playerState).CanUse;
            bool replaced = loadout.CompleteSuitReplace(slot, canUse, inventoryBusy || !session.IsOpen);
            RenderReplacement();
            ReplacementChanged?.Invoke();
            if (replaced) { SaveToProfile(); Render(); }
        }

        public void CancelReplacement()
        {
            loadout.CancelReplace();
            RenderReplacement();
            ReplacementChanged?.Invoke();
        }
        private void RenderReplacement()
        {
            Root.Q<VisualElement>("workshop-toolbar").style.display = Replacing ? DisplayStyle.None : DisplayStyle.Flex;
            grid.style.display = Replacing ? DisplayStyle.None : DisplayStyle.Flex;
            Root.Q<VisualElement>("workshop-replace-banner").style.display = Replacing ? DisplayStyle.Flex : DisplayStyle.None;
            Root.Q<VisualElement>("workshop-replace-actions").style.display = Replacing ? DisplayStyle.Flex : DisplayStyle.None;
            var pending = ReplacingSuit ? Find(suits, loadout.PendingSuitId) : Find(districts, loadout.PendingDistrictId);
            Root.Q<Label>("workshop-replace-message").text = "Tap a wiggling slot to swap in " + (pending?.Name ?? "") + ".";
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
                AddStatRows(stats, LoadoutInfoModel.SuitStats(balance, baseId, era));
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
            if (skins != null) skins.style.display = item != null && LoadoutChoices.Skins(baseId, null, playerState).Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
        private static void AddStatRows(VisualElement host, LoadoutStatRow[] rows)
        {
            if (host == null) return;
            foreach (var row in rows)
            {
                var line = new VisualElement(); line.AddToClassList("ws-stat");
                line.Add(MakeLabel(row.Label + " " + row.Value + " (" + (row.Delta > 0 ? "+" : "") + row.Delta + ")", "ws-fact", "ui-w500"));
                var track = new VisualElement(); track.AddToClassList("ws-histogram");
                var zero = new VisualElement(); zero.AddToClassList("ws-zero"); track.Add(zero);
                var bar = new VisualElement(); bar.AddToClassList("ws-bar"); bar.EnableInClassList("ws-bar--worse", row.Improvement < 0);
                float width = Mathf.Abs(row.Improvement) / (float)Mathf.Max(1, Mathf.Abs(row.Value) + Mathf.Abs(row.Delta)) * 50f;
                bar.style.width = Length.Percent(width); bar.style.left = Length.Percent(row.Improvement < 0 ? 50f - width : 50f);
                track.Add(bar); line.Add(track); host.Add(line);
            }
        }
        private void RenderInventory()
        {
            for (int i = 0; i < presets.Length; i++) presets[i].SetEnabled(!inventoryBusy);
            if (eraMessage != null) { eraMessage.text = inventoryMessage; eraMessage.style.display = string.IsNullOrEmpty(inventoryMessage) ? DisplayStyle.None : DisplayStyle.Flex; }
            RenderCards(); RenderInfo(); RenderItemPanel();
        }
        private void CloseInventory()
        {
            CancelReplacement();
            ItemViewed?.Invoke(false, null);
            CloseItemPanel();
            session.Close();
            if (sheet != null && sheet.IsShowing(itemInfo)) sheet.Close();
        }

        private bool IsInventoryActive(int visit) => session.IsVisitCurrent(visit);

        private void Inspect(Item item)
        {
            if (inspectedItem?.ID != item?.ID) CancelReplacement();
            if (inspectedItem != item)
            {
                inspectedItem = item;
                if (!inventoryBusy && playerState != null) inventoryMessage = "";
            }
            session.Select(item == null ? null : LoadoutTypes.CatalogBaseForLobbyId(item.ID));
            if (panelBaseId != null && panelBaseId != session.SelectedBaseID) CloseItemPanel();
            loadout.Preview(item?.ID);
            RenderInventory();
            ItemViewed?.Invoke(activeTab == Tab.Suits, item?.ID);
        }

        private async Task LoadInventoryAsync(int visit)
        {
            CancelReplacement();
            inventoryMessage = "Loading inventory...";
            RenderInventory();
            // A reopened page must not fetch a snapshot before an earlier save finishes.
            await inventoryIdle;
            if (!IsInventoryActive(visit) || !session.TryBeginRequest(out InventoryRequestToken token)) return;
            RenderInventory();
            try
            {
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
                session.EndRequest(token);
                if (IsInventoryActive(visit)) RenderInventory();
            }
        }

        private Task<bool> EquipVariantAsync(string baseId, string variantId) => EquipInventoryAsync(baseId, variantId, false);
        private Task<bool> EquipSkinAsync(string baseId, string skinId) => EquipInventoryAsync(baseId, skinId, true);

        private async Task<bool> EquipInventoryAsync(string baseId, string id, bool skin)
        {
            if (!session.IsOpen || inspectedItem == null ||
                LoadoutTypes.CatalogBaseForLobbyId(inspectedItem.ID) != baseId) return false;
            LoadoutChoice[] chips = skin ? LoadoutChoices.Skins(baseId, null, playerState) : LoadoutChoices.Variants(baseId, playerState);
            if (!Array.Exists(chips, chip => chip.ID == id && chip.Selectable)) return false;
            if (!session.TryBeginRequest(out InventoryRequestToken token)) return false;

            int visit = token.Visit;
            var completion = new TaskCompletionSource<bool>();
            inventoryIdle = completion.Task;
            inventoryMessage = "Saving...";
            RenderInventory();
            bool applied = false;
            try
            {
                var changes = new EquippedRecord();
                var map = new Dictionary<string, string> { [baseId] = id };
                if (skin) changes.Skins = map;
                else changes.Variants = map;
                PlayerState result = await BackendServices.Inventory.EquipAsync(changes);
                if (!session.IsCurrent(token)) return false;
                if (result == null) throw new InvalidOperationException("Missing player state");
                playerState = result;
                CollectItems();
                LoadFromProfile();
                Render();
                inventoryMessage = "";
                applied = true;
                LoadoutChanged?.Invoke();
            }
            catch (Exception)
            {
                if (session.IsCurrent(token))
                {
                    inventoryMessage = "Couldn't equip. Please try again.";
                    Say(inventoryMessage);
                }
            }
            finally
            {
                bool visitCurrent = session.IsVisitCurrent(token);
                bool current = session.IsCurrent(token);
                session.EndRequest(token);
                completion.TrySetResult(true);
                if (visitCurrent)
                {
                    if (!current)
                    {
                        // Discard the old selection's response and get a fresh snapshot.
                        playerState = null;
                        _ = LoadInventoryAsync(visit);
                    }
                    else RenderInventory();
                }
            }
            return applied;
        }

        // ===== VARIANT SHEET =====

        public void BindOverlayRouter(LobbyOverlayRouter router, VisualElement shell)
        {
            inventorySheet.BindRouter(router, "workshop-choices");
            shell.Add(inventorySheet);
            inventorySheet.Opened += () =>
            {
                inventorySheet.BringToFront();
                shell.Q("toast-dock")?.BringToFront();
            };
            inventorySheet.Closed += CloseItemPanel;
        }

        private void OpenVariants()
        {
            CancelReplacement();
            Item item = inspectedItem;
            if (item == null || panelHost == null || inventoryBusy) return;
            string baseId = LoadoutTypes.CatalogBaseForLobbyId(item.ID);
            LoadoutChoice[] choices = LoadoutChoices.Variants(baseId, playerState);
            if (choices.Length == 0) return;
            CloseItemPanel();
            panelBaseId = baseId;
            panelChoiceId = choices[0].ID;
            foreach (var choice in choices) if (choice.Equipped) { panelChoiceId = choice.ID; break; }
            RenderItemPanel();
        }

        private void OpenSkins()
        {
            CancelReplacement();
            Item item = inspectedItem;
            if (item == null || panelHost == null || inventoryBusy) return;
            string baseId = LoadoutTypes.CatalogBaseForLobbyId(item.ID);
            LoadoutChoice[] choices = LoadoutChoices.Skins(baseId, null, playerState);
            if (choices.Length == 0) return;
            CloseItemPanel();
            panelBaseId = baseId; panelSkins = true;
            panelChoiceId = choices[0].ID;
            foreach (var choice in choices) if (choice.Equipped) { panelChoiceId = choice.ID; break; }
            RenderItemPanel();
        }

        private void RenderSkinPanel()
        {
            activeBand?.Stop(); activeBand = null;
            inventorySheet.Body.Clear();
            string baseId = panelBaseId;
            LoadoutChoice[] choices = LoadoutChoices.Skins(baseId, null, playerState);
            if (choices.Length == 0 || inspectedItem == null) { CloseItemPanel(); return; }
            int centered = 0;
            for (int i = 0; i < choices.Length; i++) if (choices[i].ID == panelChoiceId) centered = i;
            panelChoiceId = choices[centered].ID;

            panelHost.pickingMode = PickingMode.Position;
            var sheet = new VisualElement();
            var body = new VisualElement { pickingMode = PickingMode.Ignore }; body.AddToClassList("ws-sheet-body");
            var art = new PaperCard { pickingMode = PickingMode.Ignore }; art.AddToClassList("ws-preview");
            body.Add(art);
            Label title = MakeLabel("", "ws-title", "ui-w600");
            Label status = MakeLabel("", "ws-fact", "ui-w500");
            body.Add(title); body.Add(status);
            var buy = new PaperButton(() => Say("Coming soon")) { text = "Buy another box" };
            buy.AddToClassList("ui-reset-button"); buy.AddToClassList("ws-secondary"); buy.AddToClassList("ui-w600");
            body.Add(buy);
            sheet.Add(body);

            var band = new SkinBand();
            activeBand = band;
            band.SetChoices(choices, centered);
            sheet.Add(band);

            var buttons = new VisualElement(); buttons.AddToClassList("ws-sheet-buttons");
            var back = new PaperButton(CloseItemPanel) { text = "Back" };
            back.AddToClassList("ui-reset-button"); back.AddToClassList("ws-sheet-button"); back.AddToClassList("ui-w600");
            var use = new PaperButton();
            use.AddToClassList("ui-reset-button"); use.AddToClassList("ws-sheet-button"); use.AddToClassList("ws-primary"); use.AddToClassList("ui-w600");
            use.SetEnabled(!inventoryBusy);
            buttons.Add(back); buttons.Add(use);
            sheet.Add(buttons);
            inventorySheet.Body.Add(sheet);
            inventorySheet.Open(true);

            // Updates the centred skin's preview, status and Use label in place; the band is untouched.
            void ShowCentered(int index)
            {
                LoadoutChoice choice = choices[Mathf.Clamp(index, 0, choices.Length - 1)];
                panelChoiceId = choice.ID;
                var state = DisplayUnlock(choice.UnlockState);
                art.Clear();
                art.Add(MakeArt(inspectedItem, state.Status != UnlockStatus.Owned, state.Status == UnlockStatus.CannotUnlock));
                title.text = inspectedItem.Name + ", " + choice.Label.ToLowerInvariant();
                status.text = choice.Equipped ? "Equipped" : state.Status == UnlockStatus.Owned ? "Owned"
                    : state.Status == UnlockStatus.CannotUnlock ? "Locked. ! This is unlockable in era " + state.UnlockEra : "Locked";
                use.text = choice.Equipped ? "Selected" : state.Status == UnlockStatus.Owned ? "Use" : "Not owned";
            }
            ShowCentered(centered);
            band.CenterChanged += ShowCentered;
            // Actual eligibility is independent of the debug display override.
            use.clicked += () =>
            {
                LoadoutChoice choice = choices[Mathf.Clamp(band.CenteredIndex, 0, choices.Length - 1)];
                if (choice.Selectable) _ = EquipSkinAsync(baseId, choice.ID);
            };
        }

        private void CloseItemPanel()
        {
            activeBand?.Stop(); activeBand = null;
            panelBaseId = null; panelChoiceId = null; panelSkins = false;
            inventorySheet.Close();
            if (panelHost == null) return;
            inventorySheet.Body.Clear();
            panelHost.pickingMode = PickingMode.Ignore;
        }

        private void RenderItemPanel()
        {
            if (panelHost == null || panelBaseId == null) return;
            if (panelSkins) { RenderSkinPanel(); return; }
            inventorySheet.Body.Clear();
            string baseId = panelBaseId;
            LoadoutChoice[] choices = LoadoutChoices.Variants(baseId, playerState);
            if (choices.Length == 0 || inspectedItem == null) { CloseItemPanel(); return; }
            LoadoutChoice current = choices[0];
            foreach (var choice in choices) if (choice.ID == panelChoiceId) current = choice;
            panelChoiceId = current.ID;
            var state = DisplayUnlock(current.UnlockState);

            panelHost.pickingMode = PickingMode.Position;
            var sheet = new VisualElement();
            var body = new ScrollView(ScrollViewMode.Vertical);
            body.AddToClassList("ws-sheet-body");
            body.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            body.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            var art = new PaperCard { pickingMode = PickingMode.Ignore }; art.AddToClassList("ws-preview");
            art.Add(MakeArt(inspectedItem, state.Status != UnlockStatus.Owned, state.Status == UnlockStatus.CannotUnlock));
            body.Add(art);
            body.Add(MakeLabel(inspectedItem.Name + ", " + current.Label.ToLowerInvariant(), "ws-title", "ui-w600"));
            var rows = new VisualElement { pickingMode = PickingMode.Ignore };
            AddStatRows(rows, LoadoutInfoModel.SuitStats(TreeBalance(), baseId, current.OrderKey));
            body.Add(rows);
            body.Add(MakeLabel(inspectedItem.Description ?? "", "ws-fact", "ui-w500"));
            bool locked = state.Status == UnlockStatus.CannotUnlock || state.Status == UnlockStatus.Owned && !state.CanUse;
            if (locked) body.Add(MakeLabel("! This is unlockable in era " + state.UnlockEra, "ws-fact", "ui-w500"));
            if (!string.IsNullOrEmpty(inventoryMessage)) body.Add(MakeLabel(inventoryMessage, "ws-fact", "ui-w500"));
            sheet.Add(body);

            var strip = new ScrollView(ScrollViewMode.Horizontal);
            strip.AddToClassList("ws-strip");
            strip.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            strip.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            foreach (LoadoutChoice choice in choices)
            {
                string choiceId = choice.ID;
                var chipState = DisplayUnlock(choice.UnlockState);
                var chip = new PaperButton(() => { panelChoiceId = choiceId; RenderItemPanel(); });
                chip.AddToClassList("ui-reset-button"); chip.AddToClassList("ws-chip");
                chip.EnableInClassList("ws-card--preview", choiceId == current.ID);
                chip.Add(MakeArt(inspectedItem, chipState.Status != UnlockStatus.Owned, false));
                chip.Add(MakeLabel(choice.Label, "ws-name", "ui-w600"));
                strip.Add(chip);
            }
            sheet.Add(strip);

            var buttons = new VisualElement(); buttons.AddToClassList("ws-sheet-buttons");
            var back = new PaperButton(CloseItemPanel) { text = "Back" };
            back.AddToClassList("ui-reset-button"); back.AddToClassList("ws-sheet-button"); back.AddToClassList("ui-w600");
            var use = new PaperButton { text = current.Equipped ? "Selected" : state.Status == UnlockStatus.Owned ? "Use" : "Not owned" };
            use.AddToClassList("ui-reset-button"); use.AddToClassList("ws-sheet-button"); use.AddToClassList("ws-primary"); use.AddToClassList("ui-w600");
            use.SetEnabled(!inventoryBusy);
            string useId = current.ID;
            // Actual eligibility is independent of the debug display override.
            use.clicked += () => { if (current.Selectable) _ = EquipVariantAsync(baseId, useId); };
            buttons.Add(back); buttons.Add(use);
            sheet.Add(buttons);
            inventorySheet.Body.Add(sheet);
            inventorySheet.Open(true);
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
