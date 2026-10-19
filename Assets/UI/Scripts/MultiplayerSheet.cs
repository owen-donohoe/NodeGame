using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    public sealed class MultiplayerSheet
    {
        private readonly MkSheet sheet = new MkSheet { name = "multiplayer-sheet" };
        private readonly LobbyOverlayRouter router;
        private readonly Action<LobbyModeSelection> search;
        private string playerId;

        public MultiplayerSheet(VisualElement host, LobbyOverlayRouter router, Action<LobbyModeSelection> search)
        {
            this.router = router; this.search = search;
            sheet.BindRouter(router, "multiplayer"); host.Add(sheet);
            sheet.styleSheets.Add(Resources.Load<StyleSheet>("Lobby/ModePicker"));
            sheet.Body.Add(GuildViews.Text("Multiplayer", "mk-h2"));
            var grid = LobbyModePicker.ModeGrid("multiplayer-modes"); sheet.Body.Add(grid);
            foreach (var mode in new[] { new LobbyModeSelection(LobbyMode.Ranked1v1, true), new LobbyModeSelection(LobbyMode.Team2v2), new LobbyModeSelection(LobbyMode.FreeForAll4) })
            {
                var selection = mode;
                grid.Add(LobbyModePicker.ModeCard(selection, () => Select(selection)));
            }
            sheet.Body.Add(GuildViews.Button("Back", sheet.Close));
        }
        public void Open(string targetPlayerId = null)
        {
            playerId = targetPlayerId; sheet.BringToFront(); sheet.Open();
        }
        private void Select(LobbyModeSelection selection)
        {
            string target = playerId;
            sheet.Close(); router.CloseAll();
            Debug.Log("[Stub] Invite " + (target ?? "player") + " to " + new HomeModeDisplay(selection).Name);
            search(selection);
        }
    }
}
