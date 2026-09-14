using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The lobby's landing page, after lobby-prototype.html: boxes, a quiet
    /// loadout preview, the villager, BATTLE and the next-box meter.
    ///
    /// Identity and trophies are not here any more - they live in the
    /// persistent chrome (<see cref="LobbyChrome"/>), which every page shares.
    ///
    /// Reads PlayerProfile on show. Every control does something or says why it
    /// cannot yet, through the toast.
    /// </summary>
    public class HomePage : LobbyPage
    {
        // Villager bob: the prototype's 3.4s keyframe loop, as a class that
        // flips every half cycle and a transition that eases between.
        private const long BobHalfCycleMs = 1700;

        private readonly LobbyToast toast;
        private readonly LobbyContextMenu menu;
        private readonly LoadoutCatalog catalog;

        private readonly Label victoryBadge;
        private readonly VisualElement boxMeterFill;
        private readonly VisualElement[] nodeChips = new VisualElement[LoadoutData.NodeSlots];
        private readonly VisualElement[] suitChips = new VisualElement[LoadoutData.SuitSlots];
        private readonly VisualElement villager;
        private IVisualElementScheduledItem bob;

        /// <summary>Raised when BATTLE is pressed. The shell opens the sheet.</summary>
        public event System.Action PlayRequested;

        /// <summary>Raised when the loadout preview is pressed.</summary>
        public event System.Action LoadoutRequested;

        public HomePage(VisualTreeAsset layout, LobbyToast toast, LobbyContextMenu menu, LoadoutCatalog catalog)
            : base(LobbyPageID.Home, Build(layout))
        {
            this.toast = toast;
            this.menu = menu;
            this.catalog = catalog != null ? catalog : new LoadoutCatalog(null, null);

            victoryBadge = Root.Q<Label>("home-box-victory-badge");
            boxMeterFill = Root.Q<VisualElement>("home-boxmeter-fill");
            villager = Root.Q<VisualElement>("home-villager");

            for (int i = 0; i < nodeChips.Length; i++)
                nodeChips[i] = Root.Q<VisualElement>("home-loadout-node-" + i);
            for (int i = 0; i < suitChips.Length; i++)
                suitChips[i] = Root.Q<VisualElement>("home-loadout-suit-" + i);

            Bind("home-battle", () => { if (PlayRequested != null) PlayRequested(); });
            Bind("home-loadout", () => { if (LoadoutRequested != null) LoadoutRequested(); });

            // TODO(boxes): nothing opens a box, and nothing writes boxProgress yet.
            Bind("home-box-victory", () => Say("Opening boxes arrives in a later update"));
            Bind("home-box-daily", () => Say("Daily boxes arrive in a later update"));
            Bind("home-boxmeter", () => Say("Boxes fill by playing. Opening them arrives later"));

            // TODO(villager): there is no appearance editor.
            Bind("home-villager", () => Say("Villager customisation arrives in a later update"));

            AttachMenus();
        }

        /// <summary>The prototype's long-press menus on the boxes and the villager.</summary>
        private void AttachMenus()
        {
            if (menu == null) return;

            System.Func<IList<LobbyMenuItem>> boxRows = () => new List<LobbyMenuItem>
            {
                new LobbyMenuItem("What is in a box", () => Say("Cosmetics, and rarely an unlock from your arena")),
                new LobbyMenuItem("Open now", () => Say("Opening boxes arrives in a later update")),
                new LobbyMenuItem("History", () => Say("Box history arrives in a later update")),
            };

            menu.Attach(Root.Q<Button>("home-box-victory"), boxRows);
            menu.Attach(Root.Q<Button>("home-box-daily"), boxRows);

            menu.Attach(Root.Q<Button>("home-villager"), () => new List<LobbyMenuItem>
            {
                new LobbyMenuItem("Change appearance", () => Say("Villager customisation arrives in a later update")),
                new LobbyMenuItem("Go to Workshop", () => { if (LoadoutRequested != null) LoadoutRequested(); }),
            });
        }

        private static VisualElement Build(VisualTreeAsset layout)
        {
            VisualElement root = new VisualElement();
            root.name = "page-home";
            root.AddToClassList("lb-page-flush");
            root.pickingMode = PickingMode.Ignore;

            if (layout != null) layout.CloneTree(root);

            return root;
        }

        private void Bind(string name, System.Action action)
        {
            Button button = Root.Q<Button>(name);
            if (button != null) button.clicked += action;
        }

        private void Say(string message)
        {
            if (toast != null) toast.Show(message);
        }

        public override void OnShow()
        {
            Refresh();

            if (villager != null && bob == null)
            {
                bob = villager.schedule
                    .Execute(() => villager.ToggleInClassList("lb-villager--up"))
                    .Every(BobHalfCycleMs);
            }
            else if (bob != null)
            {
                bob.Resume();
            }
        }

        public override void OnHide()
        {
            if (bob != null) bob.Pause();
        }

        public void Refresh()
        {
            PlayerProfile profile = PlayerProfile.Instance;

            if (victoryBadge != null)
            {
                int boxes = profile != null ? profile.BoxesAvailable : 0;
                victoryBadge.text = boxes.ToString();
                victoryBadge.EnableInClassList("lb-badge--zero", boxes <= 0);
            }

            if (boxMeterFill != null)
            {
                float progress = profile != null ? Mathf.Clamp01(profile.BoxProgress) : 0f;
                boxMeterFill.style.width = Length.Percent(progress * 100f);
            }

            // A side whose empty slots the player could fill is flagged, so an
            // unfinished loadout is visible before BATTLE. A side they cannot
            // fill yet is left quiet.
            LoadoutEditor loadout = LoadoutCatalog.CurrentLoadout();
            bool nodesShort = loadout.IsNodeSideShort(catalog.OwnedNodeCount());
            bool suitsShort = loadout.IsSuitSideShort(catalog.OwnedSuitCount());

            for (int i = 0; i < nodeChips.Length; i++)
            {
                string id = loadout.NodeAt(i);
                SetChip(nodeChips[i], id, catalog.NodeName(id), nodesShort);
            }

            for (int i = 0; i < suitChips.Length; i++)
            {
                string id = loadout.SuitAt(i);
                SetChip(suitChips[i], id, catalog.SuitName(id), suitsShort);
            }
        }

        /// <summary>
        /// A filled chip is navy and shows the item's monogram. An empty chip is
        /// orange when its side is short - fillable but not filled - and the
        /// plain quiet chip otherwise. Colour never carries this alone: a filled
        /// chip has a letter, an empty one has none.
        /// TODO(art): the prototype shows each item's icon here; no icons exist yet.
        /// </summary>
        private static void SetChip(VisualElement chip, string id, string displayName, bool sideShort)
        {
            if (chip == null) return;

            bool filled = !string.IsNullOrEmpty(id);
            chip.EnableInClassList("lb-lpi--filled", filled);
            chip.EnableInClassList("lb-lpi--short", !filled && sideShort);

            chip.Clear();
            if (!filled) return;

            Label letter = new Label(ItemTint.MonogramFor(displayName, id));
            letter.AddToClassList("lb-lpi__letter");
            letter.AddToClassList("lb-w600");
            letter.pickingMode = PickingMode.Ignore;
            chip.Add(letter);
        }

    }
}
