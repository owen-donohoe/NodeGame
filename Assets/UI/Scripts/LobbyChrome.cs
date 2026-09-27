using NodeWar.Backend;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The persistent top of the lobby: currency purses, match history, the
    /// gear, and the trophy strip. It sits outside the page host, so it is bound once here
    /// rather than once per page.
    ///
    /// Only the player name is a link (to Profile); the rest of the strip is
    /// information. Values the game does not track yet are shown as an em-dash
    /// rather than a made-up number.
    /// </summary>
    public class LobbyChrome
    {
        private const string Dash = "—";

        private readonly Label nameText;
        private readonly Label trophyCount;
        private readonly Label trophyMax;
        private readonly VisualElement trophyFill;
        private readonly Label arenaText;

        /// <summary>Raised when the player name is pressed.</summary>
        public event System.Action ProfileRequested;

        /// <summary>Raised when the gear is pressed.</summary>
        public event System.Action SettingsRequested;

        /// <summary>Raised when the TV button beside the gear is pressed.</summary>
        public event System.Action HistoryRequested;

        public LobbyChrome(VisualElement root, LobbyToast toast)
        {
            nameText = root.Q<Label>("name-text");
            trophyCount = root.Q<Label>("trophy-count");
            trophyMax = root.Q<Label>("trophy-max");
            trophyFill = root.Q<VisualElement>("trophy-fill");
            arenaText = root.Q<Label>("arena-text");

            Bind(root, "name-button", () => { if (ProfileRequested != null) ProfileRequested(); });

            // TODO(economy): coins and gold leaf do not exist in PlayerProfile yet.
            Bind(root, "purse-coin", () => toast.Show("Coins arrive in a later update"));
            Bind(root, "purse-leaf", () => toast.Show("Gold leaf arrives in a later update"));

            Bind(root, "gear", () => { if (SettingsRequested != null) SettingsRequested(); });
            Bind(root, "history", () => { if (HistoryRequested != null) HistoryRequested(); });

            root.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                if (evt.target == root) ObserveState();
            });
            root.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target == root) BackendServices.StateChanged -= Refresh;
            });
            if (root.panel != null) ObserveState();
        }

        private void ObserveState()
        {
            BackendServices.StateChanged -= Refresh;
            BackendServices.StateChanged += Refresh;
            Refresh();
        }

        private static void Bind(VisualElement root, string name, System.Action action)
        {
            Button button = root.Q<Button>(name);
            if (button != null) button.clicked += action;
        }

        public void Refresh()
        {
            PlayerProfile profile = PlayerProfile.Instance;

            if (nameText != null)
                nameText.text = profile != null ? profile.Username : "player";

            RankRecord rank = BackendServices.LastKnownState?.Rank;
            RankDisplay display = new RankDisplay(rank?.RR ?? 0);
            if (trophyCount != null) trophyCount.text = rank != null ? display.RR.ToString() : Dash;
            if (trophyMax != null) trophyMax.text = rank != null ? " RR" : "";
            if (trophyFill != null)
                trophyFill.style.width = Length.Percent(rank != null ? display.Fill * 100f : 0f);
            if (arenaText != null)
                arenaText.text = rank == null ? "Arena " + Dash : display.Name + (display.Span.HasValue
                    ? " · " + (display.Span.Value - display.RRIntoArena) + " to " + RankTable.Names[display.Arena + 1]
                    : " · Top arena");
        }
    }
}
