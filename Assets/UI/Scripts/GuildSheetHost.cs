using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>The Guild's single bridge to the current shared sheet host.</summary>
    internal sealed class GuildSheetHost
    {
        private readonly LobbySheet sheet;
        private VisualElement content;

        public GuildSheetHost(LobbySheet sheet) { this.sheet = sheet; }
        public bool IsShowing => sheet.IsShowing(content);
        public void Open(VisualElement value, bool large = false)
        {
            content = value;
            sheet.Open(value, large);
        }
        public void Close()
        {
            if (IsShowing) sheet.Close();
            content = null;
        }
    }
}
