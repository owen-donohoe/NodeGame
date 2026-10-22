using System;
using System.Collections.Generic;
using NodeWar.Backend;

namespace NodeWar.Lobby
{
    public sealed class FriendPresenceOrder : IComparer<PlayerCard>
    {
        public static readonly FriendPresenceOrder Instance = new FriendPresenceOrder();

        public int Compare(PlayerCard a, PlayerCard b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int order = Priority(a.Presence).CompareTo(Priority(b.Presence));
            if (order == 0) order = StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
            if (order == 0) order = StringComparer.OrdinalIgnoreCase.Compare(a.Tag, b.Tag);
            return order == 0 ? StringComparer.Ordinal.Compare(a.PlayerId, b.PlayerId) : order;
        }

        private static int Priority(PlayerPresence presence) => presence == PlayerPresence.InBattle ? 0 :
            presence == PlayerPresence.Online ? 1 : presence == PlayerPresence.Offline ? 2 : 3;
    }
}
