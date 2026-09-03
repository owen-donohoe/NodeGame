using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Which page the lobby is showing.
    ///
    /// Deliberately not the same set as NodeWar.Lobby.PanelType, which the uGUI
    /// lobby uses. That enum has GameMode as a page; here mode selection lives
    /// in the play popup, and Social is new. The two stacks run side by side
    /// until S5, so they keep their own vocabularies rather than one pretending
    /// to be the other.
    /// </summary>
    public enum LobbyPageID
    {
        Home,
        Workshop,
        Shop,
        Social,
        Profile
    }

    /// <summary>
    /// One page of the lobby. The UI Toolkit counterpart to LobbyPanel: it owns
    /// a subtree and gets told when it becomes visible.
    ///
    /// A plain class, not a MonoBehaviour. Pages are elements in a panel, not
    /// objects in a scene, so there is nothing for a MonoBehaviour to attach to
    /// and nothing to wire in an inspector. Anything a page needs is passed to
    /// its constructor.
    /// </summary>
    public abstract class LobbyPage
    {
        public LobbyPageID ID { get; private set; }

        /// <summary>The page's own subtree. Added to the page host on registration.</summary>
        public VisualElement Root { get; private set; }

        protected LobbyPage(LobbyPageID id, VisualElement root)
        {
            ID = id;
            Root = root;
            Root.AddToClassList("page");
        }

        /// <summary>Called when this page becomes the visible one. Refresh data here.</summary>
        public virtual void OnShow() { }

        /// <summary>Called when this page is being hidden. Cancel pending work here.</summary>
        public virtual void OnHide() { }

        internal void SetVisible(bool visible)
        {
            Root.EnableInClassList("page--hidden", !visible);
        }
    }
}
