using System;
using System.Collections.Generic;
using NodeWar.Backend;

namespace NodeWar.Lobby
{
    public static class TeammateInviteRoster
    {
        public static string PresenceLabel(PlayerPresence value) => value == PlayerPresence.Online ? "In lobby"
            : value == PlayerPresence.InBattle ? "In battle" : "Offline";
        public static string PresenceClass(PlayerPresence value) => value == PlayerPresence.Online ? "online"
            : value == PlayerPresence.InBattle ? "battle" : "offline";
        public static string Handle(PlayerCard player)
        {
            string name = HandleDisplay.Display(player.Name, player.Tag, player.PlayerId ?? "Player");
            return name.Length > 16 ? name.Substring(0, 16) + "\u2026" : name;
        }
        public static PlayerPresence Presence(GuildMember member) => member.Player.Presence == PlayerPresence.InBattle
            ? PlayerPresence.InBattle : member.Online ? PlayerPresence.Online : PlayerPresence.Offline;
        public static List<PlayerCard> Ordered(IEnumerable<PlayerCard> players, string self)
        {
            var result = new List<PlayerCard>();
            if (players != null) foreach (var player in players)
            {
                if (player == null || string.IsNullOrEmpty(player.PlayerId) || player.PlayerId == self) continue;
                if (result.Exists(p => p.PlayerId == player.PlayerId)) continue;
                result.Add(player);
            }
            result.Sort((a, b) => {
                int order = Priority(a.Presence).CompareTo(Priority(b.Presence));
                if (order == 0) order = StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
                if (order == 0) order = StringComparer.OrdinalIgnoreCase.Compare(a.Tag, b.Tag);
                return order == 0 ? StringComparer.Ordinal.Compare(a.PlayerId, b.PlayerId) : order;
            });
            return result;
        }
        private static int Priority(PlayerPresence p) => p == PlayerPresence.Online ? 0 : p == PlayerPresence.InBattle ? 1 : 2;
    }
}
