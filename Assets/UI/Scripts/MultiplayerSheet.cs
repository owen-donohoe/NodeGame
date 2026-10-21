using System;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    public sealed class MultiplayerSheet
    {
        private readonly MkPanel sheet = new MkPanel { name = "multiplayer-sheet" };
        private readonly LobbyOverlayRouter router;
        private readonly Action<LobbyModeSelection> search;
        private string playerId;

        public MultiplayerSheet(VisualElement host, LobbyOverlayRouter router, Action<LobbyModeSelection> search)
        {
            this.router = router; this.search = search;
            sheet.BindRouter(router, "multiplayer"); host.Add(sheet);
            BackendServices.Account.Changed += AccountChanged;
            sheet.RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == sheet) BackendServices.Account.Changed -= AccountChanged; });
            sheet.styleSheets.Add(Resources.Load<StyleSheet>("Lobby/ModePicker"));
            sheet.Title.text = "Multiplayer";
            var grid = LobbyModePicker.ModeGrid("multiplayer-modes"); sheet.Body.Add(grid);
            foreach (var mode in ModeDefinition.All)
            {
                if (!mode.CanSelect(true)) continue;
                var selection = new LobbyModeSelection(mode.Id, true);
                grid.Add(LobbyModePicker.ModeCard(selection, () => Select(selection)));
            }
            sheet.Body.Add(GuildViews.Button("Back", sheet.Close));
        }
        private void AccountChanged(AccountInfo _) { playerId = null; sheet.Close(); }
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
