using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>Sending UI only. Invited records a stub request, never a delivery receipt.</summary>
    public sealed class TeammateInviteSheet : IDisposable
    {
        private readonly MkSheet sheet = new MkSheet { name = "teammate-invite-sheet" };
        private readonly VisualElement list = new VisualElement();
        private readonly List<string> invited = new List<string>();
        private int version;
        public TeammateInviteSheet(VisualElement shell, LobbyOverlayRouter router)
        {
            sheet.styleSheets.Add(Resources.Load<StyleSheet>("Lobby/TeammateInvite"));
            sheet.BindRouter(router, "teammate-invite"); shell.Add(sheet);
            sheet.Body.Add(GuildViews.Text("Invite teammate", "mk-h2"));
            sheet.Body.Add(list); sheet.Body.Add(GuildViews.Button("Done", sheet.Close));
            sheet.Closed += () => version++;
            BackendServices.Account.Changed += AccountChanged;
        }
        private void AccountChanged(AccountInfo _) { invited.Clear(); sheet.Close(); version++; }
        public void Open()
        {
            sheet.Open(true); _ = LoadAsync();
        }
        private bool Current(int request, string id) => sheet.IsOpen && request == version && id == BackendServices.Account.Current?.PlayerId;
        private async Task LoadAsync()
        {
            int request = ++version; string self = BackendServices.Account.Current?.PlayerId;
            list.Clear(); list.Add(GuildViews.Text("Loading players\u2026", "mk-muted"));
            try
            {
                var friendsTask = BackendServices.Social.GetFriendsAsync();
                var guildTask = BackendServices.Guild.GetMineAsync();
                await Task.WhenAll(friendsTask, guildTask);
                if (!Current(request, self)) return;
                var members = new List<PlayerCard>();
                if (guildTask.Result?.Members != null) foreach (var member in guildTask.Result.Members)
                {
                    var p = member?.Player; if (p == null) continue;
                    members.Add(new PlayerCard { PlayerId = p.PlayerId, Name = p.Name, Tag = p.Tag, Presence = TeammateInviteRoster.Presence(member) });
                }
                list.Clear();
                Section("Friends", TeammateInviteRoster.Ordered(friendsTask.Result, self));
                Section("Guild members", TeammateInviteRoster.Ordered(members, self));
            }
            catch (Exception)
            {
                if (!Current(request, self)) return;
                list.Clear(); list.Add(GuildViews.Text("Couldn't load players", "mk-muted"));
                list.Add(GuildViews.Button("Retry", () => _ = LoadAsync()));
            }
        }
        private void Section(string title, List<PlayerCard> players)
        {
            var ledger = GuildViews.Card(title); list.Add(ledger);
            if (players.Count == 0) ledger.Add(GuildViews.Text("No players to invite", "mk-muted"));
            foreach (var player in players)
            {
                var row = new VisualElement(); row.AddToClassList("teammate-row"); ledger.Add(row);
                var avatar = GuildViews.Text(string.IsNullOrEmpty(player.Name) ? "?" : player.Name.Substring(0, 1), "mk-avatar"); row.Add(avatar);
                var who = new VisualElement(); who.AddToClassList("teammate-who"); row.Add(who);
                who.Add(GuildViews.Text(TeammateInviteRoster.Handle(player), "mk-friend__name"));
                var presence = new VisualElement(); presence.AddToClassList("teammate-presence"); who.Add(presence);
                var dot = new VisualElement(); dot.AddToClassList("teammate-dot"); dot.AddToClassList("teammate-dot--" + TeammateInviteRoster.PresenceClass(player.Presence)); presence.Add(dot);
                presence.Add(GuildViews.Text(TeammateInviteRoster.PresenceLabel(player.Presence), "mk-muted"));
                var send = GuildViews.Button(invited.Contains(player.PlayerId) ? "Invited" : "Invite", null);
                send.SetEnabled(!invited.Contains(player.PlayerId)); row.Add(send);
                send.clicked += () =>
                {
                    MultiplayerSheet.SendInviteStub(player.PlayerId, new LobbyModeSelection(LobbyMode.Team2v2));
                    invited.Add(player.PlayerId);
                    // The same player can appear in both ledgers; keep both buttons consistent.
                    list.Query<Button>().ForEach(b => { if ((string)b.userData == player.PlayerId) { b.text = "Invited"; b.SetEnabled(false); } });
                };
                send.userData = player.PlayerId;
            }
        }
        public void Dispose() { version++; sheet.Close(); BackendServices.Account.Changed -= AccountChanged; sheet.RemoveFromHierarchy(); }
    }
}
