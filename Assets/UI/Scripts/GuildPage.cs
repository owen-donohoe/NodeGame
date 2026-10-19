using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    public sealed class GuildPage : LobbyPage
    {
        private readonly IGuildService service;
        private readonly GuildBrowseCache cache;
        private readonly GuildDataView mine = new GuildDataView();
        private readonly GuildDataView search = new GuildDataView();
        private readonly GuildDataView browse = new GuildDataView();
        private readonly VisualElement visitor;
        private readonly TextField query;
        private readonly Foldout browseFold;
        private readonly LobbyOverlayPanel filters;
        private readonly LobbyOverlayPanel preview;
        private readonly GuildChatScreen chat;
        private readonly GuildDataView pinned = new GuildDataView();
        private readonly GuildDataView feed = new GuildDataView();
        private GuildFilter filter = new GuildFilter();
        private GuildView current;
        private bool visible;
        private int visit;

        public GuildPage(IGuildService service, GuildBrowseCache cache, LobbyOverlayRouter router, VisualElement overlays)
            : base(LobbyPageID.Guild, Build())
        {
            this.service = service; this.cache = cache;
            chat = new GuildChatScreen(service, router, overlays);
            Root.userData = this;
            visitor = Root.Q("guild-visitor");
            visitor.RemoveFromHierarchy();
            Root.Q("guild-state").Add(mine);
            query = visitor.Q<TextField>("guild-query");
            visitor.Q("guild-search-results").Add(search);
            visitor.Q("guild-browse-results").Add(browse);
            browseFold = visitor.Q<Foldout>("guild-browse");
            browseFold.value = false;
            browseFold.RegisterValueChangedCallback(evt => { if (evt.newValue && visible) LoadBrowse(false); });
            visitor.Q<Button>("guild-search").clicked += Search;
            query.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) { Search(); evt.StopPropagation(); }
            });
            visitor.Q<Button>("guild-refresh").clicked += () => LoadBrowse(true);
            mine.RetryRequested += Refresh;
            search.RetryRequested += Search;
            browse.RetryRequested += () => LoadBrowse(true);
            filters = new LobbyOverlayPanel("Filter guilds");
            preview = new LobbyOverlayPanel("Guild preview");
            WirePanel(filters, "guild-filter", router, overlays);
            WirePanel(preview, "guild-preview", router, overlays);
            visitor.Q<Button>("guild-filter").clicked += OpenFilter;
            GuildViews.Icon(visitor.Q<Button>("guild-filter"), LobbyIconKind.Funnel);
            GuildViews.Icon(visitor.Q<Button>("guild-search"), LobbyIconKind.Search);
            GuildViews.Icon(visitor.Q<Button>("guild-refresh"), LobbyIconKind.Refresh);
            pinned.RetryRequested += Refresh; feed.RetryRequested += Refresh;
            Root.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                if (evt.target == Root) BackendServices.Account.Changed += AccountChanged;
            });
            Root.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target != Root) return;
                BackendServices.Account.Changed -= AccountChanged;
                OnHide();
                filters.Root.RemoveFromHierarchy(); preview.Root.RemoveFromHierarchy();
                chat.Root.RemoveFromHierarchy();
            });
        }

        private static VisualElement Build()
        {
            var root = new VisualElement { name = "page-guild" };
            Resources.Load<VisualTreeAsset>("Lobby/GuildPage").CloneTree(root);
            return root;
        }

        private static void WirePanel(LobbyOverlayPanel panel, string id, LobbyOverlayRouter router, VisualElement host)
        {
            host.Add(panel.Root);
            panel.Opened += () => { panel.Root.BringToFront(); router.Open(new LobbyOverlay(id, OverlayKind.Panel), panel.Close); };
            panel.Closed += () => router.Closed(id);
        }

        public override void OnShow() { visible = true; Refresh(); }
        public override void OnHide()
        {
            visible = false; visit++;
            mine.Invalidate(); search.Invalidate(); browse.Invalidate();
            filters.Close(); preview.Close();
            pinned.Invalidate(); feed.Invalidate(); chat.Close();
        }

        private void AccountChanged(AccountInfo info)
        {
            current = null; visit++;
            mine.Invalidate(true); search.Invalidate(true); browse.Invalidate(true);
            filters.Close(); preview.Close();
            pinned.Invalidate(true); feed.Invalidate(true); chat.Close();
            if (visible) Refresh();
        }

        public void Refresh() { if (visible) _ = RefreshAsync(); }

        public async Task OpenChatAsync()
        {
            if (!visible) return;
            Task refresh = RefreshAsync();
            int token = visit;
            string account = BackendServices.Account.Current?.PlayerId;
            await refresh;
            if (visible && visit == token && account == BackendServices.Account.Current?.PlayerId && current?.Summary != null)
                chat.Open(current);
        }

        private Task RefreshAsync()
        {
            visit++;
            chat.Close();
            search.Invalidate(); browse.Invalidate();
            current = null;
            return mine.Load(service.GetMineAsync, RenderMembership, _ => true, "You're not in a guild.");
        }

        private void RenderMembership(GuildView guild)
        {
            current = guild;
            if (guild == null)
            {
                mine.Content.Add(visitor);
                Search();
                if (browseFold.value) LoadBrowse(false);
            }
            else
            {
                mine.Content.Add(GuildViews.Header(guild));
                var weekly = new ProgressBar { title = "Weekly task: " + guild.WeeklyProgress + "/" + guild.WeeklyGoal,
                    value = guild.WeeklyGoal > 0 ? Mathf.Clamp01((float)guild.WeeklyProgress / guild.WeeklyGoal) * 100f : 0f };
                mine.Content.Add(weekly);
                mine.Content.Add(PlayerCardViews.Text("Pinned announcements"));
                mine.Content.Add(pinned);
                _ = pinned.Load(() => Task.FromResult(guild.Pinned), rows => RenderPosts(pinned.Content, rows),
                    rows => rows != null && rows.Count > 0, "No pinned announcements.");
                mine.Content.Add(PlayerCardViews.Text("Guild feed"));
                mine.Content.Add(feed);
                _ = feed.Load(() => Task.FromResult(guild.Feed), rows => RenderPosts(feed.Content, rows),
                    rows => rows != null && rows.Count > 0, "No guild posts yet.");
                var openChat = new Button(() => _ = OpenChatAsync()) { text = "Chat" };
                GuildViews.Icon(openChat, LobbyIconKind.Chat);
                mine.Content.Add(openChat);
            }
        }

        private static void RenderPosts(VisualElement host, List<GuildPost> posts)
        {
            foreach (var post in posts)
            {
                if (post == null) continue;
                var row = new VisualElement();
                row.AddToClassList("lb-guild-post");
                if (post.Player != null) row.Add(PlayerCardViews.Text(PlayerCardViews.Handle(post.Player)));
                row.Add(PlayerCardViews.Text(post.Text));
                if (post.Kind == GuildPostKind.Invite)
                    row.Add(new Button(() => Debug.Log("[Guild] join invite " + post.PostId)) { text = "Join" });
                else if (post.Kind == GuildPostKind.InBattle)
                {
                    var spectate = new Button(() => Debug.Log("[Guild] spectate " + post.Player?.PlayerId)) { text = "Spectate" };
                    spectate.AddToClassList("lb-guild-red"); row.Add(spectate);
                }
                host.Add(row);
            }
        }

        private void Search()
        {
            if (!visible || current != null) return;
            string text = query.value;
            GuildFilter selected = SnapshotFilter();
            _ = search.Load(() => service.SearchAsync(text, selected), rows => RenderRows(search.Content, rows),
                rows => rows != null && rows.Count > 0, "No guilds match this search. Try another name or filter.");
        }

        private void LoadBrowse(bool refresh)
        {
            if (!visible || current != null) return;
            GuildFilter selected = SnapshotFilter();
            _ = browse.Load(async () =>
            {
                var all = await cache.GetAsync(service, refresh);
                return all.FindAll(guild => GuildFilters.Matches(guild, selected));
            }, rows => RenderRows(browse.Content, rows), rows => rows.Count > 0, "No public guilds match your filters.");
        }

        private GuildFilter SnapshotFilter() => new GuildFilter { JoinableOnly = filter.JoinableOnly, MinRR = filter.MinRR, MinMembers = filter.MinMembers };

        private void RenderRows(VisualElement host, List<GuildSummary> guilds)
        {
            foreach (var guild in guilds)
            {
                var row = new Button(() => OpenPreview(guild));
                row.AddToClassList("lb-guild-row");
                row.Add(PlayerCardViews.Text("[" + guild.IconId + "] " + guild.Name));
                row.Add(PlayerCardViews.Text(guild.Members + "/" + guild.Capacity));
                var chip = PlayerCardViews.Text(guild.Joinable && guild.Members < guild.Capacity ? "Joinable" : "Closed");
                chip.AddToClassList("lb-guild-chip");
                row.Add(chip);
                host.Add(row);
            }
        }

        private void OpenPreview(GuildSummary guild)
        {
            preview.Content.Clear();
            preview.Content.Add(PlayerCardViews.Text("[" + guild.IconId + "] " + guild.Name));
            preview.Content.Add(PlayerCardViews.Text(guild.Members + "/" + guild.Capacity + " members · Minimum " + guild.MinRR + " RR"));
            var join = new Button(() => Debug.Log("[Guild] join " + guild.GuildId)) { text = "Join" };
            join.SetEnabled(guild.Joinable && guild.Members < guild.Capacity);
            preview.Content.Add(join);
            preview.Open();
        }

        private void OpenFilter()
        {
            filters.Content.Clear();
            var joinable = new Toggle("Joinable only") { value = filter.JoinableOnly };
            var rr = new IntegerField("Min RR") { value = filter.MinRR };
            var members = new IntegerField("Min members") { value = filter.MinMembers };
            filters.Content.Add(joinable); filters.Content.Add(rr); filters.Content.Add(members);
            filters.Content.Add(new Button(() =>
            {
                filter = new GuildFilter { JoinableOnly = joinable.value, MinRR = Math.Max(0, rr.value), MinMembers = Math.Max(0, members.value) };
                filters.Close(); Search();
                if (browseFold.value) LoadBrowse(false);
            }) { text = "Apply" });
            filters.Open();
        }
    }
}
