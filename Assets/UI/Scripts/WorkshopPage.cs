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
            public bool Granted, IsSuit;
        }
        private readonly LoadoutCatalog catalog;
        private readonly LobbyToast toast;
        private readonly LobbyContextMenu menu;
        private readonly List<Item> districts = new List<Item>(), suits = new List<Item>();
        private readonly Button segDistricts, segSuits;
        private readonly SegmentedSwitch tabs, ownership;
        private readonly VisualElement unlocked;
        private readonly Label eraMessage;
        private readonly ScrollView grid;
        private readonly VisualElement firstPanel, panelSpace, stageWindow, secondPanel, secondSpace, secondClip;
        private readonly VisualElement navClearance = new VisualElement { pickingMode = PickingMode.Ignore };
        private float stageBandHeight = 220f;
        private VisualElement nav;

        private readonly Button ownedFilter, allFilter, sortFilter;
        private readonly Button[] presets = new Button[LoadoutPresets.Count];
        private readonly Label presetLabel;
        private MkPanel clearConfirmation;
        private int clearPreset;
        private bool closingForUse;
        private bool showAll;
        private LoadoutSort sort;
        private PlayerState playerState => session.State;
        private bool equipmentSaving;
        private bool ownershipKnown => RememberGuard.HasInventoryState(playerState);
        private Item inspectedItem;
        private readonly WorkshopInventorySession session = new WorkshopInventorySession();

        private readonly MkSheet inventorySheet = new MkSheet();
        private string panelBaseId, panelChoiceId, panelSkinId;
        private int panelRevision;
        private readonly VisualElement itemActions = new VisualElement();

        private bool inventoryBusy => equipmentSaving;
        private string inventoryMessage = "";
        private Task inventoryIdle = Task.CompletedTask;
        private LoadoutScreenModel loadout = new LoadoutScreenModel(LoadoutData.CreateEmpty());
        private Tab activeTab { get => loadout.BrowsingSuits ? Tab.Suits : Tab.Districts; set => loadout.Browse(value == Tab.Suits); }
        public bool IsOpen => session.IsOpen;
        public VisualElement StageWindow => stageWindow;
        public float StageViewportTop => grid.contentViewport.worldBound.y;
        public event Action Hidden;
        public event Action<bool> ViewChanged;
        public bool ReplacingSuit => loadout.ReplacingSuit;
        public bool Replacing => loadout.Replacing;
        public LoadoutData StageLoadout => loadout.ToData();
        public string[] StageGrants(bool suit)
        {
            int arena = playerState?.Rank?.Arena ?? 0;
            return suit ? LoadoutRules.LockedSuits(arena) : LoadoutRules.LockedDistricts(arena);
        }
        public int StageDistrictCapacity => LoadoutRules.DisplayDistrictSlots;
        public bool ViewingSuit => activeTab == Tab.Suits;
        public bool InspectedSuit => inspectedItem?.IsSuit ?? ViewingSuit;
        public string ViewedId => inspectedItem?.ID;
        public event Action<bool, string> ItemViewed;
        public event Action LoadoutChanged;
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
            tabs = SegmentedSwitch.Replace(segDistricts.parent.parent, segDistricts, segSuits);
            ownership = SegmentedSwitch.Replace(ownedFilter.parent.parent, ownedFilter, allFilter);
            ownership.AddToClassList("ws-ownership");
            sortFilter = Root.Q<Button>("workshop-sort");
            presetLabel = Root.Q<Label>("workshop-preset-label");
            ownedFilter.clicked += () => { showAll = false; RenderCards(); };
            allFilter.clicked += () => { showAll = true; RenderCards(); };
            sortFilter.clicked += () => { sort = (LoadoutSort)(((int)sort + 1) % 3); RenderCards(); };
            for (int i = 0; i < presets.Length; i++)
            {
                int index = i;
                presets[i] = Root.Q<Button>("workshop-preset-" + i);
                presets[i].clicked += () => SwitchPreset(index);
            }
            var clear = Root.Q<Button>("workshop-clear");
            clear.name = "Clear loadout";
            clear.Insert(0, new LobbyIcon(LobbyIconKind.Close));
            clear.clicked += ConfirmClear;
            Root.Q<Button>("workshop-replace-cancel").clicked += CancelReplacement;
            RenderReplacement();
            if (segDistricts != null) segDistricts.clicked += () => SetTab(Tab.Districts);
            if (segSuits != null) segSuits.clicked += () => SetTab(Tab.Suits);
            Root.RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == Root) CloseInventory(); });
            CollectItems();
            // Build every tile (and bake its art) now, so showing the page never does.
            RenderCards();
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
            BackendServices.Account.Changed -= AccountChanged; BackendServices.Account.Changed += AccountChanged;
            if (panelBaseId != null || inventorySheet.IsOpen) CloseItemPanel();
            session.AcceptState(BackendServices.LastKnownState);
            CollectItems(); LoadFromProfile(); Inspect(null); Render();
            UpdateScrollLayout();
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
            if (grid == null) return;
            float navHeight = nav?.resolvedStyle.height ?? 0;
            navClearance.style.height = float.IsNaN(navHeight) ? 0 : navHeight;
            if (Replacing && grid.scrollOffset != Vector2.zero) grid.scrollOffset = Vector2.zero;
            if (!Mathf.Approximately(stageWindow.resolvedStyle.height, stageBandHeight)) stageWindow.style.height = stageBandHeight;
            // Header and card heights never depend on a layout pass: the header is a fixed size and the
            // card height is remembered from the last measurement (or estimated before the first one).
            float cardHeight = secondPanel.layout.height;
            if (!float.IsNaN(cardHeight) && cardHeight > 0) measuredCardHeight = cardHeight;
            else if (measuredCardHeight <= 0) measuredCardHeight = 9f + 24f + 52f + 12f + Mathf.Ceil(Mathf.Max(1, unlocked.childCount) / 2f) * 160f;
            float header = WorkshopLayout.HeaderHeight;
            if (!Mathf.Approximately(panelSpace.resolvedStyle.height, header)) panelSpace.style.height = header;
            var clip = WorkshopLayout.Clip(stageBandHeight, header, measuredCardHeight, grid.scrollOffset.y);
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
                    IsSuit = true, Granted = !catalog.IsSuitOffered(suit.suitID, arena)
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
            loadout = new LoadoutScreenModel(catalog.CurrentLoadout(playerState).ToLoadout(), LoadoutRules.ArenaOrFallback(playerState?.Rank));
            loadout.Preview(preview);
            activeTab = profile != null && profile.WorkshopTabIndex == (int)Tab.Suits ? Tab.Suits : Tab.Districts;
            // Until rank is confirmed, fallback display must not trim a saved high deck.
            if (ownershipKnown) SaveToProfile();
        }
        private void SwitchPreset(int index)
        {
            if (!session.IsOpen || inventoryBusy || !ownershipKnown) return;
            var profile = PlayerProfile.Instance;
            if (profile == null || profile.data.activeLoadoutPreset == index) return;
            CancelReplacement();
            CloseItemPanel();
            int arena = LoadoutRules.ArenaOrFallback(playerState?.Rank);
            var chosen = LoadoutPresets.Switch(ref profile.data, index, arena,
                id => catalog.IsSuitOffered(id, arena) && catalog.UnlockForBase(id, playerState).CanUse,
                id => catalog.IsDistrictOffered(id, arena) && catalog.UnlockForBase(id, playerState).CanUse);
            bool browsingSuits = ViewingSuit;
            loadout = new LoadoutScreenModel(chosen, arena);
            loadout.Browse(browsingSuits);
            profile.Save();
            LoadoutChanged?.Invoke();
            Render();
        }
        private void ConfirmClear()
        {
            if (!session.IsOpen || inventoryBusy || clearConfirmation == null) return;
            clearPreset = PlayerProfile.Instance != null ? PlayerProfile.Instance.data.activeLoadoutPreset : 0;
            clearConfirmation.Title.text = "Clear loadout " + (clearPreset + 1) + "?";
            clearConfirmation.Open();
        }
        public bool IsStageSlotLocked(bool suit, int slot) => loadout.IsSlotLocked(suit, slot);

        private void ClearConfirmed()
        {
            if (!session.IsOpen || inventoryBusy) return;
            var profile = PlayerProfile.Instance;
            if (profile != null && profile.data.activeLoadoutPreset != clearPreset) { clearConfirmation.Close(); return; }
            clearConfirmation.Close();
            CancelReplacement(); CloseItemPanel();
            // With no confirmed era, preserve every starter lock; never persist this fallback.
            int arena = playerState?.Rank?.Arena ?? 0;
            bool browsingSuits = ViewingSuit;
            loadout = new LoadoutScreenModel(loadout.ToData(), arena);
            loadout.Browse(browsingSuits);
            loadout.ClearChoices(); SaveToProfile(); Render();
            toast?.Show("Loadout " + (clearPreset + 1) + " cleared");
        }
        private void SaveToProfile()
        {
            if (!ownershipKnown) { LoadoutChanged?.Invoke(); return; }
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
            tabs.Select((int)activeTab);
            var items = activeTab == Tab.Suits ? suits : districts;
            SortByFamily(items);
            Item item = Find(inspectedItem?.IsSuit == true ? suits : districts, loadout.PreviewId);
            Inspect(item);
            RenderCards();
            var profile = PlayerProfile.Instance;
            int selected = profile != null ? profile.data.activeLoadoutPreset : 0;
            for (int i = 0; i < presets.Length; i++)
            {
                presets[i].EnableInClassList("ws-preset--on", selected == i);
            }
            int arena = LoadoutRules.ArenaOrFallback(playerState?.Rank);
            if (presetLabel != null) presetLabel.text = "LOADOUT " + (selected + 1) + " \u00b7 " + RankTable.Names[Mathf.Clamp(arena, 0, RankTable.Names.Count - 1)];
        }
        private static VisualElement MakeArt(Item item, bool neutral, bool navy, int variant = 0, string skinId = null)
        {
            var clip = new VisualElement { pickingMode = PickingMode.Ignore };
            clip.AddToClassList("ws-art-clip"); clip.AddToClassList("mk-art");
            var art = new VisualElement { pickingMode = PickingMode.Ignore };
            art.AddToClassList("ws-art");
            art.EnableInClassList("ws-art--neutral", neutral); art.EnableInClassList("ws-art--navy", navy);
            // The generated paper art carries the item's tint, so the box stays neutral behind it.
            StyleBackground paper = item == null ? new StyleBackground(StyleKeyword.None) : LobbyPaperArt.BackgroundFor(item.ID, variant);
            if (paper.keyword != StyleKeyword.None)
            {
                art.style.backgroundImage = paper;
                art.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                if (!string.IsNullOrEmpty(skinId))
                    art.style.unityBackgroundImageTintColor = Color.Lerp(Color.white, StagePaperArt.Tints[ItemTint.IndexFor(ItemTint.SkinKey(item.ID, skinId))], 0.5f);
            }
            else
            {
                if (item != null) art.AddToClassList(ItemTint.ClassFor(string.IsNullOrEmpty(skinId) ? item.ID : ItemTint.SkinKey(item.ID, skinId)));
                art.Add(MakeLabel(item == null ? "+" : ItemTint.MonogramFor(item.Name, item.ID), "ws-monogram", "mk-h2"));
            }
            if (item != null && !string.IsNullOrEmpty(skinId))
            {
                var badge = MakeLabel(ItemTint.SkinBadge(skinId), "ws-monogram");
                badge.style.position = Position.Absolute; badge.style.right = 3; badge.style.bottom = 1; badge.style.fontSize = 12;
                clip.Add(art); clip.Add(badge); return clip;
            }
            clip.Add(art); return clip;
        }
        // Tiles are built once per item, with their art, and kept for the page's lifetime. Refreshing
        // only flips classes and text in place, so showing the page never rebuilds or re-lays-out them.
        private sealed class Tile
        {
            public Tab Tab;
            public string ID;
            public VisualElement Cell, Art, Starter;
            public Button Card;
            public Label Name, Meta, Mark;
        }
        private readonly Dictionary<string, Tile> tiles = new Dictionary<string, Tile>();
        private VisualElement emptyCell;
        private float measuredCardHeight;

        private static string TileKey(Tab tab, string id) => (int)tab + ":" + id;

        private void EnsureTiles()
        {
            foreach (Item item in districts) EnsureTile(Tab.Districts, item);
            foreach (Item item in suits) EnsureTile(Tab.Suits, item);
            if (emptyCell == null)
            {
                emptyCell = new VisualElement { pickingMode = PickingMode.Ignore };
                emptyCell.AddToClassList("ws-empty-cell");
                emptyCell.Add(MakeLabel("Nothing here. Try All.", "ws-empty", "mk-muted"));
            }
        }

        private void EnsureTile(Tab tab, Item item)
        {
            string key = TileKey(tab, item.ID);
            if (tiles.ContainsKey(key)) return;
            string id = item.ID;
            var tile = new Tile { Tab = tab, ID = id };
            tile.Cell = new VisualElement { pickingMode = PickingMode.Ignore };
            tile.Cell.AddToClassList("ws-cell");
            tile.Card = new Button(() => PreviewItem(id));
            tile.Card.AddToClassList("mk-tile"); tile.Card.AddToClassList("ws-card");
            var art = MakeArt(item, false, false);
            tile.Art = art.Q(className: "ws-art");
            tile.Card.Add(art);
            tile.Name = MakeLabel(item.Name, "ws-name", "mk-h3");
            tile.Meta = MakeLabel("", "mk-caption", "ws-tile-meta");
            tile.Mark = MakeLabel("", "mk-mark", "ws-mark");
            TrackedText.Track(tile.Mark);
            tile.Starter = new VisualElement { pickingMode = PickingMode.Ignore };
            tile.Starter.AddToClassList("ws-starter-tag");
            tile.Starter.Add(new LobbyIcon(LobbyIconKind.Lock, LobbyIconContext.Workshop));
            var starterText = MakeLabel("Locked", "ws-starter-text");
            TrackedText.Track(starterText);
            tile.Starter.Add(starterText);
            tile.Card.Add(tile.Starter);
            tile.Card.Add(tile.Name); tile.Card.Add(tile.Meta); tile.Card.Add(tile.Mark);
            if (menu != null) menu.Attach(tile.Card, () =>
            {
                var actions = new List<LobbyMenuItem> { new LobbyMenuItem("View details", () => PreviewItem(id)) };
                Item current = Find(tab == Tab.Suits ? suits : districts, id);
                if (current != null && tab == activeTab && IsSelected(current) && !current.Granted)
                    actions.Add(new LobbyMenuItem("Remove from loadout", () => RemoveItem(current)));
                return actions;
            });
            tile.Cell.Add(tile.Card);
            tiles[key] = tile;
        }

        private void RenderCards()
        {
            if (unlocked == null) return;
            EnsureTiles();
            ownership.Select(showAll ? 1 : 0);
            sortFilter.Q<Label>().text = "Sort: " + (sort == LoadoutSort.OwnedFirst ? "Era" : sort == LoadoutSort.Name ? "Name" : "Role");
            var items = activeTab == Tab.Suits ? suits : districts;
            SortByFamily(items);
            var wanted = new List<VisualElement>();
            foreach (Item item in items)
            {
                var state = DisplayUnlock(catalog.UnlockForBase(item.ID, playerState));
                bool owned = state.Status == UnlockStatus.Owned;
                if (!LoadoutBrowse.Includes(showAll, owned)) continue;
                Tile tile = tiles[TileKey(activeTab, item.ID)];
                tile.Card.pickingMode = PickingMode.Position;
                tile.Card.SetEnabled(true);
                bool preview = loadout.PreviewId == item.ID;
                tile.Card.EnableInClassList("mk-tile--selected", preview);
                tile.Card.EnableInClassList("ws-card--preview", preview);
                tile.Card.EnableInClassList("mk-tile--locked", !owned);
                tile.Art.EnableInClassList("ws-art--neutral", !owned);
                if (tile.Name.text != item.Name) tile.Name.text = item.Name;
                string meta = "Era " + state.UnlockEra;
                if (tile.Meta.text != meta) tile.Meta.text = meta;
                bool inHand = IsSelected(item);
                bool fixedStarter = IsFixedStarter(item);
                tile.Starter.style.display = fixedStarter ? DisplayStyle.Flex : DisplayStyle.None;
                string mark = fixedStarter ? "" : inHand ? "In hand" : !owned ? "Arena " + state.UnlockEra : "";
                if (tile.Mark.text != mark) TrackedText.SetText(tile.Mark, mark);
                tile.Mark.EnableInClassList("mk-mark--era", !inHand && !owned);
                tile.Mark.style.display = mark.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                wanted.Add(tile.Cell);
            }
            if (wanted.Count == 0) wanted.Add(emptyCell);
            bool same = unlocked.childCount == wanted.Count;
            for (int i = 0; same && i < wanted.Count; i++) same = unlocked[i] == wanted[i];
            if (!same)
            {
                unlocked.Clear();
                foreach (var cell in wanted) unlocked.Add(cell);
            }
            UpdateScrollLayout();
        }

        public void InspectStageItem(bool suit, int stageIndex)
        {
            if (!session.IsOpen || Replacing) return;
            var chosen = suit ? StageLoadout.suitIDs : StageLoadout.districtIDs;
            string id = StageSlotMap.IdAt(chosen, StageGrants(suit), stageIndex);
            if (id.Length > 0) PreviewItem(id);
        }

        private void PreviewItem(string id)
        {
            Inspect(Find(suits, id) ?? Find(districts, id));
            RenderCards();
            if (inspectedItem != null) OpenItemPanel();
        }
        private bool IsSelected(Item item)
        {
            if (item == null) return false;
            var data = loadout.ToData();
            return item.Granted || Array.IndexOf(item.IsSuit ? data.suitIDs : data.districtIDs, item.ID) >= 0;
        }
        private void UsePreview()
        {
            if (inspectedItem == null || IsSelected(inspectedItem) || inventoryBusy || Replacing) return;
            // Actual eligibility is independent of the debug display override.
            if (!catalog.UnlockForBase(inspectedItem.ID, playerState).CanUse || inspectedItem.Granted) return;
            closingForUse = true; CloseItemPanel(); closingForUse = false;
            if (!loadout.Use(true, inventoryBusy))
            {
                if (Replacing) { RenderReplacement(); ReplacementChanged?.Invoke(); }
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
            // Only failures take room on the page; "Loading" and "Saving" must not shift the layout.
            bool failed = inventoryMessage.StartsWith("Couldn", StringComparison.Ordinal);
            if (eraMessage != null) { eraMessage.text = failed ? inventoryMessage : ""; eraMessage.style.display = failed ? DisplayStyle.Flex : DisplayStyle.None; }
            RenderCards(); RenderItemPanel();
        }
        private void CloseInventory()
        {
            BackendServices.Account.Changed -= AccountChanged;
            clearConfirmation?.Close();
            CancelReplacement();
            ItemViewed?.Invoke(false, null);
            CloseItemPanel();
            session.Close();
        }

        private void AccountChanged(AccountInfo account)
        {
            if (!session.IsOpen) return;
            // Responses issued for the previous account must neither load nor save into this one.
            int visit = session.InvalidateAccount();
            clearConfirmation?.Close(); CancelReplacement(); CloseItemPanel();
            session.AcceptState(BackendServices.LastKnownState);
            CollectItems(); LoadFromProfile(); Inspect(null); Render();
            _ = LoadInventoryAsync(visit);
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
            loadout.Inspect(item?.ID, item?.IsSuit ?? ViewingSuit);
            RenderInventory();
            ItemViewed?.Invoke(item?.IsSuit ?? ViewingSuit, item?.ID);
        }

        private async Task LoadInventoryAsync(int visit)
        {
            // A reopened page must not fetch a snapshot before an earlier save finishes.
            await inventoryIdle;
            if (!IsInventoryActive(visit) || !session.TryBeginRequest(out InventoryRequestToken token)) return;
            var completion = new TaskCompletionSource<bool>();
            inventoryIdle = completion.Task;
            inventoryMessage = ownershipKnown ? "" : "Loading inventory...";
            RenderInventory();
            bool retry = false;
            try
            {
                PlayerState result = await BackendServices.PlayerState.GetAsync();
                if (!IsInventoryActive(visit)) return;
                if (!session.AcceptState(result)) throw new InvalidOperationException("Incomplete player state");
                CollectItems();
                LoadFromProfile();
                Render();
                inventoryMessage = "";
            }
            catch (Exception)
            {
                if (IsInventoryActive(visit))
                {
                    inventoryMessage = "";
                    retry = true;
                    if (session.Failed()) Say("Couldn't refresh inventory. Retrying...");
                }
            }
            finally
            {
                session.EndRequest(token);
                completion.TrySetResult(true);
                if (IsInventoryActive(visit)) RenderInventory();
            }
            if (retry)
            {
                await Task.Delay(session.RetrySeconds * 1000);
                if (IsInventoryActive(visit)) _ = LoadInventoryAsync(visit);
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
            equipmentSaving = true;
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
                if (!session.AcceptState(result)) throw new InvalidOperationException("Incomplete player state");
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
                    if (session.Failed()) Say("Couldn't refresh inventory. Retrying...");
                }
            }
            finally
            {
                bool visitCurrent = session.IsVisitCurrent(token);
                bool current = session.IsCurrent(token);
                session.EndRequest(token);
                equipmentSaving = false;
                completion.TrySetResult(true);
                if (visitCurrent)
                {
                    if (!current)
                    {
                        // Discard the old selection's response and get a fresh snapshot.
                        _ = LoadInventoryAsync(visit);
                    }
                    else
                    {
                        inventoryMessage = "";
                        RenderInventory();
                        if (!applied) _ = RetryInventoryAsync(visit);
                    }
                }
            }
            return applied;
        }

        private async Task RetryInventoryAsync(int visit)
        {
            await Task.Delay(Math.Max(2, session.RetrySeconds) * 1000);
            if (IsInventoryActive(visit)) await LoadInventoryAsync(visit);
        }

        // ===== ITEM SHEET =====

        public void BindOverlayRouter(LobbyOverlayRouter router, VisualElement shell)
        {
            clearConfirmation = new MkPanel { name = "workshop-clear-confirmation" };
            clearConfirmation.BindRouter(router, "workshop-clear");
            clearConfirmation.styleSheets.Add(Resources.Load<StyleSheet>("Lobby/Workshop"));
            clearConfirmation.Surface.AddToClassList("ws-clear-confirmation");
            var clearBody = new Label("Removes your chosen districts and suits. Locked starters stay.") { enableRichText = false };
            clearBody.AddToClassList("ws-clear-body");
            clearConfirmation.Body.Add(clearBody);
            var clearActions = new VisualElement(); clearActions.AddToClassList("ws-clear-actions");
            clearActions.Add(SheetButton("Cancel", clearConfirmation.Close));
            clearActions.Add(SheetButton("Clear", ClearConfirmed, "mk-btn--urgent"));
            clearConfirmation.Body.Add(clearActions);
            shell.Add(clearConfirmation);
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
            bool fixedStarter = IsFixedStarter(inspectedItem);
            if (fixedStarter)
            {
                string note = "Locked starter for your current era.";
                var starterNote = MakeLabel(note, "ws-starter-note");
                starterNote.tooltip = note;
                content.Add(starterNote);
            }
            var artRow = new VisualElement(); artRow.AddToClassList("ws-item-art");
            var art = new VisualElement(); art.AddToClassList("ws-artbox");
            art.Add(MakeArt(inspectedItem, state.Status != UnlockStatus.Owned, state.Status == UnlockStatus.CannotUnlock, variant));
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
            previous.Add(ArrowGlyph("ws-arr-glyph--prev")); next.Add(ArrowGlyph("ws-arr-glyph--next"));
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
            if (inspectedItem.IsSuit)
                rows = StatHistogram.SuitRows(LoadoutInfoModel.SuitStats(balance, baseId, variant), LoadoutInfoModel.SuitStats(balance, baseId, 0));
            else if (Enum.TryParse(inspectedItem.ID.Substring(5), true, out DistrictType district))
                rows = StatHistogram.DistrictRows(balance, district, variant);
            else rows = Array.Empty<HistogramRow>();
            if (inspectedItem.IsSuit && rows.Length > 0) histogram.Add(StatLegend());
            foreach (var row in rows) histogram.Add(HistogramLine(row));

            string description = inspectedItem.Description ?? "";
            if (!inspectedItem.IsSuit && Enum.TryParse(inspectedItem.ID.Substring(5), true, out DistrictType type))
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
                var skinArt = MakeArt(inspectedItem, !choice.UnlockState.CanUse, false, 0, skinId);
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
            if (!fixedStarter && IsSelected(inspectedItem))
            {
                var remove = SheetButton("Remove", () => RemoveItem(inspectedItem));
                remove.SetEnabled(ownershipKnown && !inventoryBusy);
                itemActions.Add(remove);
            }
            var use = SheetButton(fixedStarter ? "Fixed" : "USE", () => _ = UseItemAsync(), "mk-btn--primary");
            bool canUse = catalog.UnlockForBase(inspectedItem.ID, playerState).CanUse &&
                (index < 0 || choices[index].Equipped || choices[index].Selectable);
            bool skinValid = LoadoutScreenModel.ValidChoice(skins, panelSkinId);
            bool changes = !IsSelected(inspectedItem) || index >= 0 && !choices[index].Equipped ||
                Array.Exists(skins, choice => choice.ID == panelSkinId && !choice.Equipped);
            use.tooltip = !canUse ? "Locked - own this item and reach its required arena to use it." : !skinValid ? "Selected skin is locked." : "";
            use.SetEnabled(!fixedStarter && ownershipKnown && !inventoryBusy && canUse && skinValid && changes); itemActions.Add(use);
        }

        private bool IsFixedStarter(Item item) => Array.IndexOf(StageGrants(item.IsSuit), item.ID) >= 0;

        private void RemoveItem(Item item)
        {
            if (item == null || !session.IsOpen || inventoryBusy || !ownershipKnown || IsFixedStarter(item)) return;
            CancelReplacement();
            var chosen = item.IsSuit ? StageLoadout.suitIDs : StageLoadout.districtIDs;
            if (!loadout.ClearChosen(item.IsSuit, Array.IndexOf(chosen, item.ID))) return;
            SaveToProfile(); Render();
        }

        private int CurrentEra(Item item)
        {
            var data = loadout.ToData();
            var eras = item.IsSuit ? data.suitEras : data.districtEras;
            int type = DistrictMigration.SourceType(item.ID);
            if (item.IsSuit && Enum.TryParse(item.ID.Substring(5), true, out SuitType suit)) type = (int)suit;
            return type >= 0 && type < eras.Length ? eras[type] : 0;
        }

        private static VisualElement HistogramLine(HistogramRow row)
        {
            var line = new VisualElement { tooltip = "1 cell = " + row.Unit };
            line.AddToClassList("mk-hrow"); line.AddToClassList("ws-stat-row");
            var body = new VisualElement(); body.AddToClassList("ws-stat-body");
            body.Add(MakeLabel(row.Label, "ws-stat-label"));
            var cells = new VisualElement(); cells.AddToClassList("mk-cells"); cells.AddToClassList("ws-stat-cells");
            for (int i = 0; i < StatHistogram.MaxCells; i++)
            {
                StatSegment segment = row.Segments != null ? row.Segments[i] : StatSegment.Empty;
                var cell = MakeLabel("", "mk-cell", "ws-stat-cell");
                if (segment == StatSegment.Villager) cell.AddToClassList("mk-cell--f");
                else if (segment == StatSegment.BaseSuit) cell.AddToClassList("mk-cell--b");
                else if (segment == StatSegment.Gain) cell.AddToClassList("mk-cell--g");
                else if (segment == StatSegment.Loss)
                {
                    cell.AddToClassList("mk-cell--t");
                    cell.style.borderLeftWidth = cell.style.borderRightWidth = cell.style.borderTopWidth = cell.style.borderBottomWidth = 0;
                    cell.generateVisualContent += DrawLostCell;
                }
                cells.Add(cell);
            }
            body.Add(cells); line.Add(body);
            var delta = MakeLabel(row.Delta == 0 ? "0" : (row.Delta > 0 ? "+" : "") + row.Delta, "ws-stat-delta");
            delta.AddToClassList(row.Delta == 0 ? "ws-stat-delta--same" : row.Better ? "ws-stat-delta--up" : "ws-stat-delta--down");
            line.Add(delta);
            return line;
        }

        private static VisualElement ArrowGlyph(string side)
        {
            var glyph = new VisualElement { pickingMode = PickingMode.Ignore };
            glyph.AddToClassList("ws-arr-glyph"); glyph.AddToClassList(side); return glyph;
        }

        private static VisualElement StatLegend()
        {
            var legend = new VisualElement(); legend.AddToClassList("ws-legend");
            foreach (var entry in new[] { ("Villager", "mk-cell--f"), ("Base suit", "mk-cell--b"), ("This suit", "mk-cell--g") })
            {
                var item = new VisualElement(); item.AddToClassList("ws-legend-item");
                var swatch = new VisualElement(); swatch.AddToClassList("ws-legend-swatch"); swatch.AddToClassList(entry.Item2);
                item.Add(swatch); item.Add(MakeLabel(entry.Item1, "ws-legend-label")); legend.Add(item);
            }
            return legend;
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
            if (!session.IsOpen || !ownershipKnown || inventoryBusy || inspectedItem == null || panelBaseId == null) return;
            int revision = panelRevision;
            string baseId = panelBaseId, choiceId = panelChoiceId, skinId = panelSkinId;
            bool Current() => session.IsOpen && inventorySheet.IsOpen && revision == panelRevision && panelBaseId == baseId;
            if (!catalog.UnlockForBase(inspectedItem.ID, playerState).CanUse) return;
            var variants = LoadoutChoices.Variants(baseId, playerState);
            int variant = Array.FindIndex(variants, choice => choice.ID == choiceId);
            if (!LoadoutScreenModel.ValidChoice(variants, choiceId)) return;
            if (variant >= 0 && LoadoutScreenModel.NeedsEquipment(variants[variant]))
            {
                if (!variants[variant].Selectable || !await EquipVariantAsync(baseId, choiceId) || !Current()) return;
            }
            var skins = LoadoutChoices.Skins(baseId, null, playerState);
            int skin = Array.FindIndex(skins, choice => choice.ID == skinId);
            if (!LoadoutScreenModel.ValidChoice(skins, skinId)) return;
            if (skin >= 0 && LoadoutScreenModel.NeedsEquipment(skins[skin]))
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
