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
        private readonly MkSheet panel = new MkSheet();
        private readonly MkCard own = new MkCard { name = "own-profile" };
        private readonly AsyncStateView stateView = new AsyncStateView();
        private readonly SafeAreaBinder safeArea;
        private AsyncState state = new AsyncState();
        private VisualElement sourceRect;
        private IVisualElementScheduledItem hideLater;
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
            Root.styleSheets.Add(Resources.Load<StyleSheet>("Lobby/ProfileScreen")); Root.Add(own); Root.Add(panel);
            own.AddToClassList("g2-profile-own"); own.Add(stateView);
            safeArea = new SafeAreaBinder(own); panel.Closed += Close;
            stateView.Content.AddToClassList("g2-profile-content");
            stateView.AddToClassList("g2-profile-state"); stateView.RetryRequested += Reload;
            Root.style.display = DisplayStyle.None;
            own.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 0) { pointer = evt.pointerId; down = evt.position; } });
            own.RegisterCallback<PointerUpEvent>(evt =>
            {
                Vector2 delta = (Vector2)evt.position - down;
                if (evt.pointerId == pointer && delta.y > 60 && delta.y > Mathf.Abs(delta.x)) Close();
                pointer = -1;
            });
            own.RegisterCallback<PointerCancelEvent>(_ => pointer = -1);
            Root.RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == Root) Close(); });
        }
        public void Update() { if (IsOpen && viewingSelf) safeArea.Update(); }
        public void Open(VisualElement source)
        {
            if (IsOpen) return;
            sourceRect = source;
            OpenFor(SelfId);
            Root.AddToClassList("g2-profile--instant"); SnapTo(source);
            Root.schedule.Execute(() =>
            {
                if (!IsOpen) return;
                Root.RemoveFromClassList("g2-profile--instant"); Root.AddToClassList("g2-profile--expanded");
                Root.style.translate = new Translate(0, 0); Root.style.scale = new Scale(Vector2.one);
            }).ExecuteLater(16);
        }
        public void OpenFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            hideLater?.Pause();
            bool changed = playerId != id || account != SelfId;
            playerId = id; account = SelfId; viewingSelf = id == SelfId;
            if (changed) { state = new AsyncState(); stateView.Content.Clear(); }
            Root.EnableInClassList("g2-profile--self", viewingSelf);
            Root.AddToClassList("g2-profile--expanded");
            own.style.display = viewingSelf ? DisplayStyle.Flex : DisplayStyle.None;
            if (viewingSelf) { panel.Close(); own.Add(stateView); }
            else { sourceRect = null; panel.Body.Add(stateView); panel.Open(true); }
            Root.style.translate = new Translate(0, 0); Root.style.scale = new Scale(Vector2.one);
            BackendServices.StateChanged -= StateChanged; BackendServices.StateChanged += StateChanged;
            BackendServices.Account.Changed -= AccountChanged; BackendServices.Account.Changed += AccountChanged;
            bool opening = !IsOpen; IsOpen = true; Root.style.display = DisplayStyle.Flex;
            if (opening) Opened?.Invoke();
            Reload();
        }
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false; generation++; pointer = -1;
            BackendServices.StateChanged -= StateChanged; BackendServices.Account.Changed -= AccountChanged;
            Root.RemoveFromClassList("g2-profile--expanded");
            if (viewingSelf && sourceRect != null)
            {
                SnapTo(sourceRect);
                hideLater = Root.schedule.Execute(() => Root.style.display = DisplayStyle.None); hideLater.ExecuteLater(340);
            }
            else { panel.Close(); Root.style.display = DisplayStyle.None; }
            Closed?.Invoke();
        }
        private void SnapTo(VisualElement source)
        {
            if (source == null || Root.parent == null) return;
            Rect full = Root.parent.worldBound, from = source.worldBound;
            if (full.width <= 0 || full.height <= 0) return;
            Root.style.translate = new Translate(from.x - full.x, from.y - full.y);
            Root.style.scale = new Scale(new Vector2(from.width / full.width, from.height / full.height));
        }
        private void StateChanged() { if (IsOpen && viewingSelf) Reload(); }
        private void AccountChanged(AccountInfo _) { Close(); }
        private void Reload() { if (IsOpen) _ = LoadAsync(++generation, playerId, SelfId); }
        private async Task LoadAsync(int request, string id, string requestedFor)
        {
            state = state.Reduce(AsyncResult.Loading, Now); stateView.Render(state, Now);
            try
            {
                PublicProfileView profile = await profiles.GetAsync(id);
                if (!Active(request, id, requestedFor)) return;
                stateView.Content.Clear();
                if (profile?.Card == null) { state = state.Reduce(AsyncResult.Empty, Now); stateView.Render(state, Now, "This profile is unavailable."); return; }
                Render(profile);
                state = state.Reduce(AsyncResult.Ready, Now); stateView.Render(state, Now);
            }
            catch (Exception)
            {
                if (!Active(request, id, requestedFor)) return;
                state = state.Reduce(AsyncResult.Offline, Now); stateView.Render(state, Now, "Couldn't load this profile. Try again.");
            }
        }
        private bool Active(int request, string id, string requestedFor) => IsOpen && generation == request && playerId == id && SelfId == requestedFor;
        private void Render(PublicProfileView profile)
        {
            var header = new VisualElement { name = "public-profile-header" }; header.AddToClassList("g2-profile-header");
            var identity = new VisualElement(); identity.style.flexGrow = 1; identity.style.flexShrink = 1;
            identity.Add(GuildViews.Text(PlayerCardViews.Handle(profile.Card), "mk-h2"));
            var rank = new RankDisplay(profile.Card.RR);
            var rankRow = new VisualElement(); rankRow.AddToClassList("mk-row");
            var era = GuildViews.Text(rank.Name, "mk-chip"); era.AddToClassList("g2-era-badge"); rankRow.Add(era);
            rankRow.Add(GuildViews.Text(rank.RR + " RR", "mk-trophy")); identity.Add(rankRow); header.Add(identity);
            header.Add(GuildViews.Button("Back", Close));
            if (viewingSelf) header.RegisterCallback<ClickEvent>(_ => Close());
            stateView.Content.Add(header);
            var columns = new VisualElement(); columns.AddToClassList("g2-profile-columns"); stateView.Content.Add(columns);
            var left = new ScrollView(); left.AddToClassList("g2-profile-left"); LobbyScrollbars.Hide(left);
            left.Add(ProfileViews.StatsGrid(profile.Stats)); left.Add(ProfileViews.Loadout(profile)); columns.Add(left);
            if (viewingSelf) columns.Add(ProfileViews.Ladder(profile.Card.RR));
            else left.Add(ProfileViews.History(profile.Recent));
        }
    }
}
