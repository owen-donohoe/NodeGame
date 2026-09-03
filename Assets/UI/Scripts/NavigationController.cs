using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Owns which page is visible and keeps the nav bar in step with it.
    ///
    /// The uGUI lobby does this by toggling GameObjects active
    /// (LobbyManager.ShowPanel). Here every registered page stays in the tree
    /// and visibility is a USS class, so a page keeps its scroll position and
    /// its element state between visits without anyone writing code to save
    /// and restore them - which GroupSelectionPanel currently does by hand.
    /// </summary>
    public class NavigationController
    {
        private readonly VisualElement pageHost;
        private readonly List<LobbyPage> pages = new List<LobbyPage>();
        private readonly Dictionary<LobbyPageID, Button> navButtons =
            new Dictionary<LobbyPageID, Button>();

        private LobbyPage currentPage;

        public LobbyPageID? CurrentPageID
        {
            get { return currentPage != null ? currentPage.ID : (LobbyPageID?)null; }
        }

        public NavigationController(VisualElement pageHost)
        {
            this.pageHost = pageHost;
        }

        /// <summary>
        /// Wires one nav-bar button to a page. Safe to call for a page that has
        /// not been registered yet - the button simply does nothing until it is,
        /// which is what keeps the shell usable while pages arrive one session
        /// at a time.
        /// </summary>
        public void BindNavButton(LobbyPageID id, Button button)
        {
            if (button == null) return;

            navButtons[id] = button;
            button.clicked += () => Show(id);
        }

        public void Register(LobbyPage page)
        {
            if (page == null) return;

            pages.Add(page);
            pageHost.Add(page.Root);
            page.SetVisible(false);
        }

        public bool Has(LobbyPageID id)
        {
            return Find(id) != null;
        }

        /// <summary>
        /// Shows a page. A request for a page that is not registered is ignored
        /// rather than throwing: during S2-S4 most tabs point at nothing yet,
        /// and a half-built lobby should stay navigable.
        /// </summary>
        public void Show(LobbyPageID id)
        {
            LobbyPage target = Find(id);
            if (target == null) return;
            if (currentPage == target) return;

            if (currentPage != null)
            {
                currentPage.OnHide();
                currentPage.SetVisible(false);
            }

            currentPage = target;
            currentPage.SetVisible(true);
            currentPage.OnShow();

            RefreshNavButtons();
        }

        /// <summary>Shows the first registered page. Used at startup.</summary>
        public void ShowFirst()
        {
            if (pages.Count > 0) Show(pages[0].ID);
        }

        private void RefreshNavButtons()
        {
            foreach (KeyValuePair<LobbyPageID, Button> entry in navButtons)
            {
                bool active = currentPage != null && entry.Key == currentPage.ID;
                entry.Value.EnableInClassList("nav-button--active", active);
            }
        }

        private LobbyPage Find(LobbyPageID id)
        {
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i].ID == id) return pages[i];
            }
            return null;
        }
    }
}
