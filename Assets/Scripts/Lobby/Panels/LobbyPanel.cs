using UnityEngine;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Base class for all lobby panels. Provides lifecycle hooks
    /// and a reference back to LobbyManager for navigation.
    /// </summary>
    public abstract class LobbyPanel : MonoBehaviour
    {
        protected LobbyManager lobbyManager;

        public void SetManager(LobbyManager manager)
        {
            lobbyManager = manager;
        }

        /// <summary>
        /// Called when this panel becomes the active panel.
        /// Override to refresh displayed data.
        /// </summary>
        public virtual void OnShow() { }

        /// <summary>
        /// Called when this panel is being hidden.
        /// Override to clean up or cancel pending operations.
        /// </summary>
        public virtual void OnHide() { }
    }
}