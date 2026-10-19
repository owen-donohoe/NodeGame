using NodeWar.Backend;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    internal static class GuildViews
    {
        public static VisualElement Header(GuildView guild)
        {
            var header = new VisualElement();
            header.AddToClassList("lb-guild-header");
            header.Add(PlayerCardViews.Text("[" + guild.Summary.IconId + "] " + guild.Summary.Name));
            header.Add(PlayerCardViews.Text(guild.Summary.Members + "/" + guild.Summary.Capacity + " members · " + guild.OnlineCount + " online"));
            return header;
        }

        public static void Icon(Button button, LobbyIconKind kind)
        {
            var icon = new LobbyIcon(kind) { pickingMode = PickingMode.Ignore };
            icon.style.width = icon.style.height = 22f;
            button.Add(icon);
        }
    }
}
