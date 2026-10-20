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
        private readonly GuildReportScreen report;
        private readonly LobbyOverlayRouter router;
        private readonly VisualElement filterLayer = new VisualElement();
        private readonly VisualElement filterBubble = new VisualElement();
        private ScrollView filterContent;
        private VisualElement outsideTapRoot;
        private ValueAnimation<StyleValues> searchMotion, resultsMotion;
        private IVisualElementScheduledItem querySearch;
        private int minMembers = 1, maxMembers = 25;
        private GuildFilter filter = new GuildFilter();
        private GuildView current;
        public GuildView CurrentGuild => HasGuild ? current : null;
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
            this.router = router;
            Root.AddToClassList("ui-guild");
            filterLayer.AddToClassList("ui-filter-layer"); filterBubble.AddToClassList("ui-filter-bubble");
            filterLayer.Add(filterBubble); Root.Add(filterLayer); filterLayer.style.display = DisplayStyle.None;
            filterLayer.RegisterCallback<PointerDownEvent>(evt => { if (evt.target == filterLayer) { CloseFilter(); evt.StopPropagation(); } });
            filterBubble.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            Root.RegisterCallback<GeometryChangedEvent>(_ => { if (filterLayer.resolvedStyle.display != DisplayStyle.None) PositionFilter(); });
            chat = new GuildChatScreen(service, router, overlays);
            report = new GuildReportScreen(router, overlays);
            Root.userData = this;
            details.AddToClassList("lb-guild-preview");
            visitor = Root.Q("guild-visitor"); visitor.RemoveFromHierarchy();
            visitor.RegisterCallback<GeometryChangedEvent>(_ => PlaceSearch(false));
            visitor.Q("guild-search-anchor").RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (filterLayer.resolvedStyle.display != DisplayStyle.None) PositionFilter();
            });
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
                ExpandSearch();
            };
            query.RegisterCallback<FocusInEvent>(_ => ExpandSearch());
            query.RegisterValueChangedCallback(evt =>
            {
                querySearch?.Pause();
                if (string.IsNullOrEmpty(evt.newValue)) { CollapseSearch(); return; }
                ExpandSearch();
                querySearch = Root.schedule.Execute(Search).StartingIn(180);
            });
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
                OnHide(); chat.Root.RemoveFromHierarchy(); report.Root.RemoveFromHierarchy();
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
            CollapseSearch();
            querySearch?.Pause(); StopMotion(ref searchMotion); StopMotion(ref resultsMotion);
            results.style.opacity = 0; results.style.visibility = Visibility.Hidden; PlaceSearch(false);
            CloseFilter(); router.Closed("guild-search-state");
            visible = false; visit++;
            mine.Invalidate(); search.Invalidate(); browse.Invalidate(); details.Invalidate(true);
            report.Close(); CloseSheet(); chat.Close();
        }

        private void AccountChanged(AccountInfo info)
        {
            CollapseSearch();
            current = null; visit++; searched = browsing = false;
            mine.Invalidate(true); search.Invalidate(true); browse.Invalidate(true); details.Invalidate(true);
            report.Close(); CloseSheet(); chat.Close();
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
            var header = home.Q("guild-home-header"); header.Clear(); header.Add(GuildViews.Header(guild, true));
            var scroll = home.Q<ScrollView>("guild-home-scroll"); scroll.Clear();
            var announcements = GuildViews.Card("Announcements");
            RenderPosts(announcements, guild.Pinned, true); RenderPosts(announcements, guild.Feed, false);
            if ((guild.Pinned?.Count ?? 0) + (guild.Feed?.Count ?? 0) == 0)
                announcements.Add(PlayerCardViews.Text("No announcements yet."));
            scroll.Add(announcements);
            var members = GuildViews.Card(null);
            var heading = new VisualElement(); heading.AddToClassList("mk-ledger__header");
            var title = PlayerCardViews.Text("Members"); title.AddToClassList("mk-h3"); title.AddToClassList("lb-guild-row-text");
            heading.Add(title);
            var order = GuildMemberOrder.OnlineByRR;
            var roster = new VisualElement();
            Action render = () =>
            {
                roster.Clear();
                foreach (var member in GuildMembers.Sorted(guild.Members, order))
                    roster.Add(GuildViews.Member(member, () => OpenMember(member)));
                if (roster.childCount == 0) roster.Add(PlayerCardViews.Text("No members to show."));
            };
            var sort = new Button { text = "Online · RR" };
            sort.AddToClassList("lb-guild-sort"); sort.AddToClassList("mk-chip");
            sort.clicked += () =>
            {
                order = (GuildMemberOrder)(((int)order + 1) % 3);
                sort.text = order == GuildMemberOrder.OnlineByRR ? "Online · RR" : order == GuildMemberOrder.Seniority ? "Seniority" : "All · RR";
                render();
            };
            heading.Add(sort); members.Add(heading); members.Add(roster); render(); scroll.Add(members);
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
            float offset = searched ? 0 : Mathf.Max(0, visitor.layout.height * 0.38f - 50 - 16);
            if (float.IsNaN(offset)) return;
            StopMotion(ref searchMotion);
            // TODO: use LobbyMotion when the shared lobby motion class lands.
            if (animate) searchMotion = anchor.experimental.animation.Start(new StyleValues { top = offset }, 280).Ease(Easing.OutCubic).KeepAlive();
            else anchor.style.top = offset;
        }
        private void UpdateVisitor()
        {
            visitor.EnableInClassList("lb-guild-visitor--searched", searched);
            browseCard.style.display = DisplayStyle.None;
            search.style.display = DisplayStyle.Flex;
            results.style.display = DisplayStyle.Flex;
            results.pickingMode = searched ? PickingMode.Position : PickingMode.Ignore;
            results.SetEnabled(searched);
            if (searched) results.style.visibility = Visibility.Visible;
            else if (resultsMotion == null) results.style.visibility = Visibility.Hidden;
            browseToggle.text = "";
            browseToggle.style.display = searched ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void Search()
        {
            if (!visible || HasGuild) return;
            ExpandSearch(false);
            string text = query.value;
            GuildFilter selected = SnapshotFilter();
            int low = minMembers, high = maxMembers;
            _ = search.Load(async () =>
            {
                var rows = string.IsNullOrWhiteSpace(text) ? await cache.GetAsync(service) : await service.SearchAsync(text, selected);
                // Member ranges are not part of IGuildService; apply to returned rows locally.
                return rows?.FindAll(guild => GuildFilters.Matches(guild, selected) && guild.Members >= low && guild.Members <= high);
            }, rows => RenderRows(search.Content, rows),
                rows => rows != null && rows.Count > 0, "No guilds match this search. Try another name or filter.");
        }

        private void ExpandSearch(bool load = true)
        {
            if (!visible || HasGuild || searched) return;
            searched = true; browsing = false; UpdateVisitor(); PlaceSearch(true);
            StopMotion(ref resultsMotion);
            resultsMotion = results.experimental.animation.Start(new StyleValues { opacity = 1, top = 0 }, 280).Ease(Easing.OutCubic).KeepAlive();
            router.Open(new LobbyOverlay("guild-search-state", OverlayKind.Menu), CollapseSearch);
            if (load) Search();
        }

        private void CollapseSearch()
        {
            CloseFilter(); querySearch?.Pause();
            searched = browsing = false; query.SetValueWithoutNotify(""); query.Blur();
            search.Invalidate(); router.Closed("guild-search-state"); UpdateVisitor(); PlaceSearch(true);
            StopMotion(ref resultsMotion);
            resultsMotion = results.experimental.animation.Start(new StyleValues { opacity = 0, top = 16 }, 280).Ease(Easing.OutCubic).KeepAlive();
            resultsMotion.OnCompleted(() => { if (!searched) results.style.visibility = Visibility.Hidden; });
        }

        private static void StopMotion(ref ValueAnimation<StyleValues> motion)
        {
            if (motion == null) return;
            motion.Stop(); motion.Recycle(); motion = null;
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
            var content = SheetContent("Guild"); content.AddToClassList("mk-fixed-sheet"); content.Add(details);
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
                details.Content.Add(GuildViews.Text(guild.Summary.Description, "mk-ellipsis"));
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
            if (filterLayer.resolvedStyle.display != DisplayStyle.None) { CloseFilter(); return; }
            filterBubble.Clear();
            var tail = new VisualElement(); tail.AddToClassList("ui-filter-tail"); filterBubble.Add(tail);
            filterContent = new ScrollView { horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden };
            filterContent.AddToClassList("ui-filter-content"); filterBubble.Add(filterContent);
            filterContent.Add(GuildViews.Text("Filter guilds", "mk-h3"));
            var joinable = new Toggle("Joinable only") { value = filter.JoinableOnly };
            var rrLabel = GuildViews.Text("Min RR: " + filter.MinRR);
            var rr = new SliderInt(0, 5000) { value = filter.MinRR };
            rr.showInputField = false;
            rr.RegisterValueChangedCallback(evt => rrLabel.text = "Min RR: " + evt.newValue);
            var membersLabel = GuildViews.Text("Members: " + minMembers + "\u2013" + maxMembers);
            var members = new MinMaxSlider(minMembers, maxMembers, 1, 25);
            members.RegisterValueChangedCallback(evt => membersLabel.text = "Members: " + Mathf.RoundToInt(evt.newValue.x) + "\u2013" + Mathf.RoundToInt(evt.newValue.y));
            filterContent.Add(rrLabel); filterContent.Add(rr); filterContent.Add(membersLabel); filterContent.Add(members); filterContent.Add(joinable);
            var actions = new VisualElement(); actions.AddToClassList("ui-filter-actions");
            actions.Add(GuildViews.Button("Reset", () => { rr.value = 0; members.value = new Vector2(1, 25); joinable.value = false; }));
            actions.Add(GuildViews.Button("Apply", () =>
            {
                minMembers = Mathf.RoundToInt(members.value.x); maxMembers = Mathf.RoundToInt(members.value.y);
                filter = new GuildFilter { JoinableOnly = joinable.value, MinRR = rr.value, MinMembers = minMembers };
                CloseFilter(); Search();
            }));
            filterContent.Add(actions);
            filterLayer.style.display = DisplayStyle.Flex; filterLayer.BringToFront(); PositionFilter();
            outsideTapRoot = Root.panel?.visualTree;
            outsideTapRoot?.RegisterCallback<PointerDownEvent>(FilterOutsideTap, TrickleDown.TrickleDown);
            filterBubble.RegisterCallback<GeometryChangedEvent>(PositionFilterChanged);
            router.Open(new LobbyOverlay("guild-filter-bubble", OverlayKind.Menu), CloseFilter);
        }

        private void PositionFilterChanged(GeometryChangedEvent evt) => PositionFilter();
        private void PositionFilter()
        {
            var button = visitor.Q<Button>("guild-filter");
            var bounds = Root.WorldToLocal(button.worldBound.position);
            float width = Mathf.Min(280, Root.layout.width - 32);
            filterBubble.style.width = width;
            filterBubble.style.left = Mathf.Clamp(bounds.x + button.layout.width - width, 16, Mathf.Max(16, Root.layout.width - width - 16));
            filterBubble.style.top = bounds.y + button.layout.height + 8;
            // Keep controls reachable on short portrait viewports and with a software keyboard.
            filterContent.style.maxHeight = Mathf.Max(44, Root.layout.height - bounds.y - button.layout.height - 8 - 16 - 28);
        }
        private void CloseFilter()
        {
            outsideTapRoot?.UnregisterCallback<PointerDownEvent>(FilterOutsideTap, TrickleDown.TrickleDown);
            outsideTapRoot = null;
            filterLayer.style.display = DisplayStyle.None;
            filterBubble.UnregisterCallback<GeometryChangedEvent>(PositionFilterChanged);
            router.Closed("guild-filter-bubble");
        }
        private void FilterOutsideTap(PointerDownEvent evt)
        {
            if (evt.target is VisualElement target && (target == filterBubble || filterBubble.Contains(target) ||
                target == visitor.Q("guild-filter") || visitor.Q("guild-filter").Contains(target))) return;
            CloseFilter(); evt.StopPropagation();
        }

        private void ConfirmKick(GuildMember member)
        {
            var content = SheetContent("Remove from guild?");
            content.Add(GuildViews.Text("Remove " + PlayerCardViews.Handle(member.Player) + " from " + current.Summary.Name + "?", "mk-ellipsis"));
            content.Add(GuildViews.Button("Cancel", CloseSheet));
            content.Add(GuildViews.Button("Kick", () =>
            {
                Debug.Log("[Stub] Guild kick " + member.Player.PlayerId); CloseSheet();
            }, "mk-btn--urgent"));
            ShowSheet(content);
        }

        private void OpenMember(GuildMember member)
        {
            if (!GuildMembers.IsOfficer(current.OwnRole)) { openProfile(member.Player.PlayerId); return; }
            var content = SheetContent(PlayerCardViews.Handle(member.Player));
            content.Add(GuildViews.Button("View player", () => { CloseSheet(); openProfile(member.Player.PlayerId); }));
            content.Add(GuildViews.Button("Kick", () => ConfirmKick(member), "mk-btn--urgent"));
            content.Add(GuildViews.Button("Report", () => report.Open(member, true), "mk-btn--urgent"));
            ShowSheet(content);
        }
    }
}
