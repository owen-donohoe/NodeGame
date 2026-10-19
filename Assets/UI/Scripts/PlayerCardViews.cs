using System;
using NodeWar.Backend;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    internal static class PlayerCardViews
    {
        public static string Handle(PlayerCard card) => card == null ? "Unknown player" :
            HandleDisplay.Display(card.Name, card.Tag, card.PlayerId ?? "Unknown player");

        public static VisualElement Card(PlayerCard card, Action open, bool spectate)
        {
            var root = new VisualElement();
            root.AddToClassList("lb-social-player");
            var button = new PaperButton(open);
            button.AddToClassList("lb-player-open");
            var dot = new VisualElement { tooltip = (card?.Presence ?? PlayerPresence.Offline).ToString(), pickingMode = PickingMode.Ignore };
            dot.AddToClassList("lb-presence");
            dot.AddToClassList(card?.Presence == PlayerPresence.InBattle ? "lb-presence--battle" :
                card?.Presence == PlayerPresence.Online ? "lb-presence--online" : "lb-presence--offline");
            button.Add(dot);
            button.Add(Text(Handle(card)));
            if (card != null)
                button.Add(Text(new RankDisplay(card.RR).Name + " · " + card.RR + " RR · " +
                    (card.Presence == PlayerPresence.InBattle ? "In battle" : card.Presence.ToString())));
            root.Add(button);
            if (spectate && card?.Presence == PlayerPresence.InBattle)
            {
                var watch = new PaperButton(() => UnityEngine.Debug.Log("[Social] spectate " + card.PlayerId)) { text = "Spectate" };
                watch.AddToClassList("lb-social-red");
                root.Add(watch);
            }
            return root;
        }

        public static Button IconButton(LobbyIconKind icon, string label, Action clicked)
        {
            var button = new PaperButton(clicked) { tooltip = label };
            button.AddToClassList("lb-social-square");
            var glyph = new LobbyIcon(icon) { pickingMode = PickingMode.Ignore };
            glyph.style.width = glyph.style.height = 22f;
            button.Add(glyph);
            return button;
        }

        public static Label Text(string value) => new Label(value) { enableRichText = false, pickingMode = PickingMode.Ignore };
    }
}
