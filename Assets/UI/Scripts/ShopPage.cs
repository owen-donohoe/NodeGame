using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Shop. Replaces ShopPanel.
    ///
    /// Nothing is for sale, because there is nothing to sell it for: no
    /// currency exists anywhere in the project. The only economy-shaped state
    /// PlayerProfile carries is boxesAvailable and boxProgress, and until this
    /// page nothing read either. So the page shows those two honestly and says
    /// what would feed them, rather than inventing a coin balance that would
    /// then have to be un-invented.
    ///
    /// The categories are placeholders, and they are cosmetic or accelerative
    /// on purpose. The monetisation rule on record is free to play, nothing
    /// that affects competitive results - and a placeholder that models
    /// pay-to-win is the one that gets copied when the real shop is built.
    /// </summary>
    public class ShopPage : LobbyPage
    {
        /// <summary>
        /// What a shop under the stated rule is allowed to contain. Each is a
        /// marked placeholder with no price, because a price implies a currency
        /// and there is none.
        ///
        /// Deliberately no suits, districts or stat upgrades. If a fourth
        /// category is ever added here and it changes what happens in a match,
        /// it is the rule that broke, not this list.
        /// </summary>
        private static readonly string[][] Categories =
        {
            new[] { "Villager looks",  "Skins for the units on the board" },
            new[] { "Board themes",    "How the map and its districts are drawn" },
            new[] { "Progress boosts", "Faster unlocks, never exclusive ones" }
        };

        private readonly Label boxCountLabel;
        private readonly VisualElement boxFill;

        public ShopPage(VisualTreeAsset layout)
            : base(LobbyPageID.Shop, Build(layout))
        {
            boxCountLabel = Root.Q<Label>("shop-box-count");
            boxFill = Root.Q<VisualElement>("shop-box-fill");

            BuildCategories();
        }

        private static VisualElement Build(VisualTreeAsset layout)
        {
            VisualElement root = new VisualElement();
            root.name = "page-shop";

            if (layout != null)
            {
                layout.CloneTree(root);
            }
            else
            {
                VisualElement box = new VisualElement();
                box.AddToClassList("placeholder");
                box.style.flexGrow = 1;

                Label note = new Label("Shop layout missing - assign ShopPage.uxml");
                note.AddToClassList("placeholder__label");

                box.Add(note);
                root.Add(box);
            }

            return root;
        }

        public override void OnShow()
        {
            Refresh();
        }

        private void Refresh()
        {
            PlayerProfile profile = PlayerProfile.Instance;

            int boxes = profile != null ? profile.BoxesAvailable : 0;
            float progress = profile != null ? profile.BoxProgress : 0f;

            if (boxCountLabel != null)
                boxCountLabel.text = boxes.ToString();

            if (boxFill != null)
            {
                // Clamped rather than trusted: nothing writes boxProgress today,
                // so the first thing that does may well write something outside
                // 0-1 and a bar wider than its track would be the only symptom.
                float clamped = progress < 0f ? 0f : (progress > 1f ? 1f : progress);
                boxFill.style.width = Length.Percent(clamped * 100f);
            }
        }

        private void BuildCategories()
        {
            VisualElement host = Root.Q<VisualElement>("shop-categories");
            if (host == null) return;

            for (int i = 0; i < Categories.Length; i++)
            {
                VisualElement tile = new VisualElement();
                tile.AddToClassList("placeholder");
                tile.AddToClassList("shop__category");

                Label name = new Label(Categories[i][0]);
                name.AddToClassList("placeholder__label");
                name.AddToClassList("shop__category-name");

                Label detail = new Label(Categories[i][1]);
                detail.AddToClassList("placeholder__label");

                tile.Add(name);
                tile.Add(detail);
                host.Add(tile);
            }
        }
    }
}
