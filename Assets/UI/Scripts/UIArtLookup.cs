using System;

namespace NodeWar.Lobby
{
    /// <summary>Theme sprite precedence, independent of Unity asset types.</summary>
    public static class UIArtLookup
    {
        // -1 means use generated art. An empty first match still owns the kind.
        public static int SpriteIndex<T>(T[] entries, int kind, Func<T, int> kindOf, Func<T, bool> hasSprite)
        {
            if (entries == null) return -1;
            for (int i = 0; i < entries.Length; i++)
                if (kindOf(entries[i]) == kind) return hasSprite(entries[i]) ? i : -1;
            return -1;
        }
    }
}
