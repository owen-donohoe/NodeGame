using System;
using System.Threading.Tasks;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>Self expands from chrome; other players use the shared sheet surface.</summary>
    public class ProfilePage
    {
        private readonly IProfileService profiles;
        private readonly IGuildService guild = BackendServices.Guild;
        private readonly MkPanel confirmation = new MkPanel();
        private readonly MkPanel renamePanel = new MkPanel();
        private readonly TextField nameField = new TextField("Name");
        private readonly TextField tagField = new TextField("Tag");
        private readonly Label nameNote = new Label { enableRichText = false };
        private bool renamePending;
        private string renameAccount;
        private GuildReportScreen report;
        private GuildView ownGuild;
        private PlayerCard shownCard;
        private VisualElement footer;
        private readonly AsyncStateView moderation = new AsyncStateView();
        public event Action<string> InviteRequested;
        private readonly MkSheet panel = new MkSheet();
        private readonly MkCard own = new MkCard { name = "own-profile" };
        private readonly AsyncStateView stateView = new AsyncStateView();
        private readonly SafeAreaBinder safeArea;
        private AsyncState state = new AsyncState();
        private readonly LobbyElementMotion ownMotion;
        private IVisualElementScheduledItem hideLater;
        private IVisualElementScheduledItem revealLater;
        private bool awaitingReveal;
        private bool viewingSelf;
        private string playerId, account;
        private int generation;
        private int pointer = -1;
        private Vector2 down;
        private static long Now => (long)(Time.realtimeSinceStartupAsDouble * 1000);
        private static string SelfId => BackendServices.Account.Current?.PlayerId ?? "self";
        public VisualElement Root { get; } = new VisualElement { name = "public-profile" };
        public bool IsOpen { get; private set; }
        public bool ViewingSelf => viewingSelf;
        public event Action Opened;
        public event Action Closed;
        public event Action Renamed;

        public ProfilePage(VisualTreeAsset layout, LobbySheet sheet, LobbyToast toast, IProfileService profiles = null)
        {
            this.profiles = profiles ?? BackendServices.Profiles;
            Root.userData = this; Root.AddToClassList("g2-profile"); Root.AddToClassList("lb-no-swipe");
            var styles = Resources.Load<StyleSheet>("Lobby/ProfileScreen");
            if (styles != null) Root.styleSheets.Add(styles);
            Root.Add(own); Root.Add(panel);
            panel.SetScrollable(false);
            own.AddToClassList("g2-profile-own"); own.Add(stateView);
            ownMotion = new LobbyElementMotion(own);
            ownMotion.Set(0f, 0f, LobbyMotion.ClosedScale);
            safeArea = new SafeAreaBinder(own);
            // Switching to self closes the old sheet without closing the new profile.
            panel.Closed += () => { if (!viewingSelf) Close(); };
            stateView.Content.AddToClassList("g2-profile-content");
            stateView.AddToClassList("g2-profile-state"); stateView.RetryRequested += Reload;
            Root.style.display = DisplayStyle.None;
            own.RegisterCallback<PointerDownEvent>(evt =>
            {
                var header = stateView.Content.Q("public-profile-header");
                bool onHeader = header != null ? header.worldBound.Contains(evt.position) : evt.position.y < own.worldBound.yMin + 100;
                if (evt.button == 0 && onHeader) { pointer = evt.pointerId; down = evt.position; }
            }, TrickleDown.TrickleDown);
            own.RegisterCallback<PointerUpEvent>(evt =>
            {
                Vector2 delta = (Vector2)evt.position - down;
                if (evt.pointerId == pointer && delta.y > 60 && delta.y > Mathf.Abs(delta.x)) Close();
                pointer = -1;
            }, TrickleDown.TrickleDown);
            own.RegisterCallback<PointerCancelEvent>(_ => pointer = -1, TrickleDown.TrickleDown);
            Root.RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == Root) Close(); });
        }
        public void BindPanels(LobbyOverlayRouter router, VisualElement host)
        {
            confirmation.BindRouter(router, "player-kick"); host.Add(confirmation);
            confirmation.Opened += () => confirmation.BringToFront();
            BuildRenamePanel(router, host);
            report = new GuildReportScreen(router, host, "player-report");
            moderation.RetryRequested += () => { if (IsOpen && !viewingSelf) _ = LoadModerationAsync(generation, playerId, SelfId); };
        }
        public void Update() { if (IsOpen && viewingSelf) safeArea.Update(); }
        public void Open(VisualElement source)
        {
            if (IsOpen) return;
            OpenFor(SelfId);
        }
        public void OpenFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (IsOpen && playerId == id && account == SelfId) return;
            hideLater?.Pause();
            revealLater?.Pause();
            bool hidden = Root.style.display == DisplayStyle.None;
            bool changed = playerId != id || account != SelfId;
            playerId = id; account = SelfId; viewingSelf = id == SelfId; ownGuild = null;
            if (changed) { shownCard = null; footer = null; } else if (!viewingSelf) RenderFooter();
            if (changed) { state = new AsyncState(); stateView.Content.Clear(); }
            Root.EnableInClassList("g2-profile--self", viewingSelf);
            Root.EnableInClassList("g2-profile--player", !viewingSelf);
            own.style.display = viewingSelf ? DisplayStyle.Flex : DisplayStyle.None;
            if (viewingSelf)
            {
                panel.Close();
                if (stateView.parent != own) own.Add(stateView);
            }
            else
            {
                // Cancel a self-close completion that could otherwise hide this new sheet.
                ownMotion.Hold();
                if (stateView.parent != panel.Body) panel.Body.Add(stateView);
            }
            BackendServices.StateChanged -= StateChanged; BackendServices.StateChanged += StateChanged;
            BackendServices.Account.Changed -= AccountChanged; BackendServices.Account.Changed += AccountChanged;
            bool opening = !IsOpen; IsOpen = true; Root.style.display = DisplayStyle.Flex;
            if (opening || changed)
            {
                awaitingReveal = true;
                if (viewingSelf)
                {
                    if (hidden) ownMotion.Set(0f, 0f, LobbyMotion.ClosedScale);
                    else
                    {
                        ownMotion.Hold();
                        if (state.HasContent && !changed)
                        { awaitingReveal = false; ownMotion.Open(); }
                    }
                }
            }
            if (opening) Opened?.Invoke();
            Reload();
        }
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false; generation++; pointer = -1; confirmation.Close(); renamePanel.Close();
            awaitingReveal = false; revealLater?.Pause();
            BackendServices.StateChanged -= StateChanged; BackendServices.Account.Changed -= AccountChanged;
            if (viewingSelf)
            {
                ownMotion.Close(() => Root.style.display = DisplayStyle.None);
            }
            else
            {
                panel.Close();
                hideLater = Root.schedule.Execute(() => Root.style.display = DisplayStyle.None);
                hideLater.ExecuteLater((long)(LobbyMotion.CloseSeconds * 1000));
            }
            Closed?.Invoke();
        }
        private void RevealPreparedLayout()
        {
            if (!IsOpen || !awaitingReveal) return;
            if (!viewingSelf) { awaitingReveal = false; panel.Open(); return; }
            // Build data and let Toolkit lay out the complete hierarchy at zero
            // opacity before starting the single surface tween. No display toggle
            // or independent content fade can interrupt the expansion.
            revealLater?.Pause();
            revealLater = Root.schedule.Execute(() =>
            {
                if (!IsOpen || !awaitingReveal) return;
                if (stateView.layout.width <= 0f || stateView.layout.height <= 0f || !ProfileViews.PrepareLadder(stateView.Content))
                { RevealPreparedLayout(); return; }
                awaitingReveal = false; ownMotion.Open();
            });
            revealLater.ExecuteLater(16);
        }
        private void StateChanged() { if (IsOpen && viewingSelf) Reload(); }
        private void AccountChanged(AccountInfo _) { report?.Close(); renameAccount = null; Close(); }
        private void Reload() { if (IsOpen) _ = LoadAsync(++generation, playerId, SelfId); }
        private async Task LoadAsync(int request, string id, string requestedFor)
        {
            state = state.Reduce(AsyncResult.Loading, Now); stateView.Render(state, Now);
            try
            {
                PublicProfileView profile = await profiles.GetAsync(id);
                if (!Active(request, id, requestedFor)) return;
                stateView.Content.Clear();
                if (profile?.Card == null) { shownCard = null; footer = null; state = state.Reduce(AsyncResult.Empty, Now); stateView.Render(state, Now, "This profile is unavailable."); RevealPreparedLayout(); return; }
                shownCard = profile.Card; Render(profile);
                if (!viewingSelf) _ = LoadModerationAsync(request, id, requestedFor);
                state = state.Reduce(AsyncResult.Ready, Now); stateView.Render(state, Now);
                RevealPreparedLayout();
            }
            catch (Exception)
            {
                if (!Active(request, id, requestedFor)) return;
                state = state.Reduce(AsyncResult.Offline, Now); stateView.Render(state, Now, "Couldn't load this profile. Try again.");
                RevealPreparedLayout();
            }
        }
        private bool Active(int request, string id, string requestedFor) => IsOpen && generation == request && playerId == id && SelfId == requestedFor;
        private async Task LoadModerationAsync(int request, string id, string requestedFor)
        {
            moderation.Render(new AsyncState(Now), Now);
            try
            {
                var result = await guild.GetMineAsync();
                if (!Active(request, id, requestedFor)) return;
                ownGuild = result; RenderFooter();
                moderation.Render(new AsyncState().Reduce(AsyncResult.Ready, Now), Now);
            }
            catch (Exception)
            {
                if (!Active(request, id, requestedFor)) return;
                ownGuild = null; RenderFooter();
                moderation.Render(new AsyncState().Reduce(AsyncResult.Offline, Now), Now, "Couldn't check guild permissions.");
            }
        }
        private void RenderFooter()
        {
            if (footer == null || shownCard == null) return;
            footer.Clear();
            var target = shownCard;
            footer.Add(GuildViews.Button("Block", () => Debug.Log("[Stub] Block player " + target.PlayerId)));
            footer.Add(GuildViews.Button("Report", () => report?.Open(target, PlayerPanelPolicy.ModerationTarget(ownGuild, target.PlayerId, SelfId) != null), "g2-urgent"));
            if (PlayerPanelPolicy.ModerationTarget(ownGuild, target.PlayerId, SelfId) != null)
            {
                var kick = GuildViews.Button("Kick", () => ConfirmKick(target)); kick.AddToClassList("g2-urgent"); footer.Add(kick);
            }
        }
        private void ConfirmKick(PlayerCard target)
        {
            if (!IsOpen || PlayerPanelPolicy.ModerationTarget(ownGuild, target.PlayerId, SelfId) == null) return;
            confirmation.Body.Clear();
            confirmation.Title.text = "Remove from guild?";
            confirmation.Body.Add(GuildViews.Text("Remove " + PlayerCardViews.Handle(target) + " from " + ownGuild.Summary.Name + "?", "mk-ellipsis"));
            confirmation.Body.Add(GuildViews.Button("Cancel", confirmation.Close));
            confirmation.Body.Add(GuildViews.Button("Kick", () =>
            {
                if (IsOpen && PlayerPanelPolicy.ModerationTarget(ownGuild, target.PlayerId, SelfId) != null) Debug.Log("[Stub] Guild kick " + target.PlayerId);
                confirmation.Close();
            }, "mk-btn--urgent"));
            confirmation.Open();
        }
        private void Render(PublicProfileView profile)
        {
            var header = new VisualElement { name = "public-profile-header" }; header.AddToClassList("g2-profile-header");
            var identity = new VisualElement(); identity.style.flexGrow = 1; identity.style.flexShrink = 1; identity.style.minWidth = 0;
            var handle = GuildViews.Text(PlayerCardViews.HeaderHandle(profile.Card), "mk-h2"); handle.AddToClassList("mk-ellipsis"); TrackedText.Track(handle); identity.Add(handle);
            var rank = new RankDisplay(profile.Card.RR);
            var rankRow = new VisualElement(); rankRow.AddToClassList("mk-row");
            var era = GuildViews.Text(rank.Name, "mk-caption"); TrackedText.Track(era); era.AddToClassList("mk-chip"); era.AddToClassList("g2-era-badge"); era.AddToClassList("g2-era--" + (Math.Min(4, rank.Arena) + 1)); rankRow.Add(era);
            rankRow.Add(GuildViews.Text(rank.RR + " RR", "mk-trophy")); identity.Add(rankRow); header.Add(identity);
            if (viewingSelf)
            {
                var rename = GuildViews.Button("Rename", () => BeginRename(profile.Card)); rename.name = "profile-rename";
                header.Add(rename); header.Add(GuildViews.Button("Back", Close));
            }
            else
            {
                var actions = new VisualElement(); actions.AddToClassList("g2-player-actions");
                var add = GuildViews.Button("+", () => Debug.Log("[Stub] Add friend " + profile.Card.PlayerId)); add.name = "public-profile-add"; add.tooltip = "Add friend";
                actions.Add(add);
                var invite = GuildViews.Button("Invite", () => InviteRequested?.Invoke(profile.Card.PlayerId)); invite.name = "public-profile-invite"; actions.Add(invite);
                header.Add(actions);
            }
            if (viewingSelf) header.RegisterCallback<ClickEvent>(_ => Close());
            stateView.Content.Add(header);
            if (!viewingSelf)
            {
                var list = new ScrollView(); list.AddToClassList("g2-player-list"); LobbyScrollbars.Hide(list);
                list.Add(ProfileViews.StatsGrid(profile.Stats)); list.Add(ProfileViews.Loadout(profile));
                list.Add(ProfileViews.History(profile.Recent)); stateView.Content.Add(list);
                footer = new VisualElement(); footer.AddToClassList("g2-player-footer"); stateView.Content.Add(footer);
                RenderFooter(); stateView.Content.Add(moderation); return;
            }
            var columns = new VisualElement(); columns.AddToClassList("g2-profile-columns"); stateView.Content.Add(columns);
            var left = new ScrollView(); left.AddToClassList("g2-profile-left"); LobbyScrollbars.Hide(left);
            left.Add(ProfileViews.StatsGrid(profile.Stats)); left.Add(ProfileViews.Loadout(profile)); columns.Add(left);
            if (viewingSelf) columns.Add(ProfileViews.Ladder(profile.Card.RR));
            else left.Add(ProfileViews.History(profile.Recent));
        }

        private void BuildRenamePanel(LobbyOverlayRouter router, VisualElement host)
        {
            renamePanel.BindRouter(router, "profile-rename"); renamePanel.Title.text = "Rename";
            foreach (var field in new[] { nameField, tagField })
            {
                field.maxLength = 16; field.AddToClassList("mk-field");
                field.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                    CommitRename(); evt.StopPropagation();
                }, TrickleDown.TrickleDown);
                renamePanel.Body.Add(field);
            }
            tagField.maxLength = 4; nameNote.AddToClassList("mk-caption");
            renamePanel.Body.Add(nameNote);
            renamePanel.Body.Add(GuildViews.Button("Cancel", renamePanel.Close));
            renamePanel.Body.Add(GuildViews.Button("Save", CommitRename, "mk-btn--primary"));
            renamePanel.Opened += () => renamePanel.BringToFront();
            host.Add(renamePanel);
        }

        private void BeginRename(PlayerCard card)
        {
            if (!IsOpen || !viewingSelf || renamePending) return;
            renameAccount = SelfId;
            ProfileRecord record = BackendServices.LastKnownState?.Profile;
            nameField.SetValueWithoutNotify(record?.Name ?? card.Name ?? "");
            tagField.SetValueWithoutNotify(record?.Tag ?? "");
            ShowNote(null);
            renamePanel.Open();
            nameField.schedule.Execute(() =>
            {
                if (!IsOpen || !viewingSelf || !renamePanel.IsOpen || nameField.panel == null) return;
                nameField.Focus(); nameField.SelectAll();
            });
        }

        private async void CommitRename()
        {
            if (renamePending || !renamePanel.IsOpen) return;
            string requestedFor = renameAccount;
            string candidate = nameField.value, tag = tagField.value;
            if (!System.Text.RegularExpressions.Regex.IsMatch(candidate, @"\A[A-Za-z0-9_-]{3,16}\z"))
            { ShowNote("3 to 16 characters: letters, numbers, underscores or hyphens.", true); return; }
            if (!System.Text.RegularExpressions.Regex.IsMatch(tag, @"\A[A-Z0-9]{0,4}\z"))
            { ShowNote("Tag: 1 to 4 uppercase letters or numbers, or leave empty.", true); return; }
            renamePending = true; nameField.SetEnabled(false); tagField.SetEnabled(false);
            try
            {
                await BackendServices.Handles.SetHandleAsync(candidate, tag, Guid.NewGuid().ToString());
                if (requestedFor != SelfId) return;
                renamePanel.Close(); Renamed?.Invoke();
                if (IsOpen && viewingSelf) Reload();
            }
            catch (Exception ex)
            {
                if (requestedFor == SelfId) ShowNote(ex.Message, true);
            }
            finally { renamePending = false; nameField.SetEnabled(true); tagField.SetEnabled(true); }
        }

        private void ShowNote(string error, bool isError = false)
        {
            nameNote.text = isError ? error : RenameCostHint();
            nameNote.style.color = isError ? new Color(0.78f, 0.2f, 0.16f) : StyleKeyword.Null;
        }

        private static string RenameCostHint()
        {
            ProfileRecord profile = BackendServices.LastKnownState?.Profile;
            if (profile == null || profile.LastFreeRenameUnixSeconds == 0) return "Free";
            DateTimeOffset nextFree = DateTimeOffset.FromUnixTimeSeconds(profile.LastFreeRenameUnixSeconds).AddDays(30);
            return DateTimeOffset.UtcNow >= nextFree ? "Free" :
                "50 laurels, free again on " + nextFree.ToLocalTime().ToString("yyyy-MM-dd");
        }
    }
}
