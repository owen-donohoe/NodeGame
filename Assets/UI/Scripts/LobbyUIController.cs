using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Root of the UI Toolkit lobby. Attached alongside a UIDocument.
    ///
    /// This is the counterpart to LobbyManager, and during the migration both
    /// exist. LobbyManager still owns match launching, PlayerProfile creation
    /// and the uGUI panels; this owns only the new shell. Which one is live is
    /// decided by LobbyManager.useUIToolkitLobby, so the old lobby is one
    /// checkbox away for as long as the migration runs.
    ///
    /// What it builds, in the layer order of LobbyRoot.uxml:
    ///   - the persistent chrome (LobbyChrome) and the tab track
    ///     (NavigationController) with Shop, Loadout, Home, Social and Guild;
    ///   - the overlays above the chrome: Profile, which expands from the
    ///     trophy strip, and the push pages, Settings and Match history;
    ///   - the shared machinery every page is handed rather than builds for
    ///     itself: one sheet, one context menu, one toast.
    ///
    /// It reads no simulation state and writes none - the lobby runs in its own
    /// scene before any SimulationState exists. The view/UI boundary in
    /// .claude/rules/view-ui.md has nothing to bite on here.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LobbyUIController : MonoBehaviour
    {
        [Tooltip("The lobby shell layout. Assign LobbyRoot.uxml.")]
        [SerializeField] private VisualTreeAsset rootLayout;

        [Header("Pages")]
        [Tooltip("HomePage.uxml. Without it Home falls back to a placeholder.")]
        [SerializeField] private VisualTreeAsset homePageLayout;

        [Tooltip("PlayPopup.uxml. Without it the Play button does nothing.")]
        [SerializeField] private VisualTreeAsset playPopupLayout;

        [Tooltip("WorkshopPage.uxml. Without it Workshop falls back to a placeholder.")]
        [SerializeField] private VisualTreeAsset workshopPageLayout;

        [Tooltip("ProfilePage.uxml. Without it Profile falls back to a placeholder.")]
        [SerializeField] private VisualTreeAsset profilePageLayout;

        [Tooltip("ShopPage.uxml. Without it Shop falls back to a placeholder.")]
        [SerializeField] private VisualTreeAsset shopPageLayout;

        [Tooltip("SocialPage.uxml. Without it Social falls back to a placeholder.")]
        [SerializeField] private VisualTreeAsset socialPageLayout;

        [Tooltip("SettingsPage.uxml. Without it Settings shows a labelled note.")]
        [SerializeField] private VisualTreeAsset settingsPageLayout;

        [Tooltip("MatchHistoryPage.uxml. Without it Match history shows a labelled note.")]
        [SerializeField] private VisualTreeAsset matchHistoryPageLayout;

        [Header("Draftable items")]

        // The Workshop's catalogue. GroupSelectionPanel holds the same assets in
        // its own [SerializeField] arrays, wired in Lobby.unity; the new stack
        // gets its own copy rather than reading the old panel, so neither
        // depends on the other surviving. The project uses no Resources folder
        // and Assets/Data is not one, so a serialized reference is the only way
        // to reach a ScriptableObject at runtime here - the setup menu item
        // fills both arrays from Assets/Data/Lobby.
        [Tooltip("All SuitDefinitions. Filled by Tools > Node War > Set Up UI Toolkit Lobby.")]
        [SerializeField] private SuitDefinition[] allSuits;

        [Tooltip("All NodeDefinitions. Filled by Tools > Node War > Set Up UI Toolkit Lobby.")]
        [FormerlySerializedAs("allNodes")] [SerializeField] private DistrictDefinition[] allDistricts;

        [Header("Links")]
        [Tooltip("Used to start Bot and Testing matches. Found automatically if left empty.")]
        [SerializeField] private LobbyManager lobbyManager;

        [Tooltip("Log what the shell wired up on start. Off for normal play.")]
        [SerializeField] private bool verboseLogging;

        private UIDocument document;
        private NavigationController navigation;
        private SafeAreaBinder safeArea;
        private LoadoutCatalog catalog;
        private LobbyToast toast;
        private LobbySheet sheet;
        private LobbyContextMenu menu;
        private LobbyChrome chrome;
        private PlayPopup playPopup;
        private ProfilePage profilePage;
        private SettingsPage settingsPage;
        private AccountFlow accountFlow;
        private VisualElement lobbyRoot;
        private MatchHistoryPage matchHistoryPage;
        private GuildPage guildPage;
        private LobbyOverlayRouter router;
        private ClaimPanel claimPanel;
        private CrateScreen crateScreen;
        private bool confirmingExit;
        private LobbyStage stage;
        private string[] stageDistrictIds;
        private string[] stageSuitIds;
        private int[] stageDistrictTypes;
        private int[] stageSuitTypes;
        private LobbyOverlayPanel burgerPanel;
        private LobbyOverlayPanel bellPanel;
        private readonly string[] notifications = System.Array.Empty<string>();
        private LocalCrateInventory crateInventory;
        private HomePage homePage;
        private LobbyModePicker modePicker;
        private LobbyModeSelection selectedMode;
        private LobbyPlayFlow playFlow;
        private PrivateLobbyPanel privateLobbyPanel;
        private SearchingScreen searchingScreen;
        private MultiplayerSheet multiplayerSheet;
        private WorkshopStageBinding workshopStage;
        private VisualElement safeAreaRoot;
        private VisualElement pillDock;
        private LobbyMouseDrag mouseDrag;
        public NodeWar.Backend.ICrateInventory Crates => crateInventory;

        public LobbyRouteStack Routes => router?.Stack;
        public ClaimPanel Claims => claimPanel;
        public BadgeBoard Badges { get; private set; }

        /// <summary>
        /// Page switching, for pages to navigate between themselves. Null until
        /// OnEnable has run.
        /// </summary>
        public NavigationController Navigation
        {
            get { return navigation; }
        }

        /// <summary>
        /// Fetched once per lobby visit so the next match launches with the
        /// eras the server has equipped (BackendServices.LastKnownState), even
        /// if the player never opens the Workshop. Offline is not an error
        /// here: the match then plays era 0.
        /// </summary>
        private static async System.Threading.Tasks.Task RefreshPlayerStateAsync()
        {
            try
            {
                await NodeWar.Backend.BackendServices.PlayerState.GetAsync();
                // A ranked log that failed to upload last time goes now. A report
                // that settles returns the new state, which BackendServices keeps.
                await NodeWar.Backend.PendingRankedReports.RetryAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[LobbyUI] Could not fetch player state: " + e.Message);
            }
        }

        private void OnEnable()
        {
            document = GetComponent<UIDocument>();

            if (document.panelSettings == null)
            {
                Debug.LogError("[LobbyUI] UIDocument has no PanelSettings; nothing will render. " +
                               "Run Tools > Node War > Set Up UI Toolkit Lobby.");
                return;
            }

            VisualElement root = document.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("[LobbyUI] UIDocument has no root visual element.");
                return;
            }

            root.Clear();

            // The layout can come from either the UIDocument's own sourceAsset
            // or this component's field. Preferring the field keeps the two
            // from disagreeing silently when only one is set.
            VisualTreeAsset layout = rootLayout != null ? rootLayout : document.visualTreeAsset;
            if (layout == null)
            {
                Debug.LogError("[LobbyUI] No LobbyRoot.uxml assigned, on either this " +
                               "component or the UIDocument.");
                return;
            }

            layout.CloneTree(root);

            _ = RefreshPlayerStateAsync();

            BuildShell(root);
        }

        private void OnDisable()
        {
            if (mouseDrag != null) document?.rootVisualElement?.RemoveManipulator(mouseDrag);
            mouseDrag = null;
            NodeWar.Backend.BackendServices.StateChanged -= RefreshStageLoadout;
            homePage?.OnHide();
            homePage = null;
            searchingScreen?.Dispose(); searchingScreen = null; multiplayerSheet = null;
            privateLobbyPanel?.Dispose(); privateLobbyPanel = null;
            playFlow?.Dispose();
            playFlow = null;
            workshopStage?.Dispose();
            workshopStage = null;
            if (crateInventory != null) crateInventory.Changed -= RefreshCrateBadges;
            crateInventory = null;
            safeAreaRoot = pillDock = null;
            Application.deepLinkActivated -= HandleDeepLink;
            router?.CloseAll();
            burgerPanel = bellPanel = null;
            if (stage != null)
            {
                stage.SetVisible(false);
                Destroy(stage.gameObject);
            }
            stage = null;
            stageDistrictIds = stageSuitIds = null;
            stageDistrictTypes = stageSuitTypes = null;
            // Dispose before dropping the reference: the popup may own a live
            // socket, and leaving one open would collide with the next attempt.
            if (playPopup != null) playPopup.Dispose();

            playPopup = null;
            modePicker = null;
            profilePage = null;

            // Before dropping it: a slider moved without the page being closed
            // has not reached the profile yet, and this is the last chance to
            // write it. Unsubscribe first so the flush cannot drive a tree that
            // is about to be rebuilt.
            if (settingsPage != null)
            {
                settingsPage.Changed -= ApplyLobbySettings;
                settingsPage.Flush();
            }

            if (accountFlow != null) accountFlow.CloseDialog();
            accountFlow = null;

            settingsPage = null;
            lobbyRoot = null;
            matchHistoryPage = null;
            catalog = null;
            toast = null;
            sheet = null;
            menu = null;
            chrome = null;
            navigation = null;
            safeArea = null;
            router = null;
            claimPanel = null;
            Badges = null;
            confirmingExit = false;
        }

        private void LateUpdate()
        {
            var shop = lobbyRoot?.Q("page-shop")?.userData as ShopPage;
            shop?.Tick(Time.unscaledDeltaTime);
            navigation?.Tick(Time.unscaledDeltaTime);
            shop?.Project();
        }

        private void Update()
        {
            // Cheap: each returns immediately unless the safe area, the screen
            // or the panel size actually changed.
            if (safeArea != null) safeArea.Update();
            // The absolute dock needs the full safe area's inset in its anchor.
            if (pillDock != null && safeAreaRoot != null)
            {
                pillDock.style.bottom = safeAreaRoot.resolvedStyle.paddingBottom;
                pillDock.style.left = safeAreaRoot.resolvedStyle.paddingLeft;
                pillDock.style.right = safeAreaRoot.resolvedStyle.paddingRight;
            }
            crateInventory?.Refresh();
            if (profilePage != null) profilePage.Update();
            if (settingsPage != null) settingsPage.Update();
            if (matchHistoryPage != null) matchHistoryPage.Update();
            if (modePicker != null) modePicker.Update();

            // Pumps the connection state machine while the battle sheet is open.
            if (playPopup != null) playPopup.Update();
            playFlow?.Update();
            privateLobbyPanel?.Update();
            searchingScreen?.Update();
            burgerPanel?.Update();
            bellPanel?.Update();
            WatchStageLoadout();
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                HandleBack();
        }

        private void BuildShell(VisualElement root)
        {
            VisualElement safeAreaElement = root.Q<VisualElement>("safe-area");
            VisualElement pageHost = root.Q<VisualElement>("page-host");
            VisualElement overlayHost = root.Q<VisualElement>("overlay-host");

            if (safeAreaElement == null || pageHost == null || overlayHost == null)
            {
                Debug.LogError("[LobbyUI] LobbyRoot.uxml is missing #safe-area, #page-host " +
                               "or #overlay-host. The shell cannot be built.");
                return;
            }

            safeArea = new SafeAreaBinder(safeAreaElement);
            safeAreaRoot = safeAreaElement;
            pillDock = root.Q<VisualElement>("tabbar-dock");
            router = new LobbyOverlayRouter();
            Badges = new BadgeBoard();
            crateInventory = new LocalCrateInventory(
                () => PlayerProfile.Instance != null ? PlayerProfile.Instance.BoxesAvailable : 0,
                () => PlayerProfile.Instance != null ? PlayerProfile.Instance.BoxProgress : 0f,
                count =>
                {
                    var profile = PlayerProfile.Instance;
                    if (profile == null) return;
                    profile.data.boxesAvailable = count;
                    profile.Save();
                });
            crateInventory.Changed += RefreshCrateBadges;
            RefreshCrateBadges();
            navigation = new NavigationController(pageHost);
            navigation.BindSwipes(() => Routes != null && Routes.Count > 0);

            // Shared machinery, built before any page so every page gets the same.
            // The catalog is the one answer to what the loadout may hold and what
            // the player owns - Workshop, Home and the battle sheet all ask it.
            catalog = new LoadoutCatalog(allSuits, allDistricts);
            BuildStage(root);
            toast = new LobbyToast(root.Q<Label>("toast"));
            sheet = new LobbySheet(root);
            accountFlow = new AccountFlow(sheet);
            menu = new LobbyContextMenu(root);

            BuildOverlays(overlayHost);
            BuildChrome(root);
            BindLobbySettings(root);

            // Five tabs, in track order. Profile is reached from the player name.
            BindNav(root, "nav-shop", LobbyPageID.Shop);
            BindNav(root, "nav-workshop", LobbyPageID.Workshop);
            BindNav(root, "nav-home", LobbyPageID.Home);
            BindNav(root, "nav-social", LobbyPageID.Social);
            BindNav(root, "nav-guild", LobbyPageID.Guild);

            BuildPlayPopup();
            BuildModePicker();
            playFlow = new LobbyPlayFlow(lobbyRoot, router, playPopup, catalog, toast, NodeWar.Backend.BackendServices.MatchHistory);
            privateLobbyPanel = new PrivateLobbyPanel(lobbyRoot, router, catalog, menu, toast, profilePage.OpenFor, playFlow.OpenPrivateInvite);
            if (playPopup != null) playPopup.PrivateJoinRequested += privateLobbyPanel.Join;
            playFlow.PrivateRequested += privateLobbyPanel.Open;
            searchingScreen = new SearchingScreen(lobbyRoot, router, navigation, catalog, ShowShot);
            searchingScreen.PrivateRequested += privateLobbyPanel.Open;
            RegisterPages();
            WireOverlays(root);
            LobbyScrollbars.Hide(root);
            mouseDrag = new LobbyMouseDrag();
            root.AddManipulator(mouseDrag);

            navigation.Show(LobbyPageID.Home);
            Application.deepLinkActivated += HandleDeepLink;
            HandleDeepLink(Application.absoluteURL);

            if (verboseLogging)
                Debug.Log("[LobbyUI] Shell built. Current page: " + navigation.CurrentPageID);
        }

        private void BuildChrome(VisualElement root)
        {
            var layout = Resources.Load<VisualTreeAsset>("Lobby/Chrome");
            if (layout != null)
            {
                root.Q<VisualElement>("top-bar")?.RemoveFromHierarchy();
                root.Q<VisualElement>("strip-wrap")?.RemoveFromHierarchy();
                var header = new VisualElement { name = "lobby-chrome" };
                header.style.flexShrink = 0;
                layout.CloneTree(header);
                root.Q<VisualElement>("safe-area").Insert(0, header);
            }
            // The chrome is outside every page, so it is bound once, and it
            // refreshes whenever a page shows or the player is renamed.
            chrome = new LobbyChrome(root.Q<VisualElement>("lobby-chrome") ?? root, toast, Badges);
            chrome.CurrencyRequested += HandleDeepLink;
            navigation.PageShown += id => chrome.Refresh();

            VisualElement strip = root.Q<VisualElement>("strip-wrap");
            chrome.ProfileRequested += () => { if (profilePage != null) profilePage.Open(strip); };
            chrome.SettingsRequested += () => burgerPanel?.Open();
            chrome.HistoryRequested += OpenBell;

            if (profilePage != null) profilePage.Renamed += chrome.Refresh;
        }

        public void ShowShot(StageShot shot) => stage?.ShowShot(shot);

        private void BuildStage(VisualElement root)
        {
            stage = LobbyStage.Create();
            root.style.backgroundColor = Color.clear;
            VisualElement shell = root.Q<VisualElement>("lobby-root");
            shell.style.backgroundColor = Color.clear;
            shell.AddToClassList("lb-stage");
            var presentation = Resources.Load<StyleSheet>("Lobby/LobbyPresentation");
            if (presentation != null) shell.styleSheets.Add(presentation);
            VisualElement backdrop = root.Q<VisualElement>("backdrop");
            if (backdrop != null) backdrop.style.display = DisplayStyle.None;
            bool pageWasMoving = false, firstPagePose = true;
            navigation.MotionChanged += (from, to, progress, moving) =>
            {
                if (moving || pageWasMoving || firstPagePose)
                    stage.ApplyPageMotion(StageShots.ForPage(from.ToString()), StageShots.ForPage(to.ToString()), progress, moving);
                pageWasMoving = moving; firstPagePose = false;
            };
            navigation.PageShown += _ => RefreshStageLoadout();
            router.Stack.Changed += () => stage.SetVisible(!router.Stack.AnyFullscreen);
            NodeWar.Backend.BackendServices.StateChanged += RefreshStageLoadout;
            RefreshStageLoadout();
        }

        private void WatchStageLoadout()
        {
            if (stage == null) return;
            PlayerProfile profile = PlayerProfile.Instance;
            string[] districts = profile != null ? profile.data.loadout.nodeIDs : null;
            string[] suits = profile != null ? profile.data.loadout.suitIDs : null;
            if (!StageLoadoutTypes.Same(districts, stageDistrictIds) || !StageLoadoutTypes.Same(suits, stageSuitIds))
                RefreshStageLoadout();
        }

        private void RefreshStageLoadout()
        {
            if (stage == null || catalog == null) return;
            var loadout = catalog.CurrentLoadout().ToLoadout();
            int arena = LoadoutRules.ArenaOrFallback(NodeWar.Backend.BackendServices.LastKnownState?.Rank);
            // Free-grant items (Farm, Mine, Village, Warrior in low arenas) are never in the chosen deck
            // but are always in hand, so they stand on the stage after the chosen ones.
            int[] chosenDistricts = StageLoadoutTypes.Districts(loadout.districtIDs);
            int[] granted = StageLoadoutTypes.Districts(LoadoutRules.LockedDistricts(arena));
            int[] suits = StageLoadoutTypes.WithGranted(StageLoadoutTypes.Suits(loadout.suitIDs), StageLoadoutTypes.Suits(LoadoutRules.LockedSuits(arena)));
            int[] districtKey = StageLoadoutTypes.WithGranted(chosenDistricts, new[] { -1 }, granted);
            if (!StageLoadoutTypes.Same(districtKey, stageDistrictTypes) || !StageLoadoutTypes.Same(suits, stageSuitTypes))
            {
                workshopStage?.LoadoutReset();
                stage.SetLoadout(chosenDistricts, suits, granted);
                stageDistrictTypes = districtKey;
                stageSuitTypes = suits;
            }
            PlayerProfile profile = PlayerProfile.Instance;
            stageDistrictIds = profile?.data.loadout.nodeIDs == null ? null : (string[])profile.data.loadout.nodeIDs.Clone();
            stageSuitIds = profile?.data.loadout.suitIDs == null ? null : (string[])profile.data.loadout.suitIDs.Clone();
        }

        private void RefreshWorkshopStage()
        {
            stageDistrictTypes = stageSuitTypes = null;
            RefreshStageLoadout();
            if (navigation?.CurrentPageID == LobbyPageID.Workshop) workshopStage?.RefreshInspection();
        }

        /// <summary>
        /// The settings the lobby itself can act on. Reduced motion is the
        /// only one today - it is a class on #lobby-root, so the stylesheet
        /// decides what "reduced" means rather than this file doing it one
        /// animation at a time. The rest of the page's values are stored for
        /// systems that do not exist yet; see SettingsPage.
        ///
        /// Applied once from the saved profile, because the page raises
        /// Changed while loading and nothing is subscribed that early.
        /// </summary>
        private void BindLobbySettings(VisualElement root)
        {
            lobbyRoot = root.Q<VisualElement>("lobby-root");

            if (lobbyRoot == null)
            {
                Debug.LogWarning("[LobbyUI] LobbyRoot.uxml is missing #lobby-root; " +
                                 "settings that style the lobby will not apply.");
                return;
            }

            if (settingsPage == null) return;

            settingsPage.Changed += ApplyLobbySettings;
            ApplyLobbySettings(settingsPage.Settings);
        }

        private void ApplyLobbySettings(GameSettingsData settings)
        {
            NodeWar.Core.FrameRateCap.Apply(settings.frameCap);

            if (lobbyRoot == null) return;

            lobbyRoot.EnableInClassList("lb-reduced-motion", settings.reducedMotion);
        }

        /// <summary>
        /// Profile first, then the push pages, so a push page opened from
        /// anywhere draws over Profile. The sheet, menu and toast layers come
        /// after #overlay-host in LobbyRoot.uxml, so Rename's sheet draws over
        /// Profile too.
        /// </summary>
        private void BuildOverlays(VisualElement overlayHost)
        {
            var socialService = NodeWar.Backend.BackendServices.Social;
            var profileService = NodeWar.Backend.BackendServices.Profiles;
            profilePage = new ProfilePage(profilePageLayout, sheet, toast, profileService);
            overlayHost.Add(profilePage.Root);
            // Pages register after this block; bind Social once the shell reaches its panel.
            overlayHost.schedule.Execute(() =>
                (overlayHost.panel?.visualTree.Q("page-social")?.userData as SocialPage)?.Bind(
                    socialService, profileService, NodeWar.Backend.BackendServices.MatchHistory, profilePage.OpenFor, sheet));

            settingsPage = new SettingsPage(settingsPageLayout, accountFlow);
            overlayHost.Add(settingsPage.Root);

            matchHistoryPage = new MatchHistoryPage(matchHistoryPageLayout);
            overlayHost.Add(matchHistoryPage.Root);
        }

        private void BuildPlayPopup()
        {
            if (playPopupLayout == null)
            {
                Debug.LogWarning("[LobbyUI] No PlayPopup.uxml assigned; BATTLE will do nothing.");
                return;
            }

            if (lobbyManager == null)
                lobbyManager = FindAnyObjectByType<LobbyManager>();

            playPopup = new PlayPopup(playPopupLayout, lobbyManager, toast, sheet, catalog);
        }

        private void OnPlayRequested()
        {
            if (selectedMode.IsPrivate) playFlow.Play(selectedMode);
            else searchingScreen.Open(selectedMode);
        }

        private void BuildModePicker()
        {
            selectedMode = LobbyModeSelection.FromProfile(PlayerProfile.Instance != null ? PlayerProfile.Instance.data : default);
            modePicker = new LobbyModePicker(lobbyRoot, router);
            // Keep Home out of the page's slide/fade, including its exposed edges.
            modePicker.Opened += () => { if (homePage != null) homePage.Root.style.visibility = Visibility.Hidden; };
            modePicker.Closed += () => { if (homePage != null) homePage.Root.style.visibility = StyleKeyword.Null; };
            modePicker.Selected += selection =>
            {
                selectedMode = selection;
                var profile = PlayerProfile.Instance;
                if (profile != null) { selection.Store(ref profile.data); profile.Save(); }
                homePage?.SetMode(selection);
            };
        }

        /// <summary>
        /// The tab pages, registered in track order - left to right is the
        /// order the tab bar shows them.
        /// </summary>
        private void RegisterPages()
        {
            navigation.Register(new ShopPage(shopPageLayout, sheet, toast, menu));
            var workshop = new WorkshopPage(workshopPageLayout, catalog, toast, menu, sheet);
            navigation.Register(workshop);
            workshop.BindOverlayRouter(router, lobbyRoot);
            workshopStage = new WorkshopStageBinding(workshop, stage, router, lobbyRoot, RefreshWorkshopStage, navigation);

            if (homePageLayout != null || Resources.Load<VisualTreeAsset>("Lobby/HomePage") != null)
            {
                HomePage home = new HomePage(homePageLayout, toast, menu, catalog, accountFlow,
                    () => isActiveAndEnabled && lobbyRoot != null
                        && Routes != null && Routes.Count == 0, crateInventory);
                homePage = home;
                home.PlayRequested += OnPlayRequested;
                home.ModeRequested += () => modePicker.Open(selectedMode);
                home.CrateRequested += size =>
                crateScreen?.Open(size, crateInventory);
                navigation.Register(home);
            }
            else
            {
                navigation.Register(new PlaceholderPage(LobbyPageID.Home, "HomePage.uxml not assigned"));
            }

            navigation.Register(new SocialPage(socialPageLayout));
            // Guild-specific binding stays here so the shared chrome can replace it at merge.
            guildPage = new GuildPage(NodeWar.Backend.BackendServices.Guild, NodeWar.Backend.BackendServices.GuildBrowse,
                router, lobbyRoot, sheet, playerId => profilePage.OpenFor(playerId));
            System.Action guildChrome = () => lobbyRoot.schedule.Execute(() =>
            {
                if (navigation.CurrentPageID != LobbyPageID.Guild) return;
                bool hide = router.Stack.Top?.HidesHeader == true;
                var playerBand = lobbyRoot.Q("strip-wrap");
                var resources = lobbyRoot.Q("top-bar");
                if (playerBand != null) playerBand.style.display = hide || guildPage.HasGuild ? DisplayStyle.None : DisplayStyle.Flex;
                if (resources != null) resources.style.display = hide ? DisplayStyle.None : DisplayStyle.Flex;
            });
            guildPage.MembershipChanged += guildChrome;
            navigation.PageShown += _ => guildChrome();
            router.Stack.Changed += guildChrome;
            navigation.Register(guildPage);
            guildPage.MembershipChanged += () => RefreshChromeVisibility(lobbyRoot);

            // Padding belongs to the scrolling content, so the final row can
            // reach the top of the floating dock at every panel scale.
            void UpdateListClearance()
            {
                float height = pillDock.resolvedStyle.height;
                if (float.IsNaN(height) || height <= 0) return;
                foreach (string name in new[] { "social-scroll", "guild-results-scroll", "guild-home-scroll" })
                {
                    var scroll = lobbyRoot.Q<ScrollView>(name);
                    if (scroll != null) scroll.contentContainer.style.paddingBottom = height + 12f;
                }
                var chat = guildPage.Root.Q("guild-chat-button");
                if (chat != null) chat.style.bottom = height + 12f;
            }
            pillDock.RegisterCallback<GeometryChangedEvent>(_ => UpdateListClearance());
            // Guild home and visitor are attached on demand after membership loads.
            guildPage.Root.RegisterCallback<GeometryChangedEvent>(_ => UpdateListClearance());
            guildPage.MembershipChanged += () => lobbyRoot.schedule.Execute(UpdateListClearance);
            lobbyRoot.schedule.Execute(UpdateListClearance);
        }

        private void BindNav(VisualElement root, string elementName, LobbyPageID id)
        {
            Button button = root.Q<Button>(elementName);

            if (button == null)
            {
                Debug.LogWarning("[LobbyUI] Nav button not found in layout: " + elementName);
                return;
            }

            navigation.BindNavButton(id, button);
            string badge = id == LobbyPageID.Shop ? "shop" : id == LobbyPageID.Social ? "social" : id == LobbyPageID.Guild ? "guild" : null;
            if (badge != null) button.Add(new LobbyBadgeDot(Badges, badge));
        }

        private void RefreshCrateBadges()
        {
            if (crateInventory == null || Badges == null) return;
            Badges.Set("crates", crateInventory.Count(NodeWar.Backend.CrateSize.Small)
                + crateInventory.Count(NodeWar.Backend.CrateSize.Medium));
        }

        private void WireOverlays(VisualElement root)
        {
            VisualElement shell = root.Q<VisualElement>("lobby-root");
            claimPanel = new ClaimPanel();
            crateScreen = new CrateScreen(router);
            shell.Add(crateScreen.Root);
            (root.Q("page-shop")?.userData as ShopPage)?.Bind(claimPanel, Badges);
            (root.Q("page-shop")?.userData as ShopPage)?.BindStage(stage);
            shell.Add(claimPanel.Root);
            profilePage.BindPanels(router, shell);
            multiplayerSheet = new MultiplayerSheet(shell, router, searchingScreen.Open);
            profilePage.InviteRequested += target => multiplayerSheet.Open(target);
            searchingScreen.InviteRequested += () => multiplayerSheet.Open();
            burgerPanel = new LobbyOverlayPanel("Menu");
            bellPanel = new LobbyOverlayPanel("Notifications");
            shell.Add(burgerPanel.Root);
            shell.Add(bellPanel.Root);
            VisualElement toastLayer = root.Q<VisualElement>("toast-dock");
            System.Action<VisualElement> promote = element =>
            {
                element?.BringToFront();
                toastLayer?.BringToFront();
            };
            WirePanel(burgerPanel, "burger", promote);
            WirePanel(bellPanel, "bell", promote);
            BuildBurger();

            // PlayPopup already opens/closes this sheet. One entry preserves its
            // Closed callback, which cancels matchmaking and live connections.
            sheet.Opened += content =>
            {
                promote(root.Q<VisualElement>("sheet-layer"));
                router.Open(new LobbyOverlay("sheet", OverlayKind.Sheet, true), sheet.Close);
            };
            sheet.Closed += content => router.Closed("sheet");
            menu.Opened += () =>
            {
                promote(root.Q<VisualElement>("menu-layer"));
                router.Open(new LobbyOverlay("menu", OverlayKind.Sheet, true), menu.Close);
            };
            menu.Closed += () => router.Closed("menu");
            profilePage.Opened += () =>
            {
                profilePage.Root.BringToFront();
                promote(profilePage.Root.parent);
                router.Open(new LobbyOverlay("profile", profilePage.ViewingSelf ? OverlayKind.Panel : OverlayKind.Sheet, true), profilePage.Close);
            };
            profilePage.Closed += () =>
            {
                router.Closed("profile");
            };
            WirePushPage(settingsPage, "settings", promote);
            WirePushPage(matchHistoryPage, "history", promote);
            claimPanel.Opened += () =>
            {
                promote(claimPanel.Root);
                router.Open(new LobbyOverlay("claim", OverlayKind.Panel, true), claimPanel.Cancel);
            };
            claimPanel.Closed += () => router.Closed("claim");
            claimPanel.Cancelled += () => confirmingExit = false;
            claimPanel.Confirmed += () =>
            {
                if (!confirmingExit) return;
                confirmingExit = false;
                Application.Quit();
            };
            router.Stack.Changed += () => RefreshChromeVisibility(root);
            navigation.PageShown += id => RefreshChromeVisibility(root);
        }

        private void WirePushPage(LobbyPushPage page, string id, System.Action<VisualElement> promote)
        {
            page.Opened += () =>
            {
                page.Root.BringToFront();
                promote(page.Root.parent);
                router.Open(new LobbyOverlay(id, OverlayKind.Fullscreen, true, true), page.Close);
            };
            page.Closed += () => router.Closed(id);
        }

        private void WirePanel(LobbyOverlayPanel panel, string id, System.Action<VisualElement> promote)
        {
            panel.Opened += () =>
            {
                promote(panel.Root);
                router.Open(new LobbyOverlay(id, OverlayKind.Panel, true), panel.Close);
            };
            panel.Closed += () => router.Closed(id);
        }

        private void BuildBurger()
        {
            AddBurgerItem("Settings", () => settingsPage.Open());
            AddBurgerItem("Account", () => settingsPage.Open());
            AddBurgerItem("Privacy");
            AddBurgerItem("Notifications", OpenBell);
            AddBurgerItem("Support");
            AddBurgerItem("Restore Purchases");
            AddBurgerItem("Legal");
            AddBurgerItem("Credits");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddBurgerItem("Testing", () => playPopup?.Show(GameMode.Testing));
#endif
        }

        private void AddBurgerItem(string label, System.Action destination = null)
        {
            var button = new Button(() =>
            {
                burgerPanel.Close();
                if (destination != null) destination();
                else Debug.Log("[Stub] Menu " + label);
            }) { text = label };
            button.AddToClassList("mk-btn");
            button.AddToClassList("lb-menu-destination");
            burgerPanel.Content.Add(button);
        }

        private void OpenBell()
        {
            if (bellPanel == null) return;
            bellPanel.Content.Clear();
            var view = new AsyncStateView();
            foreach (string message in notifications) view.Content.Add(new Label(message) { enableRichText = false });
            view.Render(new AsyncState().Reduce(notifications.Length == 0 ? AsyncResult.Empty : AsyncResult.Ready, 0),
                0, "No notifications");
            bellPanel.Content.Add(view);
            Badges.Clear("bell");
            bellPanel.Open();
        }

        private void RefreshChromeVisibility(VisualElement root)
        {
            LobbyOverlay top = Routes?.Top;
            VisualElement pills = root.Q<VisualElement>("tabbar-dock");
            VisualElement header = root.Q<VisualElement>("strip-wrap");
            VisualElement currencies = root.Q<VisualElement>("top-bar");
            var playerCard = root.Q("trophy-strip");
            var guildCard = root.Q("guild-chrome-card");
            bool showGuild = (navigation.CurrentPageID == LobbyPageID.Guild || top?.ID == "guild-chat") && guildPage?.CurrentGuild != null;
            if (guildCard == null && header != null)
            {
                guildCard = new VisualElement { name = "guild-chrome-card" };
                header.Add(guildCard);
            }
            if (guildCard != null)
            {
                guildCard.style.display = showGuild ? DisplayStyle.Flex : DisplayStyle.None;
                if (showGuild && guildCard.userData != guildPage.CurrentGuild)
                {
                    guildCard.Clear();
                    guildCard.Add(GuildViews.Header(guildPage.CurrentGuild, true));
                    guildCard.userData = guildPage.CurrentGuild;
                }
            }
            if (playerCard != null) playerCard.style.display = showGuild ? DisplayStyle.None : DisplayStyle.Flex;
            if (pills != null) pills.style.display = Routes.HidesPills ? DisplayStyle.None : DisplayStyle.Flex;
            if (header != null) header.style.display = (top != null && top.HidesHeader) ? DisplayStyle.None : DisplayStyle.Flex;
            if (currencies != null) currencies.style.display = (top != null && top.HidesHeader) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void HandleBack()
        {
            if (router == null || navigation == null) return;
            BackAction action = Routes.Back(navigation.CurrentPageID ?? LobbyPageID.Home);
            if (action.Kind == BackActionKind.CloseTop) router.Close(action.ID);
            else if (action.Kind == BackActionKind.GoHome) navigation.Show(LobbyPageID.Home);
            else
            {
                claimPanel.Open(new ClaimRequest("Quit Node War?", "Close the game?", ClaimAction.Ok));
                confirmingExit = true;
            }
        }

        public void HandleDeepLink(string url)
        {
            if (router == null || navigation == null || !LobbyDeepLink.TryParse(url, out LobbyRoute route)) return;
            router.CloseAll();
            if (route.Kind == LobbyRouteKind.ShopStall)
            {
                navigation.Show(LobbyPageID.Shop);
                (lobbyRoot.Q("page-shop")?.userData as ShopPage)?.ScrollTo(route.Target);
            }
            else if (route.Kind == LobbyRouteKind.Page)
            {
                string target = route.Target == "loadout" ? "Workshop" : route.Target;
                if (System.Enum.TryParse(target, true, out LobbyPageID page)) navigation.Show(page);
            }
            else if (route.Kind == LobbyRouteKind.Bell) OpenBell();
            else if (route.Kind == LobbyRouteKind.Profile) profilePage.OpenFor(route.Target);
            else if (route.Kind == LobbyRouteKind.GuildChat)
            {
                navigation.Show(LobbyPageID.Guild);
                _ = guildPage.OpenChatAsync();
            }
            else Debug.Log("[LobbyUI] Route: " + route.Kind + " " + route.Target);
        }
    }
}
