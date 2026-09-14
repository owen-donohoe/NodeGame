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
        private readonly SuitDefinition[] allSuits;
        private readonly NodeDefinition[] allNodes;

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

        public HomePage(VisualTreeAsset layout, LobbyToast toast,
                        SuitDefinition[] allSuits, NodeDefinition[] allNodes)
            : base(LobbyPageID.Home, Build(layout))
        {
            this.toast = toast;
            this.allSuits = allSuits;
            this.allNodes = allNodes;

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
        }

        private static VisualElement Build(VisualTreeAsset layout)
        {
            VisualElement root = new VisualElement();
            root.name = "page-home";
            root.AddToClassList("lb-home-host");
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

            LoadoutData loadout = profile != null
                ? LoadoutData.Normalized(profile.Loadout)
                : LoadoutData.CreateEmpty();

            for (int i = 0; i < nodeChips.Length; i++)
                SetChip(nodeChips[i], loadout.nodeIDs[i], NodeName(loadout.nodeIDs[i]));
            for (int i = 0; i < suitChips.Length; i++)
                SetChip(suitChips[i], loadout.suitIDs[i], SuitName(loadout.suitIDs[i]));
        }

        /// <summary>
        /// A chip shows the item's monogram, muted. TODO(art): the prototype
        /// shows each item's icon here; no district or suit icons exist yet.
        /// </summary>
        private static void SetChip(VisualElement chip, string id, string displayName)
        {
            if (chip == null) return;

            chip.Clear();
            if (string.IsNullOrEmpty(id)) return;

            Label letter = new Label(ItemTint.MonogramFor(displayName, id));
            letter.AddToClassList("lb-lpi__letter");
            letter.AddToClassList("lb-w600");
            letter.pickingMode = PickingMode.Ignore;
            chip.Add(letter);
        }

        private string NodeName(string id)
        {
            if (allNodes == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < allNodes.Length; i++)
                if (allNodes[i] != null && allNodes[i].nodeID == id) return allNodes[i].displayName;
            return null;
        }

        private string SuitName(string id)
        {
            if (allSuits == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < allSuits.Length; i++)
                if (allSuits[i] != null && allSuits[i].suitID == id) return allSuits[i].displayName;
            return null;
        }
    }
}
