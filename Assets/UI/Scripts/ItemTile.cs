using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The square that stands where a district or suit icon would go.
    ///
    /// Shows the item's first letter on a tint derived from its ID (see
    /// <see cref="ItemTint"/>), or an empty state when the slot holding it is
    /// unfilled. Carries no size of its own - a loadout slot and a list row want
    /// different sizes, so the caller adds a modifier class and Workshop.uss
    /// supplies the numbers.
    ///
    /// The day icons exist, <see cref="SetItem"/> gains a Sprite argument and
    /// sets style.backgroundImage instead of the label. Nothing else moves.
    /// </summary>
    public class ItemTile
    {
        public VisualElement Root { get; private set; }

        private readonly Label monogram;
        private string tintClass;

        public ItemTile()
        {
            Root = new VisualElement();
            Root.AddToClassList("tile");
            Root.pickingMode = PickingMode.Ignore;

            monogram = new Label();
            monogram.AddToClassList("tile__monogram");
            monogram.pickingMode = PickingMode.Ignore;

            Root.Add(monogram);

            SetEmpty();
        }

        public void SetItem(string itemID, string displayName)
        {
            ClearTint();

            tintClass = ItemTint.ClassFor(itemID);
            Root.AddToClassList(tintClass);
            Root.RemoveFromClassList("tile--empty");

            monogram.text = ItemTint.MonogramFor(displayName, itemID);
        }

        public void SetEmpty()
        {
            ClearTint();

            Root.AddToClassList("tile--empty");
            monogram.text = "";
        }

        private void ClearTint()
        {
            if (tintClass == null) return;

            Root.RemoveFromClassList(tintClass);
            tintClass = null;
        }
    }
}
