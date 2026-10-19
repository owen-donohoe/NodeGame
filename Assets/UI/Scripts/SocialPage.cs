using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    public class SocialPage : LobbyPage
    {
        private sealed class Section
        {
            public readonly AsyncStateView View = new AsyncStateView();
            public AsyncState State = new AsyncState();
            public int Request;
        }
        private sealed class OnlinePlayer { public PlayerCard Card; public bool Guild; }
        private readonly Section tv = new Section(), friends = new Section(), previous = new Section();
        private ISocialService social;
        private IMatchHistoryService history;
        private IGuildService guild;
        private Action<string> openProfile;
        private List<OnlinePlayer> roster = new List<OnlinePlayer>();
        private bool visible, showOffline;
        private int visit;
        private string account;
        private static long Now => (long)(Time.realtimeSinceStartupAsDouble * 1000);

        public SocialPage(VisualTreeAsset layout) : base(LobbyPageID.Social, Build())
        {
            Root.userData = this; Root.AddToClassList("lb-page-flush");
            Root.Q("social-tv").Add(tv.View); Root.Q("social-friends").Add(friends.View); Root.Q("social-previous").Add(previous.View);
            tv.View.RetryRequested += LoadTV; friends.View.RetryRequested += LoadFriends; previous.View.RetryRequested += LoadPrevious;
            Root.RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == Root) { OnHide(); BackendServices.Account.Changed -= AccountChanged; } });
        }
        private static VisualElement Build()
        {
            var root = new VisualElement { name = "page-social" };
            Resources.Load<VisualTreeAsset>("Lobby/SocialPage").CloneTree(root); return root;
        }
        public void Bind(ISocialService social, IProfileService profiles, IMatchHistoryService history,
            Action<string> openProfile, LobbySheet sheet, IGuildService guild = null)
        {
            this.social = social; this.history = history; this.openProfile = openProfile; this.guild = guild ?? BackendServices.Guild;
            BackendServices.Account.Changed -= AccountChanged; BackendServices.Account.Changed += AccountChanged;
            if (visible) Reload();
        }
        public override void OnShow() { visible = true; if (social != null) Reload(); }
        public override void OnHide()
        {
            visible = false; visit++;
            foreach (var section in new[] { tv, friends, previous })
            {
                section.State = section.State.Reduce(section.State.HasContent ? AsyncResult.Ready : AsyncResult.Empty, Now); section.View.Render(section.State, Now);
            }
        }
        private void AccountChanged(AccountInfo info)
        {
            visit++; Clear(tv); Clear(friends); Clear(previous); roster.Clear(); showOffline = false; account = info?.PlayerId;
            if (visible && social != null) Reload();
        }
        private static void Clear(Section section)
        {
            section.State = new AsyncState(); section.View.Content.Clear(); section.View.Render(section.State.Reduce(AsyncResult.Empty, Now), Now);
        }
        private void Reload()
        {
            string current = BackendServices.Account.Current?.PlayerId;
            if (account != current) { Clear(tv); Clear(friends); Clear(previous); roster.Clear(); showOffline = false; }
            account = current; visit++; LoadTV(); LoadFriends(); LoadPrevious();
        }
        private void LoadTV() => _ = Load(tv, async () => (await social.GetLiveMatchesAsync())?.FindAll(match => match?.A != null && match.B != null), RenderTV, "No live matches right now.");
        private void LoadFriends() => _ = Load(friends, FetchOnline, rows => { roster = rows; RenderOnline(); }, "No friends or online guild members yet.");
        private void LoadPrevious() => _ = Load(previous, async () => (await history.GetAsync())?.FindAll(entry => entry != null && entry.state == MatchRecordState.Settled && entry.won.HasValue), RenderHistory, "No battles yet.");
        private async Task Load<T>(Section section, Func<Task<List<T>>> fetch, Action<List<T>> render, string empty)
        {
            if (!visible) return;
            int generation = visit, request = ++section.Request; string requestedFor = BackendServices.Account.Current?.PlayerId;
            section.State = section.State.Reduce(AsyncResult.Loading, Now); section.View.Render(section.State, Now);
            try
            {
                var rows = await fetch();
                if (!Active(generation, section, request, requestedFor)) return;
                section.View.Content.Clear();
                bool hasRows = rows != null && rows.Count > 0;
                if (hasRows) render(rows);
                section.State = section.State.Reduce(hasRows ? AsyncResult.Ready : AsyncResult.Empty, Now); section.View.Render(section.State, Now, empty);
            }
            catch (Exception)
            {
                if (!Active(generation, section, request, requestedFor)) return;
                section.State = section.State.Reduce(AsyncResult.Offline, Now); section.View.Render(section.State, Now, "You're offline. Connect to refresh this card.");
            }
        }
        private bool Active(int generation, Section section, int request, string requestedFor) => visible && visit == generation && section.Request == request && requestedFor == BackendServices.Account.Current?.PlayerId;
        private async Task<List<OnlinePlayer>> FetchOnline()
        {
            var friendsTask = social.GetFriendsAsync(); var guildTask = guild.GetMineAsync();
            await Task.WhenAll(friendsTask, guildTask);
            var cards = friendsTask.Result?.FindAll(card => card != null) ?? new List<PlayerCard>(); cards.Sort(FriendPresenceOrder.Instance);
            var result = new List<OnlinePlayer>(); var ids = new List<string>();
            foreach (var card in cards) { result.Add(new OnlinePlayer { Card = card }); ids.Add(card.PlayerId); }
            foreach (var member in GuildMembers.Sorted(guildTask.Result?.Members, GuildMemberOrder.OnlineByRR))
            {
                if (!member.Online || ids.Contains(member.Player.PlayerId) || member.Player.PlayerId == account) continue;
                var card = member.Player;
                result.Add(new OnlinePlayer { Guild = true, Card = new PlayerCard { PlayerId = card.PlayerId, Name = card.Name, Tag = card.Tag,
                    RR = member.RR, Arena = card.Arena, Presence = card.Presence == PlayerPresence.InBattle ? PlayerPresence.InBattle : PlayerPresence.Online } });
                ids.Add(card.PlayerId);
            }
            return result;
        }
        private void RenderTV(List<LiveMatch> matches)
        {
            for (int i = 0; i < 4; i++)
            {
                LiveMatch match = i < matches.Count ? matches[i] : null;
                var row = new VisualElement(); row.AddToClassList("g2-live-row");
                row.Add(MiniPlayer(match?.A, "g2-player--p0"));
                var vs = GuildViews.Button("VS", () => Debug.Log("[Stub] Watch match " + match.MatchId)); vs.AddToClassList("g2-vs"); vs.SetEnabled(match != null); row.Add(vs);
                row.Add(MiniPlayer(match?.B, "g2-player--p1")); tv.View.Content.Add(row);
            }
        }
        private Button MiniPlayer(PlayerCard player, string colour)
        {
            var button = new Button(() => openProfile?.Invoke(player?.PlayerId)); button.AddToClassList("mk-card"); button.AddToClassList("g2-live-player"); button.AddToClassList(colour);
            button.Add(GuildViews.Text(player == null ? "\u2014" : PlayerCardViews.Handle(player), "g2-player-name"));
            button.Add(GuildViews.Text(player == null ? "\u2014" : new RankDisplay(player.RR).Name + " \u00b7 " + player.RR + " RR", "mk-cap"));
            button.SetEnabled(player != null); return button;
        }
        private void RenderOnline()
        {
            var host = friends.View.Content; host.Clear(); bool guildHeading = false; int offline = 0;
            foreach (var entry in roster)
            {
                var player = entry.Card;
                if (!entry.Guild && player.Presence == PlayerPresence.Offline) { offline++; if (!showOffline) continue; }
                if (entry.Guild && !guildHeading) { host.Add(GuildViews.Text("GUILD MEMBERS", "mk-eyebrow")); guildHeading = true; }
                var row = new MkPlayerRow(PlayerCardViews.Handle(player), player.Presence == PlayerPresence.InBattle ? "In battle" : player.Presence.ToString());
                row.AddToClassList("g2-online-player"); row.EnableInClassList("g2-offline", player.Presence == PlayerPresence.Offline);
                row.RegisterCallback<ClickEvent>(evt => { if (!(evt.target is Button)) openProfile?.Invoke(player.PlayerId); });
                row.focusable = true; row.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Return) { openProfile?.Invoke(player.PlayerId); evt.StopPropagation(); } });
                if (player.Presence == PlayerPresence.InBattle)
                {
                    var watch = GuildViews.Button("Spectate", () => Debug.Log("[Stub] Spectate player " + player.PlayerId), "mk-btn--sm"); watch.AddToClassList("g2-urgent");
                    watch.RegisterCallback<ClickEvent>(evt => evt.StopPropagation()); row.Trailing.Add(watch);
                }
                else
                {
                    var dot = new VisualElement { pickingMode = PickingMode.Ignore }; dot.AddToClassList("g2-online-dot"); row.Trailing.Add(dot);
                }
                host.Add(row);
            }
            if (offline > 0) host.Add(GuildViews.Button((showOffline ? "Hide offline (" : "Show offline (") + offline + ")", () => { showOffline = !showOffline; RenderOnline(); }));
        }
        private void RenderHistory(List<MatchHistoryEntry> matches)
        {
            var sorted = new List<MatchHistoryEntry>(matches);
            sorted.Sort((a, b) => { int order = b.createdUnixSeconds.CompareTo(a.createdUnixSeconds); return order != 0 ? order : StringComparer.Ordinal.Compare(a.matchId, b.matchId); });
            foreach (var match in sorted)
            {
                var row = new VisualElement(); row.AddToClassList("g2-history-row");
                var result = GuildViews.Text(match.won.Value ? "W" : "L"); result.AddToClassList("g2-result-badge"); result.AddToClassList(match.won.Value ? "g2-gain" : "g2-loss"); row.Add(result);
                row.Add(GuildViews.Text("You"));
                var replay = GuildViews.Button("VS", () => Debug.Log("[Stub] Replay match " + match.matchId), "mk-btn--sm"); replay.AddToClassList("g2-vs"); row.Add(replay);
                var opponent = new PlayerCard { PlayerId = match.opponentPlayerId, Name = match.OpponentName, Tag = match.OpponentTag };
                var name = GuildViews.Button(PlayerCardViews.Handle(opponent), () => openProfile?.Invoke(opponent.PlayerId)); name.AddToClassList("g2-history-name"); row.Add(name);
                var delta = GuildViews.Text(match.rrDelta.HasValue ? (match.rrDelta.Value >= 0 ? "+" : "") + match.rrDelta.Value + " RR" : "\u2014");
                delta.AddToClassList("g2-rr-delta"); delta.AddToClassList(match.rrDelta.GetValueOrDefault() >= 0 ? "g2-positive" : "g2-negative"); row.Add(delta);
                previous.View.Content.Add(row);
            }
        }
    }
}
