using System;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>Home's bottom controls leave the equipped 3D stage visible.</summary>
    public class HomePage : LobbyPage
    {
        private readonly LobbyToast toast;
        private readonly LobbyToast loadoutToast;
        private readonly LoadoutCatalog catalog;
        private readonly AccountLinkPrompt accountLinkPrompt;
        private readonly ICrateInventory crates;
        private readonly Label smallCount;
        private readonly Label mediumCount;
        private readonly VisualElement progressFill;
        private readonly LobbyIcon nextIcon;
        private readonly Label modeName;
        private readonly LobbyIcon modeIcon;
        public GameMode SelectedMode { get; private set; }
        public event Action PlayRequested;
        public event Action ModeRequested;
        public event Action<CrateSize> CrateRequested;

        public HomePage(VisualTreeAsset layout, LobbyToast toast, LobbyContextMenu menu, LoadoutCatalog catalog,
            AccountFlow accountFlow, Func<bool> canPrompt, ICrateInventory crates = null)
            : base(LobbyPageID.Home, Build(layout))
        {
            this.toast = toast;
            loadoutToast = new LobbyToast(Root.Q<Label>("home-toast"), 2500);
            this.catalog = catalog ?? new LoadoutCatalog(null, null);
            this.crates = crates;
            accountLinkPrompt = new AccountLinkPrompt(accountFlow, canPrompt, Say);
            smallCount = Root.Q<Label>("home-crate-small-count");
            mediumCount = Root.Q<Label>("home-crate-medium-count");
            progressFill = Root.Q<VisualElement>("home-boxmeter-fill");
            nextIcon = Root.Q<LobbyIcon>("home-next-crate-icon");
            modeName = Root.Q<Label>("home-mode-name");
            modeIcon = Root.Q<LobbyIcon>("home-mode-icon");
            Bind("home-mode", () => ModeRequested?.Invoke());
            Bind("home-crate-small", () => CrateRequested?.Invoke(CrateSize.Small));
            Bind("home-crate-medium", () => CrateRequested?.Invoke(CrateSize.Medium));
            Bind("home-battle", () =>
            {
                if (!LoadoutRules.IsComplete(this.catalog.CurrentLoadout().ToLoadout(), this.catalog.Arena))
                {
                    loadoutToast.Show("Fill your loadout");
                    return;
                }
                PlayRequested?.Invoke();
            });
            Root.RegisterCallback<DetachFromPanelEvent>(_ => OnHide());
            SetMode(LobbyModeSelection.FromProfile(PlayerProfile.Instance != null ? PlayerProfile.Instance.data : default));
        }

        private static VisualElement Build(VisualTreeAsset fallback)
        {
            var root = new VisualElement { name = "page-home", pickingMode = PickingMode.Ignore };
            root.AddToClassList("lb-page-flush");
            var layout = Resources.Load<VisualTreeAsset>("Lobby/HomePage") ?? fallback;
            if (layout != null) layout.CloneTree(root);
            return root;
        }

        private void Bind(string name, Action action)
        {
            var button = Root.Q<Button>(name);
            if (button != null) button.clicked += action;
        }

        private void Say(string message) { toast?.Show(message); }

        public void SetMode(GameMode mode)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            if (mode == GameMode.Testing) mode = GameMode.Locked;
#endif
            SelectedMode = Enum.IsDefined(typeof(GameMode), mode) ? mode : GameMode.Locked;
            var display = new HomeModeDisplay(SelectedMode);
            if (modeName != null) modeName.text = display.Name;
            if (modeIcon != null) modeIcon.Kind = display.Icon;
            if (ColorUtility.TryParseHtmlString(display.Colour, out Color colour)) Root.Q("home-mode-dot").style.backgroundColor = colour;
}

        public void SetMode(LobbyModeSelection selection)
        {
            SelectedMode = selection.LegacyMode;
            var display = new HomeModeDisplay(selection);
            if (modeName != null) modeName.text = display.Name;
            if (modeIcon != null) modeIcon.Kind = display.Icon;
}

        public override void OnShow()
        {
            BackendServices.StateChanged -= Refresh;
            BackendServices.StateChanged += Refresh;
            if (crates != null) { crates.Changed -= Refresh; crates.Changed += Refresh; }
            Refresh();
            accountLinkPrompt.Show();
            if (!LoadoutRules.IsComplete(catalog.CurrentLoadout().ToLoadout(), catalog.Arena)) loadoutToast.Show("Fill your loadout");
        }

        public override void OnHide()
        {
            BackendServices.StateChanged -= Refresh;
            if (crates != null) crates.Changed -= Refresh;
            accountLinkPrompt.Hide();
        }

        public void Refresh()
        {
            SetCount(smallCount, crates?.Count(CrateSize.Small) ?? 0);
            SetCount(mediumCount, crates?.Count(CrateSize.Medium) ?? 0);
            Root.Q("home-crate-small")?.EnableInClassList("mk-crate--dim", (crates?.Count(CrateSize.Small) ?? 0) == 0);
            Root.Q("home-crate-medium")?.EnableInClassList("mk-crate--dim", (crates?.Count(CrateSize.Medium) ?? 0) == 0);
            if (progressFill != null) progressFill.style.width = Length.Percent((crates?.NextProgress ?? 0f) * 100f);
            if (nextIcon != null) nextIcon.Kind = crates?.NextSize == CrateSize.Medium ? LobbyIconKind.CrateMedium : LobbyIconKind.CrateSmall;
            bool complete = LoadoutRules.IsComplete(catalog.CurrentLoadout().ToLoadout(), catalog.Arena);
            var play = Root.Q<Button>("home-battle");
            if (play != null)
            {
                play.SetEnabled(true); // A disabled button swallows the tap; the toast explains why BATTLE is off.
                play.EnableInClassList("mk-battle--off", !complete);
                play.tooltip = complete ? "Play" : "Fill your loadout";
            }
        }
        private static void SetCount(Label badge, int count)
        {
            if (badge == null) return;
            badge.text = count.ToString();
            badge.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
