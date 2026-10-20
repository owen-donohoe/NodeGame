using System;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>Named panel facade; centered modal surfaces share MkPanel.</summary>
    public sealed class LobbyOverlayPanel
    {
        private readonly MkPanel sheet = new MkPanel();
        public VisualElement Root => sheet;
        public VisualElement Content { get; } = new ScrollView { name = "panel-content" };
        public bool IsOpen => sheet.IsOpen;
        public event Action Opened;
        public event Action Closed;

        public LobbyOverlayPanel(string title)
        {
            sheet.Title.text = title; sheet.Title.name = "panel-title";
            Content.style.minHeight = 0; Content.style.flexShrink = 1;
            sheet.Body.Add(Content);
            sheet.Opened += () => Opened?.Invoke();
            sheet.Closed += () => Closed?.Invoke();
        }
        public void Open() => sheet.Open();
        public void Close() => sheet.Close();
        public void Update() { }
    }
}
