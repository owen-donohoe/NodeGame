using System;
using System.Threading.Tasks;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>Mode entry panels and local launch; transport and ranked views keep their existing host.</summary>
    public sealed class LobbyPlayFlow : IDisposable
    {
        private readonly LobbyOverlayRouter router;
        private readonly PlayPopup legacy;
        private readonly LobbyToast toast;
        private readonly MatchLauncher local;
        private readonly IMatchHistoryService history;
        private readonly LobbyOverlayPanel invite, search, choice, starting;
        private readonly VisualElement inviteRoot = new VisualElement();
        private readonly VisualElement searchRoot = new VisualElement();
        private readonly AsyncStateView recent = new AsyncStateView();
        private readonly AsyncStateView loading = new AsyncStateView();
        private readonly PartyWaitScreen party;
        private AsyncState recentState = new AsyncState();
        private int searchVersion;
        private string recentPlayerId;
        private bool localBusy, disposed;
        private string privateInviteCode;
        public event Action<LobbyMode, string> PartyRequested;

        public LobbyPlayFlow(VisualElement shell, LobbyOverlayRouter router, PlayPopup legacy,
            LoadoutCatalog catalog, LobbyToast toast, IMatchHistoryService history)
        {
            this.router = router; this.legacy = legacy; this.toast = toast; this.history = history;
            local = new MatchLauncher(catalog);
            party = new PartyWaitScreen(shell, router);
            PartyRequested += (mode, player) => party.Open(mode, player);
            party.InviteRequested += () =>
            {
                Debug.Log("[Party] Invite placeholder member (not implemented)");
                party.SetFilled(party.Filled + 1);
            };
            invite = Panel(shell, "Invite to 1v1", "private-invite");
            search = Panel(shell, "Search players", "invite-search");
            choice = Panel(shell, "Party", "party-choice");
            starting = Panel(shell, "Starting a Bot match", "bot-starting");
            starting.Content.Add(loading);
            starting.Closed += () => { if (localBusy) local.Cancel(); };
            if (legacy != null) legacy.BotRequested += StartBot;
            Resources.Load<VisualTreeAsset>("Lobby/PrivateInvite")?.CloneTree(inviteRoot);
            invite.Content.Add(inviteRoot);
            Bind(inviteRoot, "invite-friends", () => ShowInviteEmpty("No friends to invite"));
            Bind(inviteRoot, "invite-guild", () => ShowInviteEmpty("You're not in a guild"));
            Bind(inviteRoot, "invite-search", OpenSearch);
            Bind(inviteRoot, "invite-code", () => { if (privateInviteCode == null) OpenCode(false); else { GUIUtility.systemCopyBuffer = privateInviteCode; toast?.Show("Code copied"); } });
            Resources.Load<VisualTreeAsset>("Lobby/PlayerSearch")?.CloneTree(searchRoot);
            search.Content.Add(searchRoot);
            searchRoot.Q<VisualElement>("recent-opponents")?.Add(recent);
            Bind(searchRoot, "player-search-go", () => Debug.Log("[Invite] Search Name#TAG: " + searchRoot.Q<TextField>("player-search")?.value));
            Bind(searchRoot, "search-code", () => OpenCode(false));
            recent.RetryRequested += () => { _ = LoadRecentAsync(); };
            search.Closed += () => searchVersion++;
        }

        private LobbyOverlayPanel Panel(VisualElement shell, string title, string id)
        {
            var panel = new LobbyOverlayPanel(title);
            shell.Add(panel.Root);
            panel.Opened += () =>
            {
                panel.Root.BringToFront();
                shell.Q<VisualElement>("toast-dock")?.BringToFront();
                router.Open(new LobbyOverlay(id, OverlayKind.Sheet, true), panel.Close);
            };
            panel.Closed += () => router.Closed(id);
            return panel;
        }

        private static long Now() => (long)(Time.realtimeSinceStartupAsDouble * 1000);
        private static void Bind(VisualElement root, string name, Action action)
        {
            var button = root.Q<Button>(name);
            if (button != null) button.clicked += action;
        }

        public void Play(LobbyModeSelection selection)
        {
            if (disposed || localBusy) return;
            switch (selection.PlayAction)
            {
                case LobbyPlayAction.Ranked:
                    if (legacy != null) legacy.OpenRankedFlow(); else toast?.Show("Ranked layout is not assigned");
                    break;
                case LobbyPlayAction.Bot: StartBot(); break;
                case LobbyPlayAction.Lan: OpenCode(true); break;
                case LobbyPlayAction.PrivateInvite:
                    privateInviteCode = null;
                    var inviteCodeButton = inviteRoot.Q<Button>("invite-code");
                    if (inviteCodeButton != null) inviteCodeButton.text = "Use a join code instead";
                    ShowInviteEmpty("No friends to invite"); invite.Open(); break;
                case LobbyPlayAction.TeamChoice:
                    ShowChoice(("Invite guild member", () => RequestParty(LobbyMode.Team2v2, "Guild member")),
                        ("Invite friend", () => RequestParty(LobbyMode.Team2v2, "Friend")),
                        ("Continue without", () => Debug.Log("[Party] 2v2 random queue (not implemented)")));
                    break;
                case LobbyPlayAction.FreeForAllChoice:
                    ShowChoice(("Random", () => Debug.Log("[Party] 1v1v1v1 random queue (not implemented)")),
                        ("Invite", () => RequestParty(LobbyMode.FreeForAll4, "Player")));
                    break;
            }
        }

        public void OpenPrivateInvite(string joinCode)
        {
            privateInviteCode = joinCode;
            var button = inviteRoot.Q<Button>("invite-code");
            if (button != null) button.text = "Copy lobby join code";
            ShowInviteEmpty("Share the code with friends or guild members" );
            invite.Open();
        }

        private void RequestParty(LobbyMode mode, string source)
        {
            Debug.Log("[Party] Invite " + source + " (not implemented)");
            PartyRequested?.Invoke(mode, source);
        }

        private void ShowChoice(params (string Label, Action Action)[] options)
        {
            choice.Content.Clear();
            foreach (var option in options)
                choice.Content.Add(new PaperButton(() => { choice.Close(); option.Action(); }) { text = option.Label });
            choice.Open();
        }

        private void ShowInviteEmpty(string message)
        {
            var results = inviteRoot.Q<VisualElement>("invite-results");
            if (results == null) return;
            results.Clear();
            var view = new AsyncStateView();
            view.Render(new AsyncState().Reduce(AsyncResult.Empty, 0), 0, message);
            results.Add(view);
        }

        private void OpenCode(bool lan)
        {
            if (legacy == null) { toast?.Show("Join-code layout is not assigned"); return; }
            invite.Close(); search.Close(); choice.Close();
            legacy.OpenJoinCode(lan);
        }

        private void OpenSearch()
        {
            search.Open();
            _ = LoadRecentAsync();
        }

        private async Task LoadRecentAsync()
        {
            int version = ++searchVersion;
            string playerId = BackendServices.Account.Current?.PlayerId;
            if (recentPlayerId != playerId)
            {
                recentPlayerId = playerId;
                recent.Content.Clear();
                recentState = new AsyncState(Now());
            }
            recentState = recentState.Reduce(AsyncResult.Retry, Now());
            recent.Render(recentState, Now());
            try
            {
                string[] opponents = RecentOpponentList.HandlesFrom(await history.GetAsync());
                if (!IsSearchCurrent(version, playerId)) return;
                recent.Content.Clear();
                foreach (string opponent in opponents) recent.Content.Add(new Label(opponent) { enableRichText = false });
                recentState = recentState.Reduce(opponents.Length == 0 ? AsyncResult.Empty : AsyncResult.Ready, Now());
                recent.Render(recentState, Now(), "No recent opponents");
            }
            catch (Exception)
            {
                if (!IsSearchCurrent(version, playerId)) return;
                recentState = recentState.Reduce(AsyncResult.Offline, Now());
                recent.Render(recentState, Now(), "Couldn't load recent opponents");
            }
        }

        private async void StartBot()
        {
            if (disposed || localBusy) return;
            legacy?.Hide();
            localBusy = true;
            loading.Render(new AsyncState(Now()), Now());
            starting.Open();
            try
            {
                if (!await local.LaunchLocalAsync(true) && !disposed && starting.IsOpen) toast?.Show(local.FailureMessage);
            }
            catch (Exception ex) { if (!disposed) toast?.Show(ex.Message); }
            finally { localBusy = false; if (!disposed) starting.Close(); }
        }

        public void Update()
        {
            invite.Update(); search.Update(); choice.Update(); starting.Update(); local.Update(); party.Update();
        }

        private bool IsSearchCurrent(int version, string playerId) => !disposed && version == searchVersion && search.IsOpen
            && playerId == BackendServices.Account.Current?.PlayerId;

        public void Dispose()
        {
            disposed = true; searchVersion++;
            party.Dispose();
            if (legacy != null) legacy.BotRequested -= StartBot;
            invite.Close(); search.Close(); choice.Close(); starting.Close();
            local.Dispose();
            invite.Root.RemoveFromHierarchy(); search.Root.RemoveFromHierarchy();
            choice.Root.RemoveFromHierarchy(); starting.Root.RemoveFromHierarchy();
        }
    }
}
