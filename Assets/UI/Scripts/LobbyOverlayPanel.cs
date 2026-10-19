using System;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>Named panel facade; all modal surfaces share MkSheet.</summary>
    public sealed class LobbyOverlayPanel
    {
        private readonly MkSheet sheet = new MkSheet();
        public VisualElement Root => sheet;
        public VisualElement Content { get; } = new VisualElement { name = "panel-content" };
        public bool IsOpen => sheet.IsOpen;
        public event Action Opened;
        public event Action Closed;

        public LobbyOverlayPanel(string title)
        {
            var heading = new VisualElement(); heading.AddToClassList("mk-spread");
            var label = new Label(title) { name = "panel-title", enableRichText = false };
            label.AddToClassList("mk-h2"); heading.Add(label);
            var close = new Button(Close) { name = "panel-close", text = "Close" };
            close.AddToClassList("mk-btn"); heading.Add(close);
            sheet.Body.Add(heading); sheet.Body.Add(Content);
            sheet.Opened += () => Opened?.Invoke();
            sheet.Closed += () => Closed?.Invoke();
        }
        public void Open() => sheet.Open();
        public void Close() => sheet.Close();
        public void Update() { }
    }
}
