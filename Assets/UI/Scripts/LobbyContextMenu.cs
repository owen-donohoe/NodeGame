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
    /// The menu a long press opens: a shared sheet, the pressed element lifted, and a
    /// short list of actions.
    ///
    /// One instance for the whole lobby. Pages attach it with
    /// <see cref="Attach"/>, which adds a <see cref="LongPressManipulator"/>
    /// and supplies the rows at the moment of the press, so a menu can depend
    /// on current state (a card's "Add to loadout" when it is already in).
    /// </summary>
    public class LobbyContextMenu
    {
        private readonly VisualElement layer;
        private readonly VisualElement menu;
        private readonly MkSheet sheet;
        private VisualElement lifted;
        private readonly VisualElement bubbleScrim = new VisualElement();
        private readonly VisualElement bubble = new VisualElement();

        public LobbyContextMenu(VisualElement root)
        {
            layer = root.Q<VisualElement>("menu-layer");
            layer.Clear();
            sheet = new MkSheet { name = "menu" };
            layer.Add(sheet);
            menu = sheet.Body;
            sheet.Closed += Close;
            bubbleScrim.AddToClassList("lb-context-bubble-scrim");
            bubble.AddToClassList("lb-context-bubble");
            var styles = Resources.Load<StyleSheet>("Lobby/ContextBubble");
            if (styles != null) layer.styleSheets.Add(styles);
            layer.Add(bubbleScrim); layer.Add(bubble);
            bubbleScrim.style.display = bubble.style.display = DisplayStyle.None;
            bubbleScrim.RegisterCallback<PointerDownEvent>(evt => { Close(); evt.StopPropagation(); });
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
                row.AddToClassList("ui-w500");

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

        /// <summary>Anchored player actions, kept on the shared menu layer above a room sheet.</summary>
        public void ShowBubble(VisualElement source, IList<LobbyMenuItem> items)
        {
            if (source == null || items == null || items.Count == 0) return;
            Close(); bubble.Clear();
            var tip = new VisualElement { pickingMode = PickingMode.Ignore }; tip.AddToClassList("lb-context-bubble-tip"); bubble.Add(tip);
            foreach (var item in items)
            {
                var row = GuildViews.Button(item.Label, () => { Close(); item.Action?.Invoke(); });
                bubble.Add(row);
            }
            bubbleScrim.style.display = bubble.style.display = DisplayStyle.Flex;
            bubbleScrim.BringToFront(); bubble.BringToFront();
            var point = layer.WorldToLocal(new Vector2(source.worldBound.center.x, source.worldBound.yMax));
            float width = Mathf.Min(240, Mathf.Max(160, layer.layout.width - 24));
            float left = Mathf.Clamp(point.x - width / 2, 12, Mathf.Max(12, layer.layout.width - width - 12));
            float height = items.Count * 52 + 32;
            bool above = point.y + height + 12 > layer.layout.height;
            bubble.style.width = width;
            bubble.style.left = left;
            bubble.style.top = above ? Mathf.Max(12, layer.WorldToLocal(new Vector2(0, source.worldBound.yMin)).y - height - 8) : point.y + 8;
            tip.style.left = Mathf.Clamp(point.x - left - 8, 16, width - 32);
            tip.EnableInClassList("lb-context-bubble-tip--below", above);
            lifted = source; lifted.AddToClassList("lb-lifted");
            IsOpen = true; Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            bubbleScrim.style.display = bubble.style.display = DisplayStyle.None;
            sheet.Close();

            if (menu != null)
            {
                // The rows stay in place so the menu can fade out, but a faded
                // row must not keep catching taps meant for the page beneath.
                menu.pickingMode = PickingMode.Ignore;
                foreach (VisualElement row in menu.Children()) row.pickingMode = PickingMode.Ignore;
            }

            if (lifted != null) lifted.RemoveFromClassList("lb-lifted");
            lifted = null;
            Closed?.Invoke();
        }
    }
}
