using UnityEngine;

namespace NodeWar.Lobby
{
    // Integration stub; feat/lobby-g10b supplies the private lobby panel.
    public static class PrivateLobbyPanel
    {
        public static void Open(LobbyModeSelection selection) =>
            Debug.Log("[Private lobby] Open " + selection.Mode);
    }
}
