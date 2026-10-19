using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    public sealed class LobbyModePicker
    {
        private readonly LobbySheet sheet;
        private readonly VisualElement root = new VisualElement();
        private readonly Toggle privateToggle;
        private LobbyModeSelection selection;
        public event Action<LobbyModeSelection> Selected;

        public LobbyModePicker(LobbySheet sheet)
        {
            this.sheet = sheet;
            Resources.Load<VisualTreeAsset>("Lobby/ModePicker")?.CloneTree(root);
            privateToggle = root.Q<Toggle>("mode-private");
            privateToggle?.RegisterValueChangedCallback(evt =>
            {
                selection = new LobbyModeSelection(selection.Mode, evt.newValue);
                Selected?.Invoke(selection);
            });
            AddCard("mode-grid", LobbyMode.Ranked1v1);
            AddCard("mode-grid", LobbyMode.Team2v2);
            AddCard("mode-grid", LobbyMode.FreeForAll4);
            AddCard("mode-local", LobbyMode.Bot);
            AddCard("mode-local", LobbyMode.Lan);
        }

        private void AddCard(string host, LobbyMode mode)
        {
            var display = new HomeModeDisplay(new LobbyModeSelection(mode));
            var card = new Button(() =>
            {
                selection = new LobbyModeSelection(mode, privateToggle?.value == true);
                Selected?.Invoke(selection);
                sheet.Close();
            });
            card.AddToClassList("lb-mode-card");
            card.Add(new LobbyIcon(display.Icon, LobbyIconContext.Home));
            card.Add(new Label(display.Name) { pickingMode = PickingMode.Ignore });
            if (ColorUtility.TryParseHtmlString(display.Colour, out Color colour)) card.style.backgroundColor = colour;
            root.Q<VisualElement>(host)?.Add(card);
        }

        public void Open(LobbyModeSelection current)
        {
            selection = current;
            privateToggle?.SetValueWithoutNotify(current.Private);
            sheet.Open(root, true);
        }
    }
}
