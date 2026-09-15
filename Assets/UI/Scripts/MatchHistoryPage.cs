using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Match history: a push page opened from the TV button beside the gear.
    ///
    /// Nothing records a finished match yet, so this always shows its empty
    /// state. When a system exists, OnOpen fills #history-list and hides
    /// #history-empty; the layout already has both.
    /// </summary>
    public class MatchHistoryPage : LobbyPushPage
    {
        private readonly VisualElement list;
        private readonly VisualElement empty;

        public MatchHistoryPage(VisualTreeAsset layout)
            : base("match-history-page", layout, "Match history layout missing - assign MatchHistoryPage.uxml")
        {
            list = Root.Q<VisualElement>("history-list");
            empty = Root.Q<VisualElement>("history-empty");
        }

        protected override void OnOpen()
        {
            // TODO(history): no match result is written anywhere yet.
            const bool hasMatches = false;

            if (list != null) list.EnableInClassList("lb-view--hidden", !hasMatches);
            if (empty != null) empty.EnableInClassList("lb-view--hidden", hasMatches);
        }
    }
}
