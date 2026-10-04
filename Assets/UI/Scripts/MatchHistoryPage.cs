using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodeWar.Backend;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Match history: a push page opened from the TV button beside the gear.
    /// Lists the caller's ranked matches from GetMatchHistory, newest first.
    /// Private lobby matches are never ranked, so they never appear here.
    /// </summary>
    public class MatchHistoryPage : LobbyPushPage
    {
        private const string LoadingBody = "Loading match history...";
        private const string EmptyBody = "Finished matches will be listed here, with the result and the loadout you took.";
        private const string ErrorBody = "Couldn't load match history.";

        private readonly VisualElement list;
        private readonly VisualElement empty;
        private readonly Label emptyBody;

        // Bumped on every OnOpen; a fetch whose page has since closed or
        // reopened checks this before touching the view.
        private int openGeneration;

        public MatchHistoryPage(VisualTreeAsset layout)
            : base("match-history-page", layout, "Match history layout missing - assign MatchHistoryPage.uxml")
        {
            list = Root.Q<VisualElement>("history-list");
            empty = Root.Q<VisualElement>("history-empty");
            emptyBody = empty != null ? empty.Q<Label>(className: "lb-stub__body") : null;
        }

        protected override void OnOpen()
        {
            int visit = ++openGeneration;
            RenderLoading();
            _ = LoadAsync(visit);
        }

        private bool IsActive(int visit) => IsOpen && openGeneration == visit;

        private async Task LoadAsync(int visit)
        {
            try
            {
                List<MatchHistoryEntry> entries = await BackendServices.MatchHistory.GetAsync();
                if (!IsActive(visit)) return;
                RenderEntries(entries);
            }
            catch (Exception)
            {
                if (IsActive(visit)) RenderError();
            }
        }

        private void RenderLoading()
        {
            if (list != null)
            {
                list.Clear();
                list.EnableInClassList("lb-view--hidden", true);
            }
            ShowEmpty(LoadingBody);
        }

        private void RenderError()
        {
            if (list != null) list.EnableInClassList("lb-view--hidden", true);
            ShowEmpty(ErrorBody);
        }

        private void RenderEntries(List<MatchHistoryEntry> entries)
        {
            bool hasMatches = entries != null && entries.Count > 0;

            if (list != null)
            {
                list.Clear();
                list.EnableInClassList("lb-view--hidden", !hasMatches);
                if (hasMatches)
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    for (int i = 0; i < entries.Count; i++)
                        list.Add(BuildRow(new MatchHistoryRow(entries[i], now), i == entries.Count - 1));
                }
            }

            if (hasMatches)
            {
                if (empty != null) empty.EnableInClassList("lb-view--hidden", true);
            }
            else
            {
                ShowEmpty(EmptyBody);
            }
        }

        private void ShowEmpty(string body)
        {
            if (empty != null) empty.EnableInClassList("lb-view--hidden", false);
            if (emptyBody != null) emptyBody.text = body;
        }

        private static VisualElement BuildRow(MatchHistoryRow row, bool isLast)
        {
            VisualElement element = new VisualElement();
            element.AddToClassList("lb-history__row");
            if (isLast) element.AddToClassList("lb-history__row--last");

            VisualElement info = new VisualElement();
            info.AddToClassList("lb-history__info");

            Label title = new Label(row.Title);
            title.AddToClassList("lb-history__title");
            title.AddToClassList("ui-w600");
            info.Add(title);

            if (!string.IsNullOrEmpty(row.Detail))
            {
                Label detail = new Label(row.Detail);
                detail.AddToClassList("lb-history__detail");
                info.Add(detail);
            }

            Label time = new Label(row.RelativeTime);
            time.AddToClassList("lb-history__time");
            info.Add(time);

            element.Add(info);

            Label replay = new Label(row.ReplayTag);
            replay.AddToClassList("lb-history__replay");
            element.Add(replay);

            return element;
        }
    }
}
