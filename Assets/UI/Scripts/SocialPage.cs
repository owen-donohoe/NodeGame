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

        private sealed class PreviousMatch
        {
            public MatchHistoryEntry Entry;
            public PlayerCard Opponent;
        }

        private readonly Section tv = new Section();
        private readonly Section previous = new Section();
        private readonly Section friends = new Section();
        private ISocialService social;
        private IProfileService profiles;
        private IMatchHistoryService history;
        private Action<string> openProfile;
        private LobbySheet sheet;
        private bool visible;
        private int visit;
        private string account;
        private static long Now => (long)(Time.realtimeSinceStartupAsDouble * 1000);

        public SocialPage(VisualTreeAsset layout) : base(LobbyPageID.Social, Build())
        {
            Root.userData = this;
            Root.Q("social-tv").Add(tv.View);
            Root.Q("social-previous").Add(previous.View);
            Root.Q("social-friends").Add(friends.View);
            tv.View.RetryRequested += LoadTV;
            previous.View.RetryRequested += LoadPrevious;
            friends.View.RetryRequested += LoadFriends;
            Root.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target != Root) return;
                OnHide();
                BackendServices.Account.Changed -= AccountChanged;
            });
        }

        private static VisualElement Build()
        {
            var root = new VisualElement { name = "page-social" };
            Resources.Load<VisualTreeAsset>("Lobby/SocialPage").CloneTree(root);
            return root;
        }

        public void Bind(ISocialService social, IProfileService profiles, IMatchHistoryService history,
            Action<string> openProfile, LobbySheet sheet)
        {
            this.social = social; this.profiles = profiles; this.history = history;
            this.openProfile = openProfile; this.sheet = sheet;
            BackendServices.Account.Changed -= AccountChanged;
            BackendServices.Account.Changed += AccountChanged;
            if (visible) Reload();
        }

        public override void OnShow() { visible = true; if (social != null) Reload(); }

        public override void OnHide()
        {
            visible = false;
            visit++;
            StopLoading(tv); StopLoading(previous); StopLoading(friends);
        }

        private static void StopLoading(Section section)
        {
            section.State = section.State.Reduce(section.State.HasContent ? AsyncResult.Ready : AsyncResult.Empty, Now);
            section.View.Render(section.State, Now);
        }

        private void AccountChanged(AccountInfo info)
        {
            visit++;
            Clear(tv); Clear(previous); Clear(friends);
            account = info?.PlayerId;
            if (visible && social != null) Reload();
        }

        private static void Clear(Section section)
        {
            section.State = new AsyncState();
            section.View.Content.Clear();
            section.View.Render(section.State.Reduce(AsyncResult.Empty, Now), Now);
        }

        private void Reload()
        {
            string current = BackendServices.Account.Current?.PlayerId;
            if (account != current) { Clear(tv); Clear(previous); Clear(friends); }
            account = current;
            visit++;
            LoadTV(); LoadPrevious(); LoadFriends();
        }

        private void LoadTV() => _ = Load(tv, social.GetLiveMatchesAsync, RenderTV,
            "No live matches right now.", "You're offline. Connect to watch live matches.", LobbyIconKind.Tv);

        private void LoadFriends() => _ = Load(friends, social.GetFriendsAsync, RenderFriends,
            "No friends yet. Open a player profile to add a friend.", "You're offline. Connect to refresh your friends.", LobbyIconKind.Smile);

        private void LoadPrevious() => _ = Load(previous, FetchPrevious, RenderPrevious,
            "No previous matches yet. Finished ranked matches appear here.", "You're offline. Connect to refresh previous matches.", LobbyIconKind.Tv);

        private async Task Load<T>(Section section, Func<Task<List<T>>> fetch, Action<List<T>> render,
            string empty, string offline, LobbyIconKind icon)
        {
            if (!visible) return;
            int generation = visit;
            int request = ++section.Request;
            string requestedFor = BackendServices.Account.Current?.PlayerId;
            section.State = section.State.Reduce(AsyncResult.Loading, Now);
            section.View.Render(section.State, Now);
            try
            {
                var items = await fetch();
                if (!Active(generation, section, request, requestedFor)) return;
                section.View.Content.Clear();
                bool hasItems = items != null && items.Count > 0;
                if (hasItems) render(items);
                section.State = section.State.Reduce(hasItems ? AsyncResult.Ready : AsyncResult.Empty, Now);
                section.View.Render(section.State, Now, empty, icon);
            }
            catch (Exception)
            {
                if (!Active(generation, section, request, requestedFor)) return;
                bool disconnected = BackendServices.SimulatedOffline || Application.internetReachability == NetworkReachability.NotReachable;
                section.State = section.State.Reduce(disconnected ? AsyncResult.Offline : AsyncResult.Error, Now);
                section.View.Render(section.State, Now, disconnected ? offline : "Couldn't refresh this section. Try again.");
            }
        }

        private bool Active(int generation, Section section, int request, string requestedFor) => visible &&
            visit == generation && section.Request == request && requestedFor == BackendServices.Account.Current?.PlayerId;

        private async Task<List<PreviousMatch>> FetchPrevious()
        {
            var entries = await history.GetAsync();
            var rows = new List<PreviousMatch>();
            if (entries == null) return rows;
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                PlayerCard opponent;
                try { opponent = (await profiles.GetAsync(entry.opponentPlayerId))?.Card; }
                catch (Exception) { opponent = null; }
                rows.Add(new PreviousMatch { Entry = entry, Opponent = opponent ??
                    new PlayerCard { PlayerId = entry.opponentPlayerId, Name = entry.opponentPlayerId } });
            }
            return rows;
        }

        private void RenderTV(List<LiveMatch> matches)
        {
            foreach (var match in matches)
            {
                if (match == null) continue;
                var row = new VisualElement();
                row.AddToClassList("lb-live-row");
                row.Add(PlayerCardViews.Card(match.A, () => openProfile(match.A?.PlayerId), false));
                var vs = new Button(() => Debug.Log("[Social] watch " + match.MatchId)) { text = "VS" };
                vs.AddToClassList("lb-social-red");
                row.Add(vs);
                row.Add(PlayerCardViews.Card(match.B, () => openProfile(match.B?.PlayerId), false));
                tv.View.Content.Add(row);
            }
        }

        private void RenderFriends(List<PlayerCard> cards)
        {
            // Sort a copy: services own their returned collection.
            var sorted = new List<PlayerCard>(cards);
            sorted.Sort(FriendPresenceOrder.Instance);
            foreach (var card in sorted)
                if (card != null) friends.View.Content.Add(PlayerCardViews.Card(card, () => openProfile(card.PlayerId), true));
        }

        private void RenderPrevious(List<PreviousMatch> matches)
        {
            foreach (var match in matches)
            {
                var row = new VisualElement();
                row.AddToClassList("lb-previous-row");
                row.Add(PlayerCardViews.IconButton(LobbyIconKind.ProfileYouAreHere, "Your loadout", ShowUnrecordedLoadout));
                var info = new VisualElement();
                info.style.flexGrow = 1;
                info.Add(new Button(() => openProfile(match.Opponent.PlayerId))
                    { text = PlayerCardViews.Handle(match.Opponent), enableRichText = false });
                info.Add(PlayerCardViews.Text(match.Entry.won.HasValue ? match.Entry.won.Value ? "Won" : "Lost" : match.Entry.state.ToString()));
                row.Add(info);
                row.Add(new Button(() => Debug.Log("[Social] replay " + match.Entry.matchId)) { text = "Replay" });
                previous.View.Content.Add(row);
            }
        }

        private void ShowUnrecordedLoadout()
        {
            var content = new VisualElement();
            content.Add(PlayerCardViews.Text("Loadout from this match isn't recorded yet"));
            sheet?.Open(content);
        }
    }
}
