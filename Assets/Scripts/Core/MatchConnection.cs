using UnityEngine;
using NodeWar.Network;

namespace NodeWar.Core
{
    /// <summary>
    /// Persists across scenes via DontDestroyOnLoad.
    /// Created by LobbyUI on connection established or local play selected.
    /// Read by GameManager.Awake() in the Gameplay scene.
    /// Destroyed on match end when returning to lobby.
    /// </summary>
    public class MatchConnection : MonoBehaviour
    {
        public NetworkManager networkManager;
        public int localPlayerID;
        public bool isNetworked;
        public bool isBotMatch;

        private static MatchConnection instance;
        public static MatchConnection Instance => instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Shuts down networking and destroys the persistent object.
        /// Called when returning to lobby after match end or disconnect.
        /// </summary>
        public void Shutdown()
        {
            if (networkManager != null)
            {
                networkManager.Shutdown();
                networkManager = null;
            }

            instance = null;
            Destroy(gameObject);
        }
    }
}