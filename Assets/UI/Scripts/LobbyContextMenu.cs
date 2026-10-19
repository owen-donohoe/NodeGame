using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>One row of a context menu.</summary>
    public struct LobbyMenuItem
    {
        public readonly string Label;
        public readonly System.Action Action;

        public LobbyMenuItem(string label, System.Action action)
        {
            Label = label;
            Action = action;
        }
    }

    /// <summary>
    /// The menu a long press opens: a scrim, the pressed element lifted, and a
    /// short list anchored where the finger was.
    ///
    /// One instance for the whole lobby. Pages attach it with
    /// <see cref="Attach"/>, which adds a <see cref="LongPressManipulator"/>
    /// and supplies the rows at the moment of the press, so a menu can depend
    /// on current state (a card's "Add to loadout" when it is already in).
    /// </summary>
    public class LobbyContextMenu
    {
        // Menu geometry from the prototype: 52px rows, kept 12px from any edge,
        // opened below the finger unless that would run off the bottom.
        private const float RowHeight = 52f;
        private const float Edge = 12f;
        private const float BelowFinger = 18f;
        private const float MenuWidth = 212f;

        private readonly VisualElement layer;
        private readonly VisualElement scrim;
        private readonly VisualElement menu;
        private readonly MkSheet sheet;
        private VisualElement lifted;

        public LobbyContextMenu(VisualElement root)
        {
            layer = root.Q<VisualElement>("menu-layer");
            layer.Clear();
            sheet = new MkSheet { name = "menu" };
            layer.Add(sheet);
            menu = sheet.Body;
            sheet.Closed += Close;
        }

        public bool IsOpen { get; private set; }
        public event System.Action Opened;
        public event System.Action Closed;

        /// <summary>
        /// Gives an element a long-press menu. The rows are asked for when the
        /// press fires; returning none means no menu.
        /// </summary>
        public void Attach(VisualElement element, System.Func<IList<LobbyMenuItem>> rows)
        {
            if (element == null || rows == null) return;

            element.AddManipulator(new LongPressManipulator((source, position) =>
            {
                IList<LobbyMenuItem> items = rows();
                if (items != null && items.Count > 0) Show(source, position, items);
            }));
        }

        public void Show(VisualElement source, Vector2 panelPosition, IList<LobbyMenuItem> items)
        {
            if (menu == null || layer == null) return;

            Close();

            menu.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                LobbyMenuItem item = items[i];

                Button row = new Button();
                row.text = item.Label;
                row.AddToClassList("mk-btn");
                row.AddToClassList("lb-menu__row");
                row.AddToClassList("ui-w500");
                if (i == items.Count - 1) row.AddToClassList("lb-menu__row--last");

                row.clicked += () =>
                {
                    Close();
                    if (item.Action != null) item.Action();
                };

                menu.Add(row);
            }

            menu.pickingMode = PickingMode.Position;
            sheet.Open();

            lifted = source;
            if (lifted != null) lifted.AddToClassList("lb-lifted");

            // TODO(settings): the prototype fires a haptic here, gated by the
            // Haptics setting. Settings are not saved yet, so there is no flag.

            IsOpen = true;
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            sheet.Close();

            if (menu != null)
            {
                // The rows stay in place so the menu can fade out, but a faded
                // row must not keep catching taps meant for the page beneath.
                menu.RemoveFromClassList("lb-menu--on");
                menu.pickingMode = PickingMode.Ignore;
                foreach (VisualElement row in menu.Children()) row.pickingMode = PickingMode.Ignore;
            }

            if (scrim != null)
            {
                scrim.RemoveFromClassList("ui-scrim--on");
                scrim.pickingMode = PickingMode.Ignore;
            }

            if (lifted != null) lifted.RemoveFromClassList("lb-lifted");
            lifted = null;
            Closed?.Invoke();
        }
    }
}
