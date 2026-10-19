using System;
using System.Threading.Tasks;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>A public profile panel. The shell routes Opened/Closed through its overlay stack.</summary>
    public class ProfilePage
    {
        private static readonly string[] StatNames = { "Matches", "Wins", "Losses", "Win rate", "Streak", "Best" };
        private readonly IProfileService profiles;
        private readonly LobbySheet sheet;
        private readonly LobbyToast toast;
        private readonly SafeAreaBinder safeArea;
        private readonly AsyncStateView stateView;
        private readonly Label handle;
        private readonly Label rank;
        private readonly Button addFriend;
        private readonly Button invite;
        private readonly Button rename;
        private readonly Button spectate;
        private readonly VisualElement stats;
        private readonly VisualElement loadout;
        private readonly VisualElement recent;
        private readonly VisualElement renameContent;
        private readonly TextField nameField;
        private readonly TextField tagField;
        private readonly Label nameNote;
        private AsyncState state = new AsyncState();
        private string playerId;
        private string shownAccount;
        private bool viewingSelf;
        private int generation;
        private int renameSession;
        private bool renamePending;
        private static long Now => (long)(Time.realtimeSinceStartupAsDouble * 1000);
        private static string SelfId => BackendServices.Account.Current?.PlayerId ?? "self";
        public VisualElement Root { get; }
        public bool IsOpen { get; private set; }
        public event Action Opened;
        public event Action Closed;
        public event Action Renamed;

        public ProfilePage(VisualTreeAsset layout, LobbySheet sheet, LobbyToast toast, IProfileService profiles = null)
        {
            this.profiles = profiles ?? BackendServices.Profiles;
            this.sheet = sheet;
            this.toast = toast;
            Root = new VisualElement { name = "public-profile", userData = this };
            Resources.Load<VisualTreeAsset>("Lobby/PublicProfile").CloneTree(Root);
            Root.AddToClassList("lb-public-overlay");
            Root.AddToClassList("lb-no-swipe");
            Root.style.display = DisplayStyle.None;
            safeArea = new SafeAreaBinder(Root.Q("public-profile-safe"));
            stateView = new AsyncStateView();
            var body = Root.Q("public-profile-body");
            body.RemoveFromHierarchy();
            stateView.Content.Add(body);
            Root.Q("public-profile-scroll").Add(stateView);
            stateView.RetryRequested += Reload;
            handle = Root.Q<Label>("public-profile-handle");
            handle.enableRichText = false;
            rank = Root.Q<Label>("public-profile-rank");
            stats = Root.Q("public-profile-stats");
            loadout = Root.Q("public-profile-loadout");
            recent = Root.Q("public-profile-recent");
            addFriend = Root.Q<Button>("public-profile-add");
            invite = Root.Q<Button>("public-profile-invite");
            rename = Root.Q<Button>("public-profile-rename");
            spectate = Root.Q<Button>("public-profile-spectate");
            addFriend.Add(new LobbyIcon(LobbyIconKind.AddFriend) { pickingMode = PickingMode.Ignore });
            invite.Add(new LobbyIcon(LobbyIconKind.Invite) { pickingMode = PickingMode.Ignore });
            addFriend.clicked += () => Debug.Log("[Profile] add friend " + playerId);
            invite.clicked += () => Debug.Log("[Profile] invite to mode " + playerId);
            spectate.clicked += () => Debug.Log("[Profile] spectate " + playerId);
            Root.Q<Button>("public-profile-close").clicked += Close;
            Root.Q("public-profile-scrim").RegisterCallback<PointerDownEvent>(evt => { Close(); evt.StopPropagation(); });
            rename.clicked += BeginRename;
            renameContent = LobbySheet.Lift(Root, "profile-rename");
            nameField = renameContent.Q<TextField>("profile-name-field");
            tagField = renameContent.Q<TextField>("profile-tag-field");
            nameNote = renameContent.Q<Label>("profile-name-note");
            nameNote.enableRichText = false;
            nameField.Query<TextElement>().ForEach(element => element.enableRichText = false);
            tagField.Query<TextElement>().ForEach(element => element.enableRichText = false);
            renameContent.Q<Button>("profile-name-save").clicked += CommitRename;
            renameContent.Q<Button>("profile-name-cancel").clicked += () => sheet?.Close();
            nameField.RegisterCallback<KeyDownEvent>(OnNameFieldKeyDown, TrickleDown.TrickleDown);
            tagField.RegisterCallback<KeyDownEvent>(OnNameFieldKeyDown, TrickleDown.TrickleDown);
            Root.RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == Root) Close(); });
        }

        public void Update() { if (IsOpen) safeArea.Update(); }
        public void Open(VisualElement source) => OpenFor(SelfId);

        public void OpenFor(string requestedPlayerId)
        {
            if (string.IsNullOrEmpty(requestedPlayerId)) return;
            bool changed = playerId != requestedPlayerId || shownAccount != SelfId;
            if (changed)
            {
                state = new AsyncState();
                CancelRename();
            }
            playerId = requestedPlayerId;
            viewingSelf = playerId == SelfId;
            shownAccount = SelfId;
            BackendServices.StateChanged -= StateChanged;
            BackendServices.StateChanged += StateChanged;
            BackendServices.Account.Changed -= AccountChanged;
            BackendServices.Account.Changed += AccountChanged;
            if (!IsOpen)
            {
                IsOpen = true;
                Root.style.display = DisplayStyle.Flex;
                Opened?.Invoke();
            }
            Reload();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            generation++;
            CancelRename();
            BackendServices.StateChanged -= StateChanged;
            BackendServices.Account.Changed -= AccountChanged;
            stateView.Render(state.Reduce(state.HasContent ? AsyncResult.Ready : AsyncResult.Empty, Now), Now);
            Root.style.display = DisplayStyle.None;
            Closed?.Invoke();
        }

        private void CancelRename()
        {
            renameSession++;
            if (sheet != null && sheet.IsShowing(renameContent)) sheet.Close();
        }

        private void StateChanged() { if (IsOpen && viewingSelf) Reload(); }

        private void AccountChanged(AccountInfo info)
        {
            if (!IsOpen) return;
            CancelRename();
            if (viewingSelf) playerId = SelfId;
            viewingSelf = playerId == SelfId;
            shownAccount = SelfId;
            state = new AsyncState();
            Reload();
        }

        private void Reload()
        {
            if (!IsOpen) return;
            _ = LoadAsync(++generation, playerId, SelfId);
        }

        private async Task LoadAsync(int request, string id, string requestedFor)
        {
            state = state.Reduce(AsyncResult.Loading, Now);
            stateView.Render(state, Now);
            try
            {
                var profile = await profiles.GetAsync(id);
                if (!Active(request, id, requestedFor)) return;
                if (profile?.Card == null)
                {
                    state = state.Reduce(AsyncResult.Empty, Now);
                    stateView.Render(state, Now, "This player profile isn't available.", LobbyIconKind.Smile);
                    return;
                }
                Render(profile);
                state = state.Reduce(AsyncResult.Ready, Now);
                stateView.Render(state, Now);
            }
            catch (Exception)
            {
                if (!Active(request, id, requestedFor)) return;
                bool offline = BackendServices.SimulatedOffline || Application.internetReachability == NetworkReachability.NotReachable;
                state = state.Reduce(offline ? AsyncResult.Offline : AsyncResult.Error, Now);
                stateView.Render(state, Now, offline ? "You're offline. Connect to load this profile." : "Couldn't load this profile. Try again.");
            }
        }

        private bool Active(int request, string id, string account) => IsOpen && generation == request &&
            playerId == id && SelfId == account;

        private void Render(PublicProfileView profile)
        {
            handle.text = PlayerCardViews.Handle(profile.Card);
            var display = new RankDisplay(profile.Card.RR);
            rank.text = display.Name + " · " + display.RR + " RR";
            addFriend.style.display = invite.style.display = viewingSelf ? DisplayStyle.None : DisplayStyle.Flex;
            rename.style.display = viewingSelf ? DisplayStyle.Flex : DisplayStyle.None;
            spectate.style.display = profile.Card.Presence == PlayerPresence.InBattle ? DisplayStyle.Flex : DisplayStyle.None;
            stats.Clear();
            string[] values = ProfileStatsFormat.Values(profile.Stats);
            for (int i = 0; i < values.Length; i++)
            {
                var cell = new PaperCard();
                cell.AddToClassList("lb-public-stat");
                cell.Add(PlayerCardViews.Text(StatNames[i]));
                cell.Add(PlayerCardViews.Text(values[i]));
                stats.Add(cell);
            }
            loadout.Clear();
            AddChips(profile.SuitIds, "Suit");
            AddChips(profile.DistrictIds, "District");
            if (loadout.childCount == 0) loadout.Add(PlayerCardViews.Text("No loadout shared."));
            recent.Clear();
            if (profile.Recent != null)
                foreach (var match in profile.Recent)
                {
                    if (match == null) continue;
                    var row = new PaperCard();
                    row.AddToClassList("lb-public-recent");
                    row.Add(PlayerCardViews.Text(PlayerCardViews.Handle(match.Opponent)));
                    row.Add(PlayerCardViews.Text((match.Won ? "Won" : "Lost") + " · " +
                        (match.RRDelta >= 0 ? "+" : "") + match.RRDelta + " RR"));
                    recent.Add(row);
                }
            if (recent.childCount == 0) recent.Add(PlayerCardViews.Text("No recent matches shared."));
        }

        private void AddChips(string[] ids, string kind)
        {
            if (ids == null) return;
            foreach (string id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var chip = new PaperCard { name = "profile-chip-" + id, pickingMode = PickingMode.Ignore };
                chip.Add(PlayerCardViews.Text(kind + ": " + id));
                chip.AddToClassList("lb-public-chip");
                loadout.Add(chip);
            }
        }

        private void BeginRename()
        {
            if (!viewingSelf || renamePending || sheet == null) return;
            renameSession++;
            nameField.SetValueWithoutNotify(BackendServices.LastKnownState?.Profile?.Name ?? PlayerProfile.Instance?.Username ?? "");
            tagField.SetValueWithoutNotify(BackendServices.LastKnownState?.Profile?.Tag ?? "");
            ShowNote(null);
            sheet.Open(renameContent);
            nameField.schedule.Execute(() => { if (sheet.IsShowing(renameContent)) { nameField.Focus(); nameField.SelectAll(); } });
        }

        private async void CommitRename()
        {
            if (renamePending || !IsOpen || !viewingSelf || sheet == null || !sheet.IsShowing(renameContent)) return;
            string candidate = nameField.value;
            string tag = tagField.value;
            if (!System.Text.RegularExpressions.Regex.IsMatch(candidate, @"\A[A-Za-z0-9_-]{3,16}\z"))
            {
                ShowNote("3 to 16 characters: letters, numbers, underscores or hyphens.");
                return;
            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(tag, @"\A[A-Z0-9]{0,4}\z"))
            {
                ShowNote("Tag: 1 to 4 uppercase letters or numbers, or leave empty.");
                return;
            }
            int session = renameSession;
            string account = SelfId;
            renamePending = true;
            nameField.SetEnabled(false); tagField.SetEnabled(false);
            try
            {
                await BackendServices.Handles.SetHandleAsync(candidate, tag, Guid.NewGuid().ToString());
                if (account != SelfId) return;
                Renamed?.Invoke();
                if (session != renameSession || !IsOpen || !viewingSelf) return;
                if (sheet.IsShowing(renameContent)) sheet.Close();
                Reload();
                toast?.Show("Name changed");
            }
            catch (Exception ex)
            {
                if (account == SelfId && session == renameSession && IsOpen && viewingSelf)
                {
                    ShowNote(ex.Message);
                    toast?.Show(ex.Message);
                }
            }
            finally
            {
                renamePending = false;
                nameField.SetEnabled(true); tagField.SetEnabled(true);
            }
        }

        private void OnNameFieldKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
            CommitRename();
            evt.StopPropagation();
        }

        private void ShowNote(string error)
        {
            nameNote.text = string.IsNullOrEmpty(error) ? RenameCostHint() : error;
            nameNote.EnableInClassList("ui-sheet__note--error", !string.IsNullOrEmpty(error));
        }

        private static string RenameCostHint()
        {
            ProfileRecord profile = BackendServices.LastKnownState?.Profile;
            if (profile == null || profile.LastFreeRenameUnixSeconds == 0) return "Free";
            DateTimeOffset nextFree = DateTimeOffset.FromUnixTimeSeconds(profile.LastFreeRenameUnixSeconds).AddDays(30);
            return DateTimeOffset.UtcNow >= nextFree ? "Free" : "50 laurels, free again on " + nextFree.ToLocalTime().ToString("yyyy-MM-dd");
        }
    }
}
