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
        private readonly Button segDistricts, segSuits;
        private readonly VisualElement unlocked;
        private readonly Label eraMessage;
        private readonly ScrollView grid;
        private readonly VisualElement firstPanel, panelSpace, stageWindow, secondPanel, secondSpace, secondClip;
        private readonly VisualElement navClearance = new VisualElement { pickingMode = PickingMode.Ignore };
        private float stageBandHeight = 220f;
        private VisualElement nav;

        private readonly Button ownedFilter, allFilter, sortFilter;
        private readonly Button[] presets = new Button[LoadoutPresets.Count];
        private readonly Label presetLabel, browserTitle;
        private bool closingForUse;
        private bool showAll;
        private LoadoutSort sort;
        private PlayerState playerState;
        private Item inspectedItem;
        private readonly WorkshopInventorySession session = new WorkshopInventorySession();

        private readonly MkSheet inventorySheet = new MkSheet();
        private string panelBaseId, panelChoiceId, panelSkinId;
        private int panelRevision;
        private readonly VisualElement itemActions = new VisualElement();

        private bool inventoryBusy => session.Busy;
        private string inventoryMessage = "";
        private Task inventoryIdle = Task.CompletedTask;
        private LoadoutScreenModel loadout = new LoadoutScreenModel(LoadoutData.CreateEmpty());
        private Tab activeTab;
        public bool IsOpen => session.IsOpen;
        public VisualElement StageWindow => stageWindow;
        public float StageViewportTop => grid.contentViewport.worldBound.y;
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
            this.toast = toast; this.menu = menu;
            segDistricts = Root.Q<Button>("workshop-tab-districts");
            segSuits = Root.Q<Button>("workshop-tab-suits");
            unlocked = Root.Q<VisualElement>("workshop-unlocked");
            unlocked.RegisterCallback<GeometryChangedEvent>(_ => SizeCards());
            eraMessage = Root.Q<Label>("workshop-era-message");
            grid = Root.Q<ScrollView>("workshop-grid");
            grid.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            firstPanel = Root.Q("workshop-panel1"); panelSpace = Root.Q("workshop-panel1-space");
            stageWindow = Root.Q("workshop-window");
            secondPanel = Root.Q("workshop-panel2"); secondSpace = Root.Q("workshop-panel2-space");
            secondClip = Root.Q("workshop-panel2-clip");
            navClearance.AddToClassList("ui-lobby-list-end"); grid.Add(navClearance);
            secondPanel.RegisterCallback<GeometryChangedEvent>(_ => UpdateScrollLayout());
            // Render the sticky first card last, above the scrolling second card.
            firstPanel.BringToFront();
            Root.RegisterCallback<GeometryChangedEvent>(_ => UpdateScrollLayout());
            firstPanel.RegisterCallback<GeometryChangedEvent>(_ => UpdateScrollLayout());
            grid.verticalScroller.valueChanged += _ => UpdateScrollLayout();
            ownedFilter = Root.Q<Button>("workshop-owned");
            allFilter = Root.Q<Button>("workshop-all");
            allFilter.AddToClassList("ui-filter-all");
            sortFilter = Root.Q<Button>("workshop-sort");
            presetLabel = Root.Q<Label>("workshop-preset-label");
            browserTitle = Root.Q<Label>("workshop-browser-title");
            ownedFilter.clicked += () => { showAll = false; RenderCards(); };
            allFilter.clicked += () => { showAll = true; RenderCards(); };
            sortFilter.clicked += () => { sort = (LoadoutSort)(((int)sort + 1) % 3); RenderCards(); };
            for (int i = 0; i < presets.Length; i++)
            {
                int index = i;
                presets[i] = Root.Q<Button>("workshop-preset-" + i);
                presets[i].clicked += () => SwitchPreset(index);
            }
            Root.Q<Button>("workshop-replace-cancel").clicked += CancelReplacement;
            RenderReplacement();
            if (segDistricts != null) segDistricts.clicked += () => SetTab(Tab.Districts);
            if (segSuits != null) segSuits.clicked += () => SetTab(Tab.Suits);
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
        public void SetStageViewportBottom(float bottom)
        {
            if (grid.panel == null) return;
            var panel = Root.panel.visualTree.worldBound;
            float height = Mathf.Max(0, panel.y + bottom * panel.height + 12f - StageViewportTop);
            if (!Mathf.Approximately(height, stageBandHeight)) stageBandHeight = height;
            UpdateScrollLayout();
        }

        private void UpdateScrollLayout()
        {
            if (grid?.panel == null) return;
            float navHeight = nav?.resolvedStyle.height ?? 0;
            navClearance.style.height = float.IsNaN(navHeight) ? 0 : navHeight;
            if (Replacing && grid.scrollOffset != Vector2.zero) grid.scrollOffset = Vector2.zero;
            if (!Mathf.Approximately(stageWindow.layout.height, stageBandHeight)) stageWindow.style.height = stageBandHeight;
            if (!Mathf.Approximately(panelSpace.layout.height, firstPanel.layout.height)) panelSpace.style.height = firstPanel.layout.height;
            var clip = WorkshopLayout.Clip(stageBandHeight, firstPanel.layout.height, secondPanel.layout.height, grid.scrollOffset.y);
            firstPanel.style.top = clip.HeaderTop;
            secondSpace.style.height = clip.ContentHeight;
            secondClip.style.top = clip.ClipTop; secondClip.style.height = clip.MaskHeight;
            secondPanel.style.top = clip.CardOffset;
        }
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
            segDistricts?.EnableInClassList("mk-seg2__btn--on", activeTab == Tab.Districts);
            segSuits?.EnableInClassList("mk-seg2__btn--on", activeTab == Tab.Suits);
            var items = activeTab == Tab.Suits ? suits : districts;
            SortByFamily(items);
            Item item = Find(items, loadout.PreviewId);
            Inspect(item);
            RenderCards();
            var profile = PlayerProfile.Instance;
            int selected = profile != null ? profile.data.activeLoadoutPreset : 0;
            for (int i = 0; i < presets.Length; i++)
            {
                presets[i].Q<Label>().EnableInClassList("mk-chip--on", selected == i);
                presets[i].SetEnabled(!inventoryBusy);
            }
            int arena = LoadoutRules.ArenaOrFallback(playerState?.Rank);
            if (presetLabel != null) presetLabel.text = "LOADOUT " + (selected + 1) + " \u00b7 " + RankTable.Names[Mathf.Clamp(arena, 0, RankTable.Names.Count - 1)];
        }
        private VisualElement MakeArt(Item item, bool neutral, bool navy)
        {
            var clip = new VisualElement { pickingMode = PickingMode.Ignore };
            clip.AddToClassList("ws-art-clip"); clip.AddToClassList("mk-art");
            var art = new VisualElement { pickingMode = PickingMode.Ignore };
            art.AddToClassList("ws-art");

            if (item != null) art.AddToClassList(ItemTint.ClassFor(item.ID));
            art.EnableInClassList("ws-art--neutral", neutral); art.EnableInClassList("ws-art--navy", navy);
            art.Add(MakeLabel(item == null ? "+" : ItemTint.MonogramFor(item.Name, item.ID), "ws-monogram", "mk-h2"));
            clip.Add(art); return clip;
        }
        private void RenderCards()
        {
            if (unlocked == null) return;
            unlocked.Clear();
            ownedFilter.EnableInClassList("ui-filter-chip--on", !showAll);
            allFilter.EnableInClassList("ui-filter-chip--on", showAll);
            sortFilter.Q<Label>().text = "Sort: " + (sort == LoadoutSort.OwnedFirst ? "Arena" : sort == LoadoutSort.Name ? "Name" : "Role");
            browserTitle.text = activeTab == Tab.Suits ? "Suits" : "Districts";
            var items = activeTab == Tab.Suits ? suits : districts;
            SortByFamily(items);
            foreach (Item item in items)
            {
                var state = DisplayUnlock(catalog.UnlockForBase(item.ID, playerState));
                if (!LoadoutBrowse.Includes(showAll, state.Status == UnlockStatus.Owned)) continue;
                var card = new Button(() => PreviewItem(item.ID));
                card.AddToClassList("mk-tile"); card.EnableInClassList("mk-tile--selected", loadout.PreviewId == item.ID); card.AddToClassList("ws-card");
                card.EnableInClassList("ws-card--preview", loadout.PreviewId == item.ID);
                card.Add(MakeArt(item, state.Status != UnlockStatus.Owned, false));
                card.Add(MakeLabel(item.Name, "ws-name", "mk-h3"));
                card.Add(MakeLabel("Era " + state.UnlockEra, "mk-caption", "ws-tile-meta"));
                card.EnableInClassList("mk-tile--locked", state.Status != UnlockStatus.Owned);
                if (IsSelected(item)) card.Add(MakeLabel("In hand", "mk-mark", "ws-mark"));
                else if (state.Status != UnlockStatus.Owned) card.Add(MakeLabel("Arena " + state.UnlockEra, "mk-mark", "mk-mark--era", "ws-mark"));
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
            if (unlocked.childCount == 0) unlocked.Add(MakeLabel("Nothing here. Try All.", "ws-empty", "mk-muted"));
            SizeCards();
        }
        private void SizeCards()
        {
            if (unlocked.contentRect.width <= 24) return;
            float width = WorkshopLayout.TileWidth(unlocked.contentRect.width);
            int index = 0;
            unlocked.Query<Button>(className: "mk-tile").ForEach(card =>
            {
                // Unity's default Button has a left margin. Two border-box widths
                // plus that inherited margin overflow the row and wrap to one column.
                card.style.marginLeft = card.style.marginTop = 0;
                card.style.flexBasis = StyleKeyword.Auto;
                card.style.width = card.style.maxWidth = width;
                card.style.minWidth = card.style.minHeight = 0;
                card.style.height = 152;
                card.style.marginRight = index++ % 2 == 1 ? 0 : WorkshopLayout.TileGap;
                card.style.marginBottom = WorkshopLayout.TileGap;
                card.Q(className: "ws-art-clip").style.height = 78;
            });
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
            if (inspectedItem != null) OpenItemPanel();
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
            closingForUse = true; CloseItemPanel(); closingForUse = false;
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
            Root.Q("workshop-browser").style.display = Replacing ? DisplayStyle.None : DisplayStyle.Flex;
            Root.Q<VisualElement>("workshop-replace-banner").style.display = Replacing ? DisplayStyle.Flex : DisplayStyle.None;
            Root.Q<VisualElement>("workshop-replace-actions").style.display = Replacing ? DisplayStyle.Flex : DisplayStyle.None;
            var pending = ReplacingSuit ? Find(suits, loadout.PendingSuitId) : Find(districts, loadout.PendingDistrictId);
            if (Replacing) grid.scrollOffset = Vector2.zero;
            Root.Q<Label>("workshop-replace-message").text = "Tap a wiggling slot to swap in " + (pending?.Name ?? "") + ".";
        }
        private void RenderInventory()
        {
            for (int i = 0; i < presets.Length; i++) presets[i].SetEnabled(!inventoryBusy);
            if (eraMessage != null) { eraMessage.text = inventoryMessage; eraMessage.style.display = string.IsNullOrEmpty(inventoryMessage) ? DisplayStyle.None : DisplayStyle.Flex; }
            RenderCards(); RenderItemPanel();
        }
        private void CloseInventory()
        {
            CancelReplacement();
            ItemViewed?.Invoke(false, null);
            CloseItemPanel();
            session.Close();
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

        // ===== ITEM SHEET =====

        public void BindOverlayRouter(LobbyOverlayRouter router, VisualElement shell)
        {
            nav = shell.Q("tabbar-dock");
            inventorySheet.BindRouter(router, "workshop-item");
            inventorySheet.styleSheets.Add(Resources.Load<StyleSheet>("Lobby/Workshop"));
            inventorySheet.Surface.AddToClassList("ws-item-sheet");
            inventorySheet.Body.style.flexGrow = 1;
            ((ScrollView)inventorySheet.Body).touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            itemActions.AddToClassList("ws-sheet-actions"); inventorySheet.Surface.Add(itemActions);
            shell.Add(inventorySheet);
            inventorySheet.Opened += () =>
            {
                inventorySheet.BringToFront(); shell.Q("toast-dock")?.BringToFront();
            };
            inventorySheet.Closed += () =>
            {
                CloseItemPanel();
                if (!closingForUse && !Replacing) { Inspect(null); RenderCards(); }
            };
        }

        private void OpenItemPanel()
        {
            CancelReplacement();
            if (inspectedItem == null || !session.IsOpen) return;
            panelRevision++;
            panelBaseId = LoadoutTypes.CatalogBaseForLobbyId(inspectedItem.ID);
            panelChoiceId = EquippedChoice(LoadoutChoices.Variants(panelBaseId, playerState));
            panelSkinId = EquippedChoice(LoadoutChoices.Skins(panelBaseId, null, playerState));
            RenderItemPanel(); inventorySheet.Open(true);
        }

        private static string EquippedChoice(LoadoutChoice[] choices)
        {
            foreach (var choice in choices) if (choice.Equipped) return choice.ID;
            return choices.Length > 0 ? choices[0].ID : null;
        }

        private void CloseItemPanel()
        {
            panelRevision++; panelBaseId = panelChoiceId = panelSkinId = null;
            inventorySheet.Close(); inventorySheet.Body.Clear(); itemActions.Clear();
        }

        private static Button SheetButton(string text, Action clicked, string variant = null)
        {
            var button = new Button(clicked) { text = text }; button.AddToClassList("mk-btn");
            if (variant != null) button.AddToClassList(variant);
            return button;
        }

        private void RenderItemPanel()
        {
            if (panelBaseId == null || inspectedItem == null) return;
            inventorySheet.Body.Clear(); itemActions.Clear();
            string baseId = panelBaseId;
            var choices = LoadoutChoices.Variants(baseId, playerState);
            int index = Array.FindIndex(choices, choice => choice.ID == panelChoiceId);
            if (index < 0 && choices.Length > 0) index = 0;
            if (index >= 0) panelChoiceId = choices[index].ID;
            int variant = index >= 0 ? choices[index].OrderKey : CurrentEra(inspectedItem);
            var state = DisplayUnlock(index >= 0 ? choices[index].UnlockState : catalog.UnlockForBase(inspectedItem.ID, playerState));
            var content = new VisualElement(); content.AddToClassList("ws-item-content");
            string name = inspectedItem.Name + (index >= 0 && choices[index].OrderKey != 0 ? " · " + choices[index].Label : "");
            content.Add(MakeLabel(name, "mk-h2", "ws-item-title"));
            var artRow = new VisualElement(); artRow.AddToClassList("ws-item-art");
            var art = new VisualElement(); art.AddToClassList("ws-artbox");
            art.Add(MakeArt(inspectedItem, state.Status != UnlockStatus.Owned, state.Status == UnlockStatus.CannotUnlock));
            artRow.Add(art);
            void Cycle(int direction)
            {
                if (inventoryBusy || choices.Length < 2) return;
                panelRevision++; panelChoiceId = choices[(index + direction + choices.Length) % choices.Length].ID; RenderItemPanel();
            }
            var previous = new Button(() => Cycle(-1)) { text = "", name = "workshop-variant-prev" };
            var next = new Button(() => Cycle(1)) { text = "", name = "workshop-variant-next" };
            previous.AddToClassList("mk-arr"); previous.AddToClassList("ws-arr-prev");
            next.AddToClassList("mk-arr"); next.AddToClassList("ws-arr-next");
            previous.SetEnabled(!inventoryBusy && choices.Length > 1); next.SetEnabled(!inventoryBusy && choices.Length > 1);
            artRow.Add(previous); artRow.Add(next); content.Add(artRow);
            var dots = new VisualElement(); dots.AddToClassList("mk-dots");
            for (int i = 0; i < Math.Max(1, choices.Length); i++)
            {
                bool locked = choices.Length > 0 && !choices[i].UnlockState.CanUse;
                var dot = MakeLabel("", "mk-dot2", "ws-variant-dot");
                dot.EnableInClassList("mk-dot2--on", choices.Length == 0 || i == index);
                dot.EnableInClassList("mk-dot2--locked", locked); dots.Add(dot);
            }
            dots.AddToClassList("ws-preview-dots"); artRow.Add(dots);
            var histogram = new VisualElement(); histogram.AddToClassList("mk-hist");
            var balance = TreeBalance();
            HistogramRow[] rows;
            if (activeTab == Tab.Suits)
                rows = StatHistogram.SuitRows(LoadoutInfoModel.SuitStats(balance, baseId, variant), LoadoutInfoModel.SuitStats(balance, baseId, 0));
            else if (Enum.TryParse(inspectedItem.ID.Substring(5), true, out DistrictType district))
                rows = StatHistogram.DistrictRows(balance, district, variant);
            else rows = Array.Empty<HistogramRow>();
            foreach (var row in rows) histogram.Add(HistogramLine(row));

            string description = inspectedItem.Description ?? "";
            if (activeTab == Tab.Districts && Enum.TryParse(inspectedItem.ID.Substring(5), true, out DistrictType type))
                description = NodeWar.View.DistrictFallback.Describe(type).Description ?? description;

            if (!state.CanUse) content.Add(MakeLabel("Locked · Arena " + state.UnlockEra, "mk-muted", "ws-item-status"));
            var skins = LoadoutChoices.Skins(baseId, null, playerState);
            if (!Array.Exists(skins, choice => choice.ID == panelSkinId)) panelSkinId = EquippedChoice(skins);
            var skinScroll = new ScrollView(ScrollViewMode.Horizontal);
            skinScroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            skinScroll.AddToClassList("ws-skin-scroll"); skinScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            skinScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            var skinRow = new VisualElement(); skinRow.AddToClassList("mk-skins");
            foreach (var choice in skins)
            {
                string skinId = choice.ID;
                var chip = new Button(() => { panelRevision++; panelSkinId = skinId; RenderItemPanel(); });
                chip.AddToClassList("mk-skin"); chip.EnableInClassList("mk-skin--on", panelSkinId == skinId);
                chip.EnableInClassList("mk-skin--locked", !choice.UnlockState.CanUse);
                var skinArt = MakeArt(inspectedItem, !choice.UnlockState.CanUse, false);
                skinArt.AddToClassList("ws-skin-art"); chip.Add(skinArt);
                chip.tooltip = choice.Label + (choice.UnlockState.CanUse ? "" : " (locked)");
                chip.SetEnabled(!inventoryBusy); skinRow.Add(chip);
            }
            // No catalogue of unowned cosmetics is supplied yet. Keep the real
            // owned choices, and show inert locked previews without inventing IDs.
            for (int i = skins.Length; i < 4; i++)
            {
                var placeholder = new Button { tooltip = "Locked skin preview" };
                placeholder.AddToClassList("mk-skin"); placeholder.AddToClassList("mk-skin--locked");
                var icon = new LobbyIcon(LobbyIconKind.Lock, LobbyIconContext.Workshop);
                icon.AddToClassList("ws-skin-lock"); placeholder.Add(icon);
                placeholder.SetEnabled(false); skinRow.Add(placeholder);
            }
            content.Add(MakeLabel("Skins", "mk-h3"));
            skinScroll.Add(skinRow); content.Add(skinScroll); content.Add(histogram);
            content.Add(MakeLabel(description, "mk-muted", "ws-description"));
            if (!string.IsNullOrEmpty(inventoryMessage)) content.Add(MakeLabel(inventoryMessage, "mk-muted", "ws-item-status"));
            inventorySheet.Body.Add(content);
            itemActions.Add(SheetButton("BACK", CloseItemPanel));
            var use = SheetButton("USE", () => _ = UseItemAsync(), "mk-btn--primary");
            bool canUse = catalog.UnlockForBase(inspectedItem.ID, playerState).CanUse &&
                (index < 0 || choices[index].Equipped || choices[index].Selectable);
            bool skinValid = Array.Exists(skins, choice => choice.ID == panelSkinId && (choice.Equipped || choice.Selectable));
            bool changes = !IsSelected(inspectedItem) || index >= 0 && !choices[index].Equipped ||
                Array.Exists(skins, choice => choice.ID == panelSkinId && !choice.Equipped);
            use.SetEnabled(!inventoryBusy && canUse && skinValid && changes); itemActions.Add(use);
        }

        private int CurrentEra(Item item)
        {
            var data = loadout.ToData();
            var eras = activeTab == Tab.Suits ? data.suitEras : data.districtEras;
            int type = DistrictMigration.SourceType(item.ID);
            if (activeTab == Tab.Suits && Enum.TryParse(item.ID.Substring(5), true, out SuitType suit)) type = (int)suit;
            return type >= 0 && type < eras.Length ? eras[type] : 0;
        }

        private static VisualElement HistogramLine(HistogramRow row)
        {
            var line = new VisualElement { tooltip = "1 cell = " + row.Unit };
            line.AddToClassList("mk-hrow"); line.AddToClassList("ws-stat-row");
            var heading = new VisualElement(); heading.AddToClassList("ws-stat-heading");
            heading.Add(MakeLabel(row.Label, "ws-stat-label"));
            heading.Add(MakeLabel(row.Value + " (" + (row.Delta > 0 ? "+" : "") + row.Delta + ")", "mk-value", "ws-stat-value"));
            line.Add(heading);
            var cells = new VisualElement(); cells.AddToClassList("mk-cells"); cells.AddToClassList("ws-stat-cells");
            for (int i = 0; i < StatHistogram.MaxCells; i++)
            {
                bool lost = i >= row.Filled && i < row.Filled + row.Loss;
                var cell = MakeLabel("", "mk-cell", "ws-stat-cell");
                if (i < row.Filled) cell.AddToClassList("mk-cell--f");
                else if (i < row.Filled + row.Gain) cell.AddToClassList("mk-cell--g");
                else if (lost)
                {
                    cell.AddToClassList("mk-cell--t");
                    cell.style.borderLeftWidth = cell.style.borderRightWidth = cell.style.borderTopWidth = cell.style.borderBottomWidth = 0;
                    cell.generateVisualContent += DrawLostCell;
                }
                cells.Add(cell);
            }
            line.Add(cells);
            return line;
        }

        private static void DrawLostCell(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            var painter = context.painter2D;
            painter.strokeColor = context.visualElement.resolvedStyle.borderLeftColor;
            painter.lineWidth = 2;
            void Dashes(Vector2 a, Vector2 b)
            {
                float length = Vector2.Distance(a, b);
                for (float offset = 0; offset < length; offset += 6)
                {
                    painter.BeginPath();
                    painter.MoveTo(Vector2.Lerp(a, b, offset / length));
                    painter.LineTo(Vector2.Lerp(a, b, Math.Min(offset + 3, length) / length));
                    painter.Stroke();
                }
            }
            Dashes(new Vector2(1, 1), new Vector2(rect.width - 1, 1));
            Dashes(new Vector2(1, rect.height - 1), new Vector2(rect.width - 1, rect.height - 1));
            Dashes(new Vector2(1, 1), new Vector2(1, rect.height - 1));
            Dashes(new Vector2(rect.width - 1, 1), new Vector2(rect.width - 1, rect.height - 1));
        }

        private async Task UseItemAsync()
        {
            if (!session.IsOpen || inventoryBusy || inspectedItem == null || panelBaseId == null) return;
            int revision = panelRevision;
            string baseId = panelBaseId, choiceId = panelChoiceId, skinId = panelSkinId;
            bool Current() => session.IsOpen && inventorySheet.IsOpen && revision == panelRevision && panelBaseId == baseId;
            if (!catalog.UnlockForBase(inspectedItem.ID, playerState).CanUse) return;
            var variants = LoadoutChoices.Variants(baseId, playerState);
            int variant = Array.FindIndex(variants, choice => choice.ID == choiceId);
            if (variant >= 0 && !variants[variant].Equipped)
            {
                if (!variants[variant].Selectable || !await EquipVariantAsync(baseId, choiceId) || !Current()) return;
            }
            var skins = LoadoutChoices.Skins(baseId, null, playerState);
            int skin = Array.FindIndex(skins, choice => choice.ID == skinId);
            if (skin < 0) return;
            if (!skins[skin].Equipped)
            {
                if (!skins[skin].Selectable || !await EquipSkinAsync(baseId, skinId) || !Current()) return;
            }
            if (!Current()) return;
            closingForUse = true; CloseItemPanel(); closingForUse = false;
            UsePreview();
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

        private static Label MakeLabel(string text, params string[] classes)
        {
            Label label = new Label(text) { enableRichText = false };
            foreach (var styleClass in classes) label.AddToClassList(styleClass);
            label.pickingMode = PickingMode.Ignore;
            return label;
        }
    }
}
