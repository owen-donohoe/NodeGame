using System;

namespace NodeWar.Lobby
{
    /// <summary>Theme sprite precedence, independent of Unity asset types.</summary>
    public static class UIArtLookup
    {
        // -1 means generated art. First wins per pair; empty exact art can fall back to Anywhere.
        public static int SpriteIndex<T>(T[] entries, int kind, LobbyIconContext context,
            Func<T, int> kindOf, Func<T, LobbyIconContext> contextOf, Func<T, bool> hasSprite)
        {
            int exact = ForPair(entries, kind, context, kindOf, contextOf, hasSprite);
            if (exact >= 0 || context == LobbyIconContext.Anywhere) return exact;
            return ForPair(entries, kind, LobbyIconContext.Anywhere, kindOf, contextOf, hasSprite);
        }

        private static int ForPair<T>(T[] entries, int kind, LobbyIconContext context,
            Func<T, int> kindOf, Func<T, LobbyIconContext> contextOf, Func<T, bool> hasSprite)
        {
            if (entries == null) return -1;
            for (int i = 0; i < entries.Length; i++)
                if (kindOf(entries[i]) == kind && contextOf(entries[i]) == context)
                    return hasSprite(entries[i]) ? i : -1;
            return -1;
        }
    }
}
