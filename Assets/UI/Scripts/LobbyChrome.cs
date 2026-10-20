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
            if (nameText != null)
            {
                nameText.enableRichText = false;
                TrackedText.Track(nameText);
            }
            trophyCount = root.Q<Label>("trophy-count");
            trophyMax = root.Q<Label>("trophy-max");
            trophyFill = root.Q<VisualElement>("trophy-fill");
            arenaText = root.Q<Label>("arena-text");
            coinValue = root.Q<Label>("purse-coin-value");
            premiumValue = root.Q<Label>("purse-leaf-value");
            var premiumName = root.Q<Label>("purse-premium-name");
            if (premiumName != null) premiumName.text = LobbyCurrencyNames.Premium;
            var premiumPurse = root.Q<Button>("purse-leaf");
            if (premiumPurse != null) premiumPurse.tooltip = "Buy " + LobbyCurrencyNames.Premium;

            var strip = root.Q("strip-wrap");
            if (strip != null) strip.focusable = true;
            var nameButton = root.Q<Button>("name-button");
            if (nameButton != null) { nameButton.focusable = false; nameButton.pickingMode = PickingMode.Ignore; }
            strip?.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == UnityEngine.KeyCode.Return || evt.keyCode == UnityEngine.KeyCode.Space)
                {
                    if (root.Q("trophy-strip")?.resolvedStyle.display != DisplayStyle.None) ProfileRequested?.Invoke();
                    evt.StopPropagation();
                }
            });
            strip?.RegisterCallback<ClickEvent>(_ =>
            {
                if (root.Q("trophy-strip")?.resolvedStyle.display != DisplayStyle.None) ProfileRequested?.Invoke();
            });

            Bind(root, "purse-coin", () => CurrencyRequested?.Invoke("nodewar://shop/coins"));
            Bind(root, "purse-leaf", () => CurrencyRequested?.Invoke("nodewar://shop/laurels"));
            root.Q<Button>("history")?.Add(new LobbyBadgeDot(badges, "bell"));


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
                TrackedText.SetText(nameText, HandleDisplay.Display(BackendServices.LastKnownState?.Profile,
                    profile != null ? profile.Username : "player"));

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
                display.RR.ToString();
            if (trophyMax != null) trophyMax.text = "";
            if (trophyFill != null)
                trophyFill.style.width = Length.Percent(rank != null ? display.Fill * 100f : 0f);
            if (arenaText != null)
                arenaText.text = rank == null ? "Era " + Dash : "Era " + (display.Arena + 1) + " \u00b7 " + display.Name + (display.Span.HasValue
                    ? " \u00b7 " + (display.Span.Value - display.RRIntoArena) + " RR to next"
                    : " \u00b7 Top era");
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
