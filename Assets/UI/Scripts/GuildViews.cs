using System;
using NodeWar.Backend;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    internal static class GuildViews
    {
        public static Label Text(string value, string style = null)
        {
            var label = new Label(value) { enableRichText = false };
            if (style != null) label.AddToClassList(style);
            return label;
        }

        public static VisualElement Card(string title)
        {
            var card = new VisualElement();
            card.AddToClassList("mk-card"); card.AddToClassList("lb-guild-card");
            if (!string.IsNullOrEmpty(title)) card.Add(Text(title, "mk-eyebrow"));
            return card;
        }

        public static Button Button(string label, Action clicked, string variant = null)
        {
            var button = new Button(clicked) { text = label };
            button.AddToClassList("mk-btn");
            if (variant != null) button.AddToClassList(variant);
            return button;
        }

        public static VisualElement Header(GuildView guild, bool showWeekly = false)
        {
            var header = Card(null); header.AddToClassList("lb-guild-header");
            var row = new VisualElement(); row.AddToClassList("mk-row");
            var icon = new LobbyIcon(LobbyIconKind.Guild) { tooltip = guild.Summary.IconId, pickingMode = PickingMode.Ignore };
            icon.AddToClassList("lb-guild-emblem"); row.Add(icon);
            var text = new VisualElement(); text.AddToClassList("lb-guild-row-text");
            text.Add(Text(guild.Summary.Name, "mk-h2"));
            text.Add(Text(guild.Summary.Members + "/" + guild.Summary.Capacity + " members · " + guild.OnlineCount + " online", "mk-muted"));
            row.Add(text); header.Add(row);
            if (!showWeekly) return header;
            var meter = new VisualElement(); meter.AddToClassList("mk-meter");
            var fill = new VisualElement(); fill.AddToClassList("mk-meter__fill");
            int progress = guild.WeeklyGoal > 0 ? Math.Max(0, Math.Min(100, (int)((long)guild.WeeklyProgress * 100 / guild.WeeklyGoal))) : 0;
            fill.style.width = Length.Percent(progress); meter.Add(fill); header.Add(meter);
            header.Add(Text("Weekly · " + guild.WeeklyProgress + "/" + guild.WeeklyGoal + " towards " + (guild.WeeklyReward ?? "the weekly reward"), "mk-cap"));
            return header;
        }

        public static string Role(GuildRole role) => role == GuildRole.Leader ? "Leader" : role == GuildRole.CoLeader ? "Co-leader" : "Member";

        public static VisualElement Member(GuildMember member, Action open)
        {
            var row = new Button(open); row.AddToClassList("mk-friend"); row.AddToClassList("lb-guild-member");
            string name = member.Player.Name ?? "?";
            var avatar = Text(name.Length > 0 ? name.Substring(0, 1) : "?", "mk-avatar");
            row.Add(avatar);
            var text = new VisualElement { pickingMode = PickingMode.Ignore }; text.AddToClassList("lb-guild-row-text");
            text.Add(Text(PlayerCardViews.Handle(member.Player), "mk-friend__name"));
            text.Add(Text(Role(member.Role) + " · " + (member.Online ? "Online" : "Offline"), "mk-muted"));
            row.Add(text);
            var trailing = new VisualElement { pickingMode = PickingMode.Ignore }; trailing.AddToClassList("mk-friend__trailing");
            trailing.Add(Text(member.RR + " RR", "mk-number")); row.Add(trailing);
            return row;
        }

        public static VisualElement GuildRow(GuildSummary guild, Action open)
        {
            var row = new Button(open); row.AddToClassList("mk-card"); row.AddToClassList("lb-guild-row");
            Icon(row, LobbyIconKind.Guild);
            var text = new VisualElement { pickingMode = PickingMode.Ignore }; text.AddToClassList("lb-guild-row-text");
            text.Add(Text(guild.Name, "mk-friend__name")); text.Add(Text(guild.Members + "/" + guild.Capacity + " members", "mk-muted"));
            row.Add(text);
            var chip = Text(guild.Joinable && guild.Members < guild.Capacity ? "Join" : "Closed", "mk-chip");
            chip.pickingMode = PickingMode.Ignore; row.Add(chip);
            return row;
        }

        public static VisualElement Post(GuildPost post, bool pinned)
        {
            var row = new VisualElement(); row.AddToClassList("lb-guild-post");
            string kind = pinned ? "Pinned" : post.Kind == GuildPostKind.ChatAnnouncement ? "From chat" :
                post.Kind == GuildPostKind.DistrictUnlocked ? "District unlocked" : post.Kind == GuildPostKind.SuitUnlocked ? "Suit unlocked" :
                post.Kind == GuildPostKind.TournamentWon ? "Tournament won" : post.Kind == GuildPostKind.EraReached ? "New era" : "Guild news";
            row.Add(Text(kind, "mk-eyebrow")); row.Add(Text(post.Text));
            return row;
        }

        public static VisualElement Message(ChatMessage message)
        {
            var row = Card(null); row.AddToClassList("lb-guild-message");
            row.Add(Text(PlayerCardViews.Handle(message.Sender), "mk-friend__name")); row.Add(Text(message.Text));
            return row;
        }

        public static void Icon(Button button, LobbyIconKind kind)
        {
            var icon = new LobbyIcon(kind) { pickingMode = PickingMode.Ignore };
            icon.style.width = icon.style.height = 22f;
            button.Add(icon);
        }
    }
}
