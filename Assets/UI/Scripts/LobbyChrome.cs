using UnityEngine;
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
        // Same window ProfilePage uses, so the two bars agree.
        private const int TrophyWindow = 100;

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
        }

        private static void Bind(VisualElement root, string name, System.Action action)
        {
            Button button = root.Q<Button>(name);
            if (button != null) button.clicked += action;
        }

        public void Refresh()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            int trophies = profile != null ? profile.Trophies : 0;

            if (nameText != null)
                nameText.text = profile != null ? profile.Username : "player";

            TrophyBarLogic bar = new TrophyBarLogic(trophies, TrophyWindow);

            if (trophyCount != null) trophyCount.text = trophies.ToString();
            if (trophyMax != null) trophyMax.text = " /" + bar.RangeMax;

            if (trophyFill != null)
                trophyFill.style.width = Length.Percent(Mathf.Clamp01(bar.GetFill(trophies)) * 100f);

            // TODO(arenas): the prototype shows "Ancient · 350 to Bronze Age".
            // No arena tiers exist in the game model yet.
            if (arenaText != null) arenaText.text = "Arena " + Dash;
        }
    }
}
