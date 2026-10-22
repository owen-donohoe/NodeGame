using System;
using System.Collections.Generic;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>Searching owns one queue attempt; sheets can be stacked above it.</summary>
    public sealed class SearchingScreen : IRankedQueueView, IDisposable
    {
        private readonly LobbyOverlayRouter router;
        private readonly NavigationController navigation;
        private readonly Action<StageShot> shot;
        private readonly VisualElement shell;
        private readonly MatchLauncher launcher;
        private readonly Label title, rating, era, message, modeName, elapsed;
        private readonly VisualElement content, boardGap, actions;
        private readonly LobbyElementMotion offerMotion;
        private LobbyMotion.Spring rowSplit;
        private double rowSampled;
        private bool fakeOfferShown, hasOffer;
        private readonly Action<float, float> boardBand;
        private double startedAt;
        private StyleEnum<Visibility> pageVisibility;
        private readonly Button invite, offer;
        private readonly Dictionary<VisualElement, (PickingMode picking, bool focus)> chrome = new Dictionary<VisualElement, (PickingMode, bool)>();
        private RankedQueuePresenter presenter;
        private LobbyModeSelection mode;
        private int version;
        private bool searching;
        private Action extraAction;
        public VisualElement Root { get; } = new VisualElement { name = "searching-screen", pickingMode = PickingMode.Position };
        public bool IsOpen { get; private set; }
        public event Action InviteRequested;
        public event Action LanRequested;
        public event Action<LobbyModeSelection> PrivateRequested;
        public event Action CancelRequested;
        public event Action ForfeitConfirmed;
        public event Action BotAccepted;

        public SearchingScreen(VisualElement shell, LobbyOverlayRouter router, NavigationController navigation,
            LoadoutCatalog catalog, Action<StageShot> shot, Action<float, float> boardBand = null)
        {
            this.shell = shell; this.router = router; this.navigation = navigation; this.shot = shot; this.boardBand = boardBand;
            launcher = new MatchLauncher(catalog);
            Root.styleSheets.Add(Resources.Load<StyleSheet>("Lobby/Searching")); Root.AddToClassList("g2-search"); Root.AddToClassList("lb-no-swipe");
            content = new VisualElement { name = "searching-content" }; content.AddToClassList("g2-search-content"); Root.Add(content);
            modeName = GuildViews.Text("", "mk-page-title"); modeName.AddToClassList("g2-search-mode"); content.Add(modeName);
            boardGap = new VisualElement { name = "queue-board", pickingMode = PickingMode.Ignore };
            boardGap.AddToClassList("g2-search-board"); content.Add(boardGap);
            var footer = new VisualElement(); footer.AddToClassList("g2-search-footer"); content.Add(footer);
            title = GuildViews.Text("Searching", "g2-search-subtitle"); footer.Add(title);
            elapsed = GuildViews.Text("00:00", "g2-search-time"); footer.Add(elapsed);
            var stats = new VisualElement(); stats.AddToClassList("g2-search-stats"); footer.Add(stats);
            rating = StatCard(stats, "RR", "g2-search-rr"); era = StatCard(stats, "Era", "g2-search-era");
            message = GuildViews.Text("", "g2-search-message"); footer.Add(message);
            actions = new VisualElement { name = "queue-actions" }; actions.AddToClassList("g2-search-actions"); footer.Add(actions);
            invite = GuildViews.Button("Invite", () => InviteRequested?.Invoke()); invite.name = "searching-invite"; actions.Add(invite);
            offer = GuildViews.Button("", () => extraAction?.Invoke()); offer.name = "searching-offer"; actions.Add(offer);
            offerMotion = new LobbyElementMotion(offer, null, vertical: true); offerMotion.Set(0, 12, 1);
            Root.schedule.Execute(AnimateActions).Every(16);
            var cancel = GuildViews.Button("Cancel", Close); cancel.name = "searching-cancel"; footer.Add(cancel);
            Root.RegisterCallback<GeometryChangedEvent>(_ => LayoutBoard());
            boardGap.RegisterCallback<GeometryChangedEvent>(_ => LayoutBoard());
            foreach (string name in new[] { "top-bar", "strip-wrap" }) shell.Q(name)?.RegisterCallback<GeometryChangedEvent>(_ => LayoutBoard());
            Root.style.display = DisplayStyle.None; shell.Add(Root);
            int dots = 0;
            Root.schedule.Execute(() => { if (IsOpen && searching) title.text = "Searching" + new string('.', dots = (dots + 1) % 4); }).Every(400);
            BackendServices.StateChanged += RefreshRating;
            BackendServices.Account.Changed += AccountChanged;
        }
        public void Open(LobbyModeSelection selection)
        {
            if (IsOpen) return;
            if (selection.LaunchStrategy == ModeLaunchStrategy.Private) { router.CloseAll(); PrivateRequested?.Invoke(selection); return; }
            if (selection.LaunchStrategy == ModeLaunchStrategy.Lan) { router.CloseAll(); LanRequested?.Invoke(); return; }
            mode = selection; navigation.Show(LobbyPageID.Home); router.CloseAll(); IsOpen = true;
            Root.style.display = DisplayStyle.Flex; Root.BringToFront(); RefreshRating(); SetChrome(true);
            modeName.text = selection.Definition.DisplayName;
            startedAt = Time.realtimeSinceStartupAsDouble; elapsed.text = "00:00";
            invite.style.display = QueueSearchPolicy.CanInvite(selection.Mode) ? DisplayStyle.Flex : DisplayStyle.None;
            var pages = shell.Q("page-host");
            if (pages != null) { pageVisibility = pages.style.visibility; pages.style.visibility = Visibility.Hidden; }
            router.Open(new LobbyOverlay("searching", OverlayKind.Fullscreen, true), Close);
            LayoutBoard(); shot(StageShot.Queue); _ = StartAsync();
        }
        private void AccountChanged(AccountInfo _) => Close();
        private static Label StatCard(VisualElement row, string title, string css)
        {
            var card = new MkCard(); card.AddToClassList("g2-search-stat");
            card.Add(GuildViews.Text(title, "g2-search-stat-caption"));
            var label = GuildViews.Text("", "g2-search-stat-label"); label.AddToClassList(css); card.Add(label); row.Add(card); return label;
        }
        private void LayoutBoard()
        {
            if (!IsOpen || Root.panel == null) return;
            float headerBottom = Root.worldBound.yMin;
            foreach (string name in new[] { "top-bar", "strip-wrap" })
            {
                var band = shell.Q(name);
                if (band != null && band.resolvedStyle.display != DisplayStyle.None) headerBottom = Mathf.Max(headerBottom, band.worldBound.yMax);
            }
            content.style.top = Mathf.Max(16, headerBottom - Root.worldBound.yMin + 16);
            var screen = Root.panel.visualTree.worldBound;
            var safe = shell.Q("safe-area");
            content.style.bottom = 24 + (safe?.resolvedStyle.paddingBottom ?? 0);
            boardGap.style.height = screen.height * 0.38f;
            if (screen.height <= 0 || boardGap.worldBound.height <= 16) return;
            boardBand?.Invoke((boardGap.worldBound.yMin - screen.yMin + 8) / screen.height,
                (boardGap.worldBound.yMax - screen.yMin - 8) / screen.height);
        }
        private void RefreshRating()
        {
            var rank = BackendServices.LastKnownState?.Rank;
            rating.text = rank != null ? rank.RR.ToString() : "\u2014";
            era.text = rank != null ? new RankDisplay(rank.RR).Name : "\u2014";
        }
        private async System.Threading.Tasks.Task StartAsync()
        {
            int request = ++version; presenter?.Dispose(); presenter = null; launcher.Cancel();
            startedAt = Time.realtimeSinceStartupAsDouble; elapsed.text = "00:00"; fakeOfferShown = false; searching = false; ShowSearching(0);
            if (QueueSearchPolicy.IsFake(mode.Mode)) return; // Deliberately searches forever without a backend ticket.
            if (!mode.Definition.CanSelect(false) || (mode.LaunchStrategy == ModeLaunchStrategy.Queue && mode.Definition.QueueKey != ModeDefinition.For(LobbyMode.Ranked1v1).QueueKey))
            {
                ShowFailed("Coming soon"); return;
            }
            try
            {
                if (mode.LaunchStrategy == ModeLaunchStrategy.Bot)
                {
                    if (!await launcher.LaunchLocalAsync(true) && IsOpen && request == version) ShowFailed(launcher.FailureMessage);
                    return;
                }
                await PendingRankedReports.RetryAsync();
                if (!IsOpen || request != version) return;
                if (!await launcher.PrepareAsync(true)) { if (IsOpen && request == version) ShowFailed(launcher.FailureMessage); return; }
                if (!IsOpen || request != version) return;
                bool rated = true;
                IRankedQueueService queue = BackendServices.RankedQueue;
                presenter = new RankedQueuePresenter(queue, this, BackendServices.RankedMatch,
                    BackendServices.PlayerState, () => new MatchLauncherConnection(launcher, rated));
                presenter.BotMatchAccepted += () => { if (IsOpen) _ = PlayBotAsync(); };
                await presenter.StartAsync(Time.realtimeSinceStartupAsDouble);
            }
            catch (Exception ex) { if (IsOpen && request == version) ShowFailed(ex.Message); }
        }
        private async System.Threading.Tasks.Task PlayBotAsync()
        {
            int request = version; Show("Starting bot match\u2026"); CancelRequested?.Invoke();
            try
            {
                if (!await launcher.LaunchLocalAsync(true) && IsOpen && request == version) ShowFailed(launcher.FailureMessage);
            }
            catch (Exception ex) { if (IsOpen && request == version) ShowFailed(ex.Message); }
        }
        public void Update()
        {
            if (!IsOpen) return;
            double now = Time.realtimeSinceStartupAsDouble;
            elapsed.text = QueueSearchPolicy.Elapsed(Math.Max(0, (int)(now - startedAt)));
            if (searching && !fakeOfferShown && QueueSearchPolicy.OfferDue(mode.Mode, Math.Max(0, (int)(now - startedAt))))
            { fakeOfferShown = true; ShowBotOffer(); }
            presenter?.Tick(now); launcher.Update();
        }
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false; version++; CancelRequested?.Invoke(); presenter?.Dispose(); presenter = null; launcher.Cancel();
            Root.style.display = DisplayStyle.None; SetChrome(false);
            var pages = shell.Q("page-host"); if (pages != null) pages.style.visibility = pageVisibility;
            router.Closed("searching");
            navigation.Show(LobbyPageID.Home); shot(StageShot.Home);
        }
        private void SetChrome(bool inert)
        {
            if (inert)
            {
                foreach (string name in new[] { "top-bar", "strip-wrap" })
                {
                    var band = shell.Q(name);
                    if (band == null) continue;
                    void Freeze(VisualElement element)
                    {
                        if (!chrome.ContainsKey(element)) chrome[element] = (element.pickingMode, element.focusable);
                        element.pickingMode = PickingMode.Ignore; element.focusable = false; element.Blur();
                    }
                    Freeze(band); band.Query<VisualElement>().ForEach(Freeze);
                }
            }
            else
            {
                foreach (var entry in chrome) { entry.Key.pickingMode = entry.Value.picking; entry.Key.focusable = entry.Value.focus; }
                chrome.Clear();
            }
        }
        private void Show(string text, string detail = null, bool failed = false)
        {
            searching = false; title.text = text; message.text = detail ?? "";
            message.style.display = string.IsNullOrEmpty(detail) ? DisplayStyle.None : DisplayStyle.Flex;
            title.EnableInClassList("g2-search-subtitle--error", failed);
            hasOffer = false; offer.style.display = DisplayStyle.None; offerMotion.Set(0, 12, 1); rowSplit = new LobbyMotion.Spring(0); rowSampled = Time.unscaledTimeAsDouble;
            extraAction = null; AnimateActions();
            if (failed) SetOffer("Retry", () => _ = StartAsync());
        }
        public void ShowIdle() => Show("Ready");
        public void ShowSearching(int seconds) { if (!searching) Show("Searching"); searching = true; }
        public void ShowFound(string id) => Show("Match found");
        public void ShowConnecting() => Show("Match found", "Connecting\u2026");
        public void ShowFailed(string error) => Show("Search failed", error, true);
        public void ShowCancelled() { if (IsOpen) Close(); }
        public void ShowRequeueing(string detail) { Show("Searching", detail); searching = true; }
        public void ShowForfeitPrompt()
        {
            Show("Match in progress", "Forfeit the previous match to search again?");
            SetOffer("Forfeit", () => ForfeitConfirmed?.Invoke());
        }
        public void ShowWaitingForResult(int seconds) => Show("Waiting for result", seconds + "s");
        public void ShowBlocked(int seconds) => Show("Ranked paused", "Try again in " + seconds + "s");
        public void ShowBotOffer()
        {
            SetOffer("Play bot", () =>
            {
                if (QueueSearchPolicy.IsFake(mode.Mode)) { version++; _ = PlayBotAsync(); }
                else BotAccepted?.Invoke();
            });
        }
        private void SetOffer(string text, Action action)
        {
            hasOffer = true; offer.text = text; extraAction = action; offer.style.display = DisplayStyle.Flex;
            rowSplit.Target = 1; rowSampled = Time.unscaledTimeAsDouble;
            offerMotion.To(1, 0, 1, LobbyMotion.OpenSeconds); AnimateActions();
        }
        private void AnimateActions()
        {
            if (!IsOpen) return;
            double now = Time.unscaledTimeAsDouble;
            rowSplit.Advance((float)(now - rowSampled), LobbyMotion.PopDamping); rowSampled = now;
            float width = actions.layout.width;
            if (float.IsNaN(width) || width <= 0) return;
            bool pair = QueueSearchPolicy.CanInvite(mode.Mode) && hasOffer;
            float split = Mathf.Clamp01(rowSplit.Position);
            invite.style.width = pair ? width - (width - 8) / 2 * split - 8 * split : width;
            offer.style.width = pair ? (width - 8) / 2 * split : width;
        }
        public void Dispose() { Close(); BackendServices.StateChanged -= RefreshRating; BackendServices.Account.Changed -= AccountChanged; launcher.Dispose(); Root.RemoveFromHierarchy(); }
    }
}
