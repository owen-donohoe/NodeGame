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
        private readonly Label title, rating, message;
        private readonly Button retry, invite, extra;
        private readonly Dictionary<VisualElement, (PickingMode picking, bool focus)> chrome = new Dictionary<VisualElement, (PickingMode, bool)>();
        private RankedQueuePresenter presenter;
        private LobbyModeSelection mode;
        private int version;
        private bool searching;
        private Action extraAction;
        public VisualElement Root { get; } = new VisualElement { name = "searching-screen", pickingMode = PickingMode.Position };
        public bool IsOpen { get; private set; }
        public event Action InviteRequested;
        public event Action<LobbyModeSelection> PrivateRequested;
        public event Action CancelRequested;
        public event Action ForfeitConfirmed;
        public event Action BotAccepted;

        public SearchingScreen(VisualElement shell, LobbyOverlayRouter router, NavigationController navigation,
            LoadoutCatalog catalog, Action<StageShot> shot)
        {
            this.shell = shell; this.router = router; this.navigation = navigation; this.shot = shot;
            launcher = new MatchLauncher(catalog);
            Root.styleSheets.Add(Resources.Load<StyleSheet>("Lobby/Searching")); Root.AddToClassList("g2-search"); Root.AddToClassList("lb-no-swipe");
            var card = new MkCard { name = "searching-card" }; card.AddToClassList("g2-search-card"); Root.Add(card);
            title = GuildViews.Text("Searching\u2026", "mk-h2"); card.Add(title);
            rating = GuildViews.Text("", "mk-trophy"); card.Add(rating);
            message = GuildViews.Text("", "mk-muted"); card.Add(message);
            invite = GuildViews.Button("Invite", () => InviteRequested?.Invoke(), "mk-btn--primary"); invite.name = "searching-invite"; card.Add(invite);
            retry = GuildViews.Button("Retry", () => _ = StartAsync()); retry.name = "searching-retry"; card.Add(retry);
            extra = GuildViews.Button("", () => extraAction?.Invoke()); card.Add(extra);
            card.Add(GuildViews.Button("Back \u00d7", Close));
            Root.style.display = DisplayStyle.None; shell.Add(Root);
            int dots = 0;
            Root.schedule.Execute(() => { if (IsOpen && searching) title.text = "Searching" + new string('.', dots = (dots + 1) % 4); }).Every(400);
            BackendServices.StateChanged += RefreshRating;
            BackendServices.Account.Changed += AccountChanged;
        }
        public void Open(LobbyModeSelection selection)
        {
            if (IsOpen) Close();
            if (selection.IsPrivate) { router.CloseAll(); PrivateRequested?.Invoke(selection); return; }
            mode = selection; navigation.Show(LobbyPageID.Home); router.CloseAll(); IsOpen = true;
            Root.style.display = DisplayStyle.Flex; Root.BringToFront(); RefreshRating(); SetChrome(true);
            invite.style.display = selection.IsPrivate || selection.Mode == LobbyMode.Team2v2 || selection.Mode == LobbyMode.FreeForAll4 ? DisplayStyle.Flex : DisplayStyle.None;
            router.Open(new LobbyOverlay("searching", OverlayKind.Panel, true), Close);
            shot(StageShot.Home); _ = StartAsync();
        }
        private void AccountChanged(AccountInfo _) => Close();
        private void RefreshRating() => rating.text = BackendServices.LastKnownState?.Rank is RankRecord rank ? rank.RR + " RR" : "\u2014";
        private async System.Threading.Tasks.Task StartAsync()
        {
            int request = ++version; presenter?.Dispose(); presenter = null; launcher.Cancel(); ShowSearching(0);
            if (mode.IsPrivate || mode.Mode == LobbyMode.Team2v2 || mode.Mode == LobbyMode.FreeForAll4 || mode.Mode == LobbyMode.Lan)
            {
                Debug.Log("[Stub] Search " + new HomeModeDisplay(mode).Name); return;
            }
            try
            {
                if (mode.Mode == LobbyMode.Bot)
                {
                    if (!await launcher.LaunchLocalAsync(true) && IsOpen && request == version) ShowFailed(launcher.FailureMessage);
                    return;
                }
                await PendingRankedReports.RetryAsync();
                if (!IsOpen || request != version) return;
                if (!await launcher.PrepareAsync(true)) { if (IsOpen && request == version) ShowFailed(launcher.FailureMessage); return; }
                if (!IsOpen || request != version) return;
                bool rated = mode.Mode == LobbyMode.Ranked1v1;
                IRankedQueueService queue = rated ? BackendServices.RankedQueue : new UgsRankedQueueService(false);
                presenter = new RankedQueuePresenter(queue, this, BackendServices.RankedMatch,
                    BackendServices.PlayerState, () => new MatchLauncherConnection(launcher, rated));
                presenter.BotMatchAccepted += () => { if (IsOpen) _ = PlayBotAsync(); };
                await presenter.StartAsync(Time.realtimeSinceStartupAsDouble);
            }
            catch (Exception ex) { if (IsOpen && request == version) ShowFailed(ex.Message); }
        }
        private async System.Threading.Tasks.Task PlayBotAsync()
        {
            int request = version;
            try
            {
                if (!await launcher.LaunchLocalAsync(true) && IsOpen && request == version) ShowFailed(launcher.FailureMessage);
            }
            catch (Exception ex) { if (IsOpen && request == version) ShowFailed(ex.Message); }
        }
        public void Update() { if (IsOpen) { presenter?.Tick(Time.realtimeSinceStartupAsDouble); launcher.Update(); } }
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false; version++; CancelRequested?.Invoke(); presenter?.Dispose(); presenter = null; launcher.Cancel();
            Root.style.display = DisplayStyle.None; SetChrome(false); router.Closed("searching");
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
            retry.style.display = failed ? DisplayStyle.Flex : DisplayStyle.None;
            extra.style.display = DisplayStyle.None;
        }
        public void ShowIdle() => Show("Ready");
        public void ShowSearching(int seconds) { if (!searching) Show("Searching\u2026"); searching = true; }
        public void ShowFound(string id) => Show("Match found");
        public void ShowConnecting() => Show("Match found", "Connecting\u2026");
        public void ShowFailed(string error) => Show("Search failed", error, true);
        public void ShowCancelled() { if (IsOpen) Close(); }
        public void ShowRequeueing(string detail) { Show("Searching\u2026", detail); searching = true; }
        public void ShowForfeitPrompt()
        {
            Show("Match in progress", "Forfeit the previous match to search again?");
            extra.text = "Forfeit"; extraAction = () => ForfeitConfirmed?.Invoke(); extra.style.display = DisplayStyle.Flex;
        }
        public void ShowWaitingForResult(int seconds) => Show("Waiting for result", seconds + "s");
        public void ShowBlocked(int seconds) => Show("Ranked paused", "Try again in " + seconds + "s");
        public void ShowBotOffer()
        {
            extra.text = "Play a bot"; extraAction = () => BotAccepted?.Invoke(); extra.style.display = DisplayStyle.Flex;
        }
        public void Dispose() { Close(); BackendServices.StateChanged -= RefreshRating; BackendServices.Account.Changed -= AccountChanged; launcher.Dispose(); Root.RemoveFromHierarchy(); }
    }
}
