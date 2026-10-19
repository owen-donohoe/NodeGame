using NodeWar.Backend;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Persistent currency purses and player header, outside the page track.
    /// </summary>
    public class LobbyChrome
    {
        private const string Dash = "—";
        private static bool firstHandleAttempted;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHandleAttempt() => firstHandleAttempted = false;

        private readonly Label nameText;
        private readonly Label trophyCount;
        private readonly Label trophyMax;
        private readonly VisualElement trophyFill;
        private readonly Label arenaText;
        private readonly Label coinValue;
        private readonly Label premiumValue;

        /// <summary>Raised when the player name is pressed.</summary>
        public event System.Action ProfileRequested;

        /// <summary>Raised when the burger is pressed; name retained for shell callers.</summary>
        public event System.Action SettingsRequested;

        /// <summary>Raised when the bell is pressed; name retained for shell callers.</summary>
        public event System.Action HistoryRequested;
        public event System.Action<string> CurrencyRequested;

        public LobbyChrome(VisualElement root, LobbyToast toast, BadgeBoard badges = null)
        {
            nameText = root.Q<Label>("name-text");
            if (nameText != null) nameText.enableRichText = false;
            trophyCount = root.Q<Label>("trophy-count");
            trophyMax = root.Q<Label>("trophy-max");
            trophyFill = root.Q<VisualElement>("trophy-fill");
            arenaText = root.Q<Label>("arena-text");
            coinValue = root.Q<Label>("purse-coin-value");
            premiumValue = root.Q<Label>("purse-leaf-value");

            Bind(root, "name-button", () => { if (ProfileRequested != null) ProfileRequested(); });

            Bind(root, "purse-coin", () => CurrencyRequested?.Invoke("nodewar://shop/coins"));
            Bind(root, "purse-leaf", () => CurrencyRequested?.Invoke("nodewar://shop/laurels"));
            root.Q<Button>("history")?.Add(new LobbyBadgeDot(badges, "bell"));
            root.Q<Button>("gear")?.Add(new LobbyBadgeDot(badges, "bell"));

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
                nameText.text = HandleDisplay.Display(BackendServices.LastKnownState?.Profile,
                    profile != null ? profile.Username : "player");

            if (!firstHandleAttempted && BackendServices.LastKnownState != null &&
                BackendServices.LastKnownState.Profile == null)
            {
                firstHandleAttempted = true;
                CreateFirstHandle(profile != null ? profile.Username : "");
            }

            // The server's balances only; the purse never shows a guessed amount.
            WalletRecord wallet = BackendServices.LastKnownState?.Wallet;
            if (coinValue != null) coinValue.text = wallet != null ? FormatAmount(wallet.Coins) : Dash;
            if (premiumValue != null) premiumValue.text = wallet != null ? FormatAmount(wallet.Premium) : Dash;

            RankRecord rank = BackendServices.LastKnownState?.Rank;
            RankDisplay display = new RankDisplay(rank?.RR ?? 0);
            if (trophyCount != null) trophyCount.text = rank == null ? Dash :
                (display.Span.HasValue ? display.RRIntoArena : display.RR).ToString();
            if (trophyMax != null) trophyMax.text = rank == null ? "" : display.Span.HasValue ? " / " + display.Span.Value + " RR" : " RR";
            if (trophyFill != null)
                trophyFill.style.width = Length.Percent(rank != null ? display.Fill * 100f : 0f);
            if (arenaText != null)
                arenaText.text = rank == null ? "Era " + Dash : display.Name;
        }

        private static async void CreateFirstHandle(string localName)
        {
            try
            {
                await BackendServices.Handles.SetHandleAsync(HandleDisplay.FirstRunName(localName), "",
                    System.Guid.NewGuid().ToString());
            }
            catch (System.Exception)
            {
                try
                {
                    await BackendServices.Handles.SetHandleAsync("Player", "", System.Guid.NewGuid().ToString());
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogWarning("Could not create the first handle: " + ex.Message);
                }
            }
        }

        private static string FormatAmount(long amount) =>
            amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
