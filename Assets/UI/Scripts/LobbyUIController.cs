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
    /// It reads no simulation state and writes none - the lobby runs in its own
    /// scene before any SimulationState exists. The view/UI boundary in
    /// .claude/rules/view-ui.md has nothing to bite on here, and will only
    /// start to matter when the HUD moves in S6.
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
        private PlayPopup playPopup;
        private LobbyToast toast;
        private LobbyChrome chrome;

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
            toast = null;
            chrome = null;
            navigation = null;
            safeArea = null;
        }

        private void Update()
        {
            // Cheap: returns immediately unless the safe area, the screen or the
            // panel size actually changed.
            if (safeArea != null) safeArea.Update();

            // Pumps the connection state machine while the popup is open.
            if (playPopup != null) playPopup.Update();
        }

        private void OnPlayRequested()
        {
            if (playPopup != null) playPopup.Show();
        }

        private void BuildShell(VisualElement root)
        {
            VisualElement lobbyRoot = root.Q<VisualElement>("lobby-root");
            VisualElement safeAreaElement = root.Q<VisualElement>("safe-area");
            VisualElement pageHost = root.Q<VisualElement>("page-host");

            if (lobbyRoot == null || safeAreaElement == null || pageHost == null)
            {
                Debug.LogError("[LobbyUI] LobbyRoot.uxml is missing #lobby-root, #safe-area " +
                               "or #page-host. The shell cannot be built.");
                return;
            }

            safeArea = new SafeAreaBinder(safeAreaElement);
            navigation = new NavigationController(pageHost);
            toast = new LobbyToast(root.Q<Label>("toast"));

            // The chrome is outside every page, so it is bound once, and it
            // refreshes whenever a page shows - Profile can rename the player.
            chrome = new LobbyChrome(root, toast);
            chrome.ProfileRequested += () => navigation.Show(LobbyPageID.Profile);
            navigation.PageShown += id => chrome.Refresh();

            // Four tabs. Profile is reached from the player name, not a tab.
            BindNav(root, "nav-shop", LobbyPageID.Shop);
            BindNav(root, "nav-home", LobbyPageID.Home);
            BindNav(root, "nav-workshop", LobbyPageID.Workshop);
            BindNav(root, "nav-social", LobbyPageID.Social);

            RegisterPages();
            BuildPlayPopup(lobbyRoot);

            navigation.Show(LobbyPageID.Home);

            if (verboseLogging)
                Debug.Log("[LobbyUI] Shell built. Current page: " + navigation.CurrentPageID);
        }

        /// <summary>
        /// The sheet is added to #lobby-root, after the safe area, so it floats
        /// over the tab bar as well as the page and inherits the lobby font. The
        /// toast is then moved after it, so "Code copied" shows over the sheet.
        /// </summary>
        private void BuildPlayPopup(VisualElement lobbyRoot)
        {
            if (playPopupLayout == null)
            {
                Debug.LogWarning("[LobbyUI] No PlayPopup.uxml assigned; BATTLE will do nothing.");
                return;
            }

            if (lobbyManager == null)
                lobbyManager = FindAnyObjectByType<LobbyManager>();

            playPopup = new PlayPopup(playPopupLayout, lobbyManager, toast);
            lobbyRoot.Add(playPopup.Root);

            VisualElement toastDock = lobbyRoot.Q<VisualElement>("toast-dock");
            if (toastDock != null) lobbyRoot.Add(toastDock);
        }

        /// <summary>
        /// Every page the lobby can show.
        ///
        /// S1 registered placeholders only, and each session since has replaced
        /// the ones it built: S2 Home, S3 Workshop and Profile, S4 Shop and
        /// Social. With S4 in, no PlaceholderPage remains except the fallback
        /// for a Home layout that failed to assign - so PlaceholderPage itself
        /// is now only a diagnostic, not a stand-in for unbuilt work.
        /// </summary>
        private void RegisterPages()
        {
            if (homePageLayout != null)
            {
                HomePage home = new HomePage(homePageLayout, toast, allSuits, allNodes);
                home.PlayRequested += OnPlayRequested;
                home.LoadoutRequested += () => navigation.Show(LobbyPageID.Workshop);
                navigation.Register(home);
            }
            else
            {
                navigation.Register(new PlaceholderPage(LobbyPageID.Home, "HomePage.uxml not assigned"));
            }

            // Workshop and Profile handle a missing layout themselves, falling
            // back to a labelled box, so they are registered unconditionally.
            navigation.Register(new WorkshopPage(workshopPageLayout, allSuits, allNodes));

            navigation.Register(new ProfilePage(profilePageLayout));

            navigation.Register(new ShopPage(shopPageLayout));
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
