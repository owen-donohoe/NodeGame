using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    public sealed class LobbyModePicker
    {
        private readonly MkPanel sheet;
        private readonly VisualElement root;
        private readonly Toggle privateToggle;
        private LobbyModeSelection selection;
        public event Action<LobbyModeSelection> Selected;

        public LobbyModePicker(VisualElement host, LobbyOverlayRouter router)
        {
            sheet = new MkPanel { name = "game-mode-panel" };
            sheet.Title.text = "Game mode"; sheet.BindRouter(router, "game-mode"); host.Add(sheet);
            root = new VisualElement { name = "game-mode-sheet" };
            root.styleSheets.Add(Resources.Load<StyleSheet>("Lobby/ModePicker"));
            sheet.Body.Add(root);
            var grid = ModeGrid("mode-grid"); root.Add(grid);
            foreach (var mode in new[] { LobbyMode.Ranked1v1, LobbyMode.Team2v2, LobbyMode.FreeForAll4 }) Add(grid, mode);
            var local = ModeGrid("mode-local"); root.Add(local);
            Add(local, LobbyMode.Bot); Add(local, LobbyMode.Lan);
            privateToggle = new Toggle("Private (1v1)") { name = "mode-private" };
            privateToggle.AddToClassList("g2-private"); privateToggle.AddToClassList("mk-label"); root.Add(privateToggle);
            privateToggle.RegisterValueChangedCallback(evt =>
            {
                selection = new LobbyModeSelection(selection.Mode, evt.newValue);
                Selected?.Invoke(selection);
            });
        }
        private void Add(VisualElement host, LobbyMode mode)
        {
            var card = ModeCard(new LobbyModeSelection(mode), () =>
            {
                selection = new LobbyModeSelection(mode, privateToggle.value);
                Selected?.Invoke(selection); sheet.Close();
            });
            card.userData = mode; host.Add(card);
        }
        internal static VisualElement ModeGrid(string name)
        {
            var grid = new VisualElement { name = name }; grid.AddToClassList("g2-mode-grid");
            grid.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (evt.newRect.width <= 16) return;
                for (int i = 0; i < grid.childCount; i++) grid[i].style.width = (evt.newRect.width - 16f) / 2f;
            });
            return grid;
        }
        internal static Button ModeCard(LobbyModeSelection mode, Action selected)
        {
            var display = new HomeModeDisplay(mode);
            var card = new Button(selected); card.AddToClassList("mk-btn"); card.AddToClassList("g2-mode-card");
            var bar = new VisualElement(); bar.AddToClassList("g2-mode-colour");
            if (ColorUtility.TryParseHtmlString(display.Colour, out Color colour)) bar.style.backgroundColor = colour;
            card.Add(bar); card.Add(new LobbyIcon(display.Icon, LobbyIconContext.Home));
            card.Add(GuildViews.Text(display.Name, "mk-h3")); return card;
        }
        public void Open(LobbyModeSelection current)
        {
            selection = current;
            root.Query<Button>(className: "g2-mode-card").ForEach(card => card.EnableInClassList("g2-mode-card--on", card.userData is LobbyMode mode && mode == current.Mode));
            privateToggle.SetValueWithoutNotify(current.Private); sheet.Open();
        }
    }
}
