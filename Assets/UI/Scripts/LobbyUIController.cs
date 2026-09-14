using UnityEngine;
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
    ///     (NavigationController) with Shop, Home, Workshop and Social;
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
        [SerializeField] private NodeDefinition[] allNodes;

        [Header("Links")]
        [Tooltip("Used to start Bot and Testing matches. Found automatically if left empty.")]
        [SerializeField] private LobbyManager lobbyManager;

        [Tooltip("Log what the shell wired up on start. Off for normal play.")]
        [SerializeField] private bool verboseLogging;

        private UIDocument document;
        private NavigationController navigation;
        private SafeAreaBinder safeArea;
        private LobbyToast toast;
        private LobbySheet sheet;
        private LobbyContextMenu menu;
        private LobbyChrome chrome;
        private PlayPopup playPopup;
        private ProfilePage profilePage;
        private SettingsPage settingsPage;
        private MatchHistoryPage matchHistoryPage;

        /// <summary>
        /// Page switching, for pages to navigate between themselves. Null until
        /// OnEnable has run.
        /// </summary>
        public NavigationController Navigation
        {
            get { return navigation; }
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

            BuildShell(root);
        }

        private void OnDisable()
        {
            // Dispose before dropping the reference: the popup may own a live
            // socket, and leaving one open would collide with the next attempt.
            if (playPopup != null) playPopup.Dispose();

            playPopup = null;
            profilePage = null;
            settingsPage = null;
            matchHistoryPage = null;
            toast = null;
            sheet = null;
            menu = null;
            chrome = null;
            navigation = null;
            safeArea = null;
        }

        private void Update()
        {
            // Cheap: each returns immediately unless the safe area, the screen
            // or the panel size actually changed.
            if (safeArea != null) safeArea.Update();
            if (profilePage != null) profilePage.Update();
            if (settingsPage != null) settingsPage.Update();
            if (matchHistoryPage != null) matchHistoryPage.Update();

            // Pumps the connection state machine while the battle sheet is open.
            if (playPopup != null) playPopup.Update();
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
            navigation = new NavigationController(pageHost);

            // Shared machinery, built before any page so every page gets the same.
            toast = new LobbyToast(root.Q<Label>("toast"));
            sheet = new LobbySheet(root);
            menu = new LobbyContextMenu(root);

            BuildOverlays(overlayHost);
            BuildChrome(root);

            // Four tabs, in track order. Profile is reached from the player name.
            BindNav(root, "nav-shop", LobbyPageID.Shop);
            BindNav(root, "nav-home", LobbyPageID.Home);
            BindNav(root, "nav-workshop", LobbyPageID.Workshop);
            BindNav(root, "nav-social", LobbyPageID.Social);

            BuildPlayPopup();
            RegisterPages();

            navigation.Show(LobbyPageID.Home);

            if (verboseLogging)
                Debug.Log("[LobbyUI] Shell built. Current page: " + navigation.CurrentPageID);
        }

        private void BuildChrome(VisualElement root)
        {
            // The chrome is outside every page, so it is bound once, and it
            // refreshes whenever a page shows or the player is renamed.
            chrome = new LobbyChrome(root, toast);
            navigation.PageShown += id => chrome.Refresh();

            VisualElement strip = root.Q<VisualElement>("strip-wrap");
            chrome.ProfileRequested += () => { if (profilePage != null) profilePage.Open(strip); };
            chrome.SettingsRequested += () => { if (settingsPage != null) settingsPage.Open(); };
            chrome.HistoryRequested += () => { if (matchHistoryPage != null) matchHistoryPage.Open(); };

            if (profilePage != null) profilePage.Renamed += chrome.Refresh;
        }

        /// <summary>
        /// Profile first, then the push pages, so a push page opened from
        /// anywhere draws over Profile. The sheet, menu and toast layers come
        /// after #overlay-host in LobbyRoot.uxml, so Rename's sheet draws over
        /// Profile too.
        /// </summary>
        private void BuildOverlays(VisualElement overlayHost)
        {
            profilePage = new ProfilePage(profilePageLayout, sheet, toast);
            overlayHost.Add(profilePage.Root);

            settingsPage = new SettingsPage(settingsPageLayout);
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

            playPopup = new PlayPopup(playPopupLayout, lobbyManager, toast, sheet);
        }

        private void OnPlayRequested()
        {
            if (playPopup != null)
                playPopup.Show();
            else if (toast != null)
                toast.Show("Cannot open the battle sheet: its layout is not assigned");
        }

        /// <summary>
        /// The tab pages, registered in track order - left to right is the
        /// order the tab bar shows them.
        /// </summary>
        private void RegisterPages()
        {
            navigation.Register(new ShopPage(shopPageLayout, sheet, toast, menu));

            if (homePageLayout != null)
            {
                HomePage home = new HomePage(homePageLayout, toast, menu, allSuits, allNodes);
                home.PlayRequested += OnPlayRequested;
                home.LoadoutRequested += () => navigation.Show(LobbyPageID.Workshop);
                navigation.Register(home);
            }
            else
            {
                navigation.Register(new PlaceholderPage(LobbyPageID.Home, "HomePage.uxml not assigned"));
            }

            navigation.Register(new WorkshopPage(workshopPageLayout, allSuits, allNodes, toast, menu));
            navigation.Register(new SocialPage(socialPageLayout));
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
        }
    }
}
