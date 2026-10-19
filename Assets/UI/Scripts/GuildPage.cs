using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace NodeWar.Lobby
{
    public sealed class GuildPage : LobbyPage
    {
        private readonly IGuildService service;
        private readonly GuildBrowseCache cache;
        private readonly GuildDataView mine = new GuildDataView();
        private readonly GuildDataView search = new GuildDataView();
        private readonly GuildDataView browse = new GuildDataView();
        private readonly GuildDataView details = new GuildDataView();
        private readonly VisualElement visitor;
        private readonly VisualElement home;
        private readonly VisualElement browseCard;
        private readonly VisualElement results;
        private readonly TextField query;
        private readonly Button browseToggle;
        private readonly Button chatButton;
        private readonly GuildSheetHost sheet;
        private readonly Action<string> openProfile;
        private readonly GuildChatScreen chat;
        private GuildFilter filter = new GuildFilter();
        private GuildView current;
        private bool visible;
        private bool browsing;
        private bool searched;
        private int visit;

        public bool HasGuild => current?.Summary != null;
        public event Action MembershipChanged;

        public GuildPage(IGuildService service, GuildBrowseCache cache, LobbyOverlayRouter router, VisualElement overlays,
            LobbySheet sheet, Action<string> openProfile) : base(LobbyPageID.Guild, Build())
        {
            this.service = service; this.cache = cache; this.sheet = new GuildSheetHost(sheet); this.openProfile = openProfile;
            chat = new GuildChatScreen(service, router, overlays);
            Root.userData = this;
            visitor = Root.Q("guild-visitor"); visitor.RemoveFromHierarchy();
            visitor.RegisterCallback<GeometryChangedEvent>(_ => PlaceSearch(false));
            home = Root.Q("guild-home"); home.RemoveFromHierarchy();
            mine.AddToClassList("lb-guild-membership"); mine.Content.AddToClassList("lb-guild-membership-content");
            Root.Q("guild-state").Add(mine);
            query = visitor.Q<TextField>("guild-query");
            query.Query<TextElement>().ForEach(element => element.enableRichText = false);
            visitor.Q("guild-search-results").Add(search);
            visitor.Q("guild-browse-results").Add(browse);
            browseCard = visitor.Q("guild-browse-card");
            results = visitor.Q("guild-results-scroll");
            browseToggle = visitor.Q<Button>("guild-browse-toggle");
            browseToggle.clicked += () =>
            {
                browsing = !browsing; UpdateVisitor();
                if (browsing) LoadBrowse(false);
            };
            visitor.Q<Button>("guild-search").clicked += Search;
            query.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) { Search(); evt.StopPropagation(); }
            });
            visitor.Q<Button>("guild-refresh").clicked += () => LoadBrowse(true);
            visitor.Q<Button>("guild-filter").clicked += OpenFilter;
            GuildViews.Icon(visitor.Q<Button>("guild-filter"), LobbyIconKind.Funnel);
            GuildViews.Icon(visitor.Q<Button>("guild-search"), LobbyIconKind.Search);
            chatButton = Root.Q<Button>("guild-chat-button");
            chatButton.clicked += () => _ = OpenChatAsync();
            GuildViews.Icon(chatButton, LobbyIconKind.Chat);
            chatButton.style.display = DisplayStyle.None;
            mine.RetryRequested += Refresh;
            search.RetryRequested += Search;
            browse.RetryRequested += () => LoadBrowse(true);
            Root.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                if (evt.target == Root) BackendServices.Account.Changed += AccountChanged;
            });
            Root.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target != Root) return;
                BackendServices.Account.Changed -= AccountChanged;
                OnHide(); chat.Root.RemoveFromHierarchy();
            });
        }

        private static VisualElement Build()
        {
            var root = new VisualElement { name = "page-guild" };
            Resources.Load<VisualTreeAsset>("Lobby/GuildPage").CloneTree(root);
            return root;
        }

        public override void OnShow() { visible = true; Refresh(); }
        public override void OnHide()
        {
            visible = false; visit++;
            mine.Invalidate(); search.Invalidate(); browse.Invalidate(); details.Invalidate(true);
            CloseSheet(); chat.Close();
        }

        private void AccountChanged(AccountInfo info)
        {
            current = null; visit++; searched = browsing = false;
            mine.Invalidate(true); search.Invalidate(true); browse.Invalidate(true); details.Invalidate(true);
            CloseSheet(); chat.Close();
            chatButton.style.display = DisplayStyle.None;
            MembershipChanged?.Invoke();
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
            if (visible && visit == token && account == BackendServices.Account.Current?.PlayerId && HasGuild)
                chat.Open(current);
        }

        private Task RefreshAsync()
        {
            visit++;
            chat.Close(); CloseSheet(); details.Invalidate(true);
            search.Invalidate(); browse.Invalidate();
            return mine.Load(service.GetMineAsync, RenderMembership, _ => true, "You're not in a guild.");
        }

        private void RenderMembership(GuildView guild)
        {
            current = guild;
            chatButton.style.display = HasGuild ? DisplayStyle.Flex : DisplayStyle.None;
            MembershipChanged?.Invoke();
            if (!HasGuild)
            {
                mine.Content.Add(visitor); UpdateVisitor();
                if (searched) Search();
                if (browsing) LoadBrowse(false);
                return;
            }
            mine.Content.Add(home);
            var header = home.Q("guild-home-header"); header.Clear(); header.Add(GuildViews.Header(guild));
            var scroll = home.Q<ScrollView>("guild-home-scroll"); scroll.Clear();
            var announcements = GuildViews.Card("Announcements");
            RenderPosts(announcements, guild.Pinned, true); RenderPosts(announcements, guild.Feed, false);
            scroll.Add(announcements);
        }
        private static void RenderPosts(VisualElement host, List<GuildPost> posts, bool pinned)
        {
            if (posts == null) return;
            foreach (var post in posts)
            {
                if (post == null) continue;
                var row = GuildViews.Post(post, pinned);
                if (post.Kind == GuildPostKind.Invite)
                    row.Add(GuildViews.Button("Join", () => Debug.Log("[Stub] Guild join invite " + post.PostId)));
                else if (post.Kind == GuildPostKind.InBattle)
                    row.Add(GuildViews.Button("Spectate", () => Debug.Log("[Stub] Guild spectate " + post.Player?.PlayerId)));
                host.Add(row);
            }
        }

        private void PlaceSearch(bool animate)
        {
            var anchor = visitor.Q("guild-search-anchor");
            float top = searched ? 8 : Mathf.Max(8, visitor.layout.height * 0.38f - 50);
            if (float.IsNaN(top)) return;
            if (animate) anchor.experimental.animation.Start(new StyleValues { marginTop = top }, 250);
            else anchor.style.marginTop = top;
        }
        private void UpdateVisitor()
        {
            visitor.EnableInClassList("lb-guild-visitor--searched", searched);
            browseCard.style.display = browsing && !searched ? DisplayStyle.Flex : DisplayStyle.None;
            search.style.display = searched ? DisplayStyle.Flex : DisplayStyle.None;
            results.style.display = browsing || searched ? DisplayStyle.Flex : DisplayStyle.None;
            browseToggle.text = browsing ? "⌃" : "⌄";
            browseToggle.style.display = searched ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void Search()
        {
            if (!visible || HasGuild) return;
            bool animate = !searched; searched = true; UpdateVisitor(); PlaceSearch(animate);
            string text = query.value;
            GuildFilter selected = SnapshotFilter();
            _ = search.Load(() => service.SearchAsync(text, selected), rows => RenderRows(search.Content, rows),
                rows => rows != null && rows.Count > 0, "No guilds match this search. Try another name or filter.");
        }

        private void LoadBrowse(bool refresh)
        {
            if (!visible || HasGuild) return;
            GuildFilter selected = SnapshotFilter(); selected.JoinableOnly = true;
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
                if (guild != null) host.Add(GuildViews.GuildRow(guild, () => OpenPreview(guild)));
        }
        private VisualElement SheetContent(string title)
        {
            var content = new VisualElement(); content.AddToClassList("lb-guild-sheet");
            content.styleSheets.Add(Resources.Load<StyleSheet>("Lobby/Guild"));
            var heading = PlayerCardViews.Text(title); heading.AddToClassList("mk-h2"); content.Add(heading);
            return content;
        }

        private void ShowSheet(VisualElement content, bool large = false)
        {
            sheet.Open(content, large);
        }

        private void CloseSheet()
        {
            sheet.Close();
        }

        private void OpenPreview(GuildSummary summary)
        {
            details.Invalidate(true);
            var content = SheetContent("Guild"); content.Add(details);
            details.RetryRequested -= RetryPreview;
            previewId = summary.GuildId; details.RetryRequested += RetryPreview;
            ShowSheet(content, true); LoadPreview();
        }

        private string previewId;
        private void RetryPreview() { if (sheet.IsShowing) LoadPreview(); }
        private void LoadPreview()
        {
            string id = previewId;
            _ = details.Load(() => service.GetAsync(id), guild =>
            {
                details.Content.Add(GuildViews.Header(guild));
                details.Content.Add(PlayerCardViews.Text(guild.Summary.Description));
                var list = new ScrollView { verticalScrollerVisibility = ScrollerVisibility.Hidden }; list.AddToClassList("lb-guild-panel-members");
                foreach (var member in GuildMembers.Sorted(guild.Members, GuildMemberOrder.OnlineByRR))
                    list.Add(GuildViews.Member(member, () => { CloseSheet(); openProfile(member.Player.PlayerId); }));
                details.Content.Add(list);
                if (!HasGuild)
                {
                    var join = GuildViews.Button("Join", () => Debug.Log("[Stub] Guild join " + id), "mk-btn--primary");
                    join.SetEnabled(guild.Summary.Joinable && guild.Summary.Members < guild.Summary.Capacity);
                    details.Content.Add(join);
                }
            }, guild => guild?.Summary != null, "This guild is unavailable.");
        }

        private void OpenFilter()
        {
            var content = SheetContent("Filter guilds");
            var joinable = new Toggle("Joinable only") { value = filter.JoinableOnly };
            var rr = new IntegerField("Min RR") { value = filter.MinRR }; rr.AddToClassList("mk-card");
            var members = new IntegerField("Min members") { value = filter.MinMembers }; members.AddToClassList("mk-card");
            content.Add(joinable); content.Add(rr); content.Add(members);
            content.Add(GuildViews.Button("Apply", () =>
            {
                filter = new GuildFilter { JoinableOnly = joinable.value, MinRR = Math.Max(0, rr.value), MinMembers = Math.Max(0, members.value) };
                CloseSheet();
                if (searched) Search();
                if (browsing) LoadBrowse(false);
            }));
            ShowSheet(content);
        }

        private void OpenMember(GuildMember member) => openProfile(member.Player.PlayerId);
    }
}