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

        [Tooltip("Log what the shell wired up on start. Off for normal play.")]
        [SerializeField] private bool verboseLogging;

        private UIDocument document;
        private NavigationController navigation;
        private SafeAreaBinder safeArea;

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
            navigation = null;
            safeArea = null;
        }

        private void Update()
        {
            // Cheap: returns immediately unless the safe area, the screen or the
            // panel size actually changed.
            if (safeArea != null) safeArea.Update();
        }

        private void BuildShell(VisualElement root)
        {
            VisualElement safeAreaElement = root.Q<VisualElement>("safe-area");
            VisualElement pageHost = root.Q<VisualElement>("page-host");

            if (safeAreaElement == null || pageHost == null)
            {
                Debug.LogError("[LobbyUI] LobbyRoot.uxml is missing #safe-area or #page-host. " +
                               "The shell cannot be built.");
                return;
            }

            safeArea = new SafeAreaBinder(safeAreaElement);
            navigation = new NavigationController(pageHost);

            BindNav(root, "nav-home", LobbyPageID.Home);
            BindNav(root, "nav-workshop", LobbyPageID.Workshop);
            BindNav(root, "nav-shop", LobbyPageID.Shop);
            BindNav(root, "nav-social", LobbyPageID.Social);
            BindNav(root, "nav-profile", LobbyPageID.Profile);

            RegisterPages();

            navigation.Show(LobbyPageID.Home);

            if (verboseLogging)
                Debug.Log("[LobbyUI] Shell built. Current page: " + navigation.CurrentPageID);
        }

        /// <summary>
        /// Every page the lobby can show.
        ///
        /// S1 registers placeholders only. Each session replaces the ones it
        /// builds: S2 Home, S3 Workshop and Profile, S4 Shop and Social. The
        /// placeholder for a page is deleted in the same commit as the page
        /// that replaces it.
        /// </summary>
        private void RegisterPages()
        {
            navigation.Register(new PlaceholderPage(LobbyPageID.Home, "due in S2"));
            navigation.Register(new PlaceholderPage(LobbyPageID.Workshop, "due in S3"));
            navigation.Register(new PlaceholderPage(LobbyPageID.Shop, "due in S4"));
            navigation.Register(new PlaceholderPage(LobbyPageID.Social, "due in S4"));
            navigation.Register(new PlaceholderPage(LobbyPageID.Profile, "due in S3"));
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
