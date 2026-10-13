using NodeWar.Backend;

namespace NodeWar.Lobby
{
    public enum UnlockStatus { Owned, CanUnlock, CannotUnlock }

    public readonly struct UnlockState
    {
        public UnlockStatus Status { get; }
        public int UnlockEra { get; }
        public bool CanUse { get; }

        public UnlockState(UnlockStatus status, int unlockEra, bool canUse)
        {
            Status = status;
            UnlockEra = unlockEra;
            CanUse = canUse;
        }

        // Presentation overrides never grant equipment eligibility.
        public UnlockState WithDisplayStatus(UnlockStatus status) => new UnlockState(status, UnlockEra, CanUse);
    }

    /// <summary>Profile base ownership and exact server inventory are independent.</summary>
    public static class UnlockStates
    {
        private static UnlockState Invalid => new UnlockState(UnlockStatus.CannotUnlock, 0, false);
        private static int Arena(PlayerState state) => state?.Rank?.Arena ?? 1;
        private static int HighestArena(PlayerState state) => state?.Rank?.HighestArena ?? 1;

        public static UnlockState ForBase(string lobbyId, bool profileOwned, PlayerState state)
        {
            string baseId = LoadoutTypes.CatalogBaseForLobbyId(lobbyId);
            if (baseId == null) return Invalid;
            int gate = SuitTree.Root(baseId)?.Arena ?? 0;
            return new UnlockState(profileOwned ? UnlockStatus.Owned :
                HighestArena(state) >= gate ? UnlockStatus.CanUnlock : UnlockStatus.CannotUnlock,
                gate, profileOwned && Arena(state) >= gate);
        }

        public static UnlockState ForVariant(string baseId, string variantId, PlayerState state)
        {
            if (!CatalogIds.TryParseVariant(variantId, out string parsedBase, out int index) || parsedBase != baseId)
                return Invalid;
            var owned = state?.Inventory?.OwnedVariants;
            bool isOwned = owned?.Contains(variantId) == true;
            int gate = index;
            bool canUnlock;
            bool available;
            if (SuitTree.IsTreeSuit(baseId))
            {
                if (!SuitTree.TryGet(variantId, out var node)) return Invalid;
                gate = node.Arena;
                canUnlock = SuitTree.CanOwn(node, HighestArena(state), owned ?? new System.Collections.Generic.List<string>());
                available = SuitTree.IsAvailable(node, Arena(state));
            }
            else
            {
                canUnlock = HighestArena(state) >= gate;
                available = Arena(state) >= gate;
            }
            return new UnlockState(isOwned ? UnlockStatus.Owned : canUnlock ? UnlockStatus.CanUnlock : UnlockStatus.CannotUnlock,
                gate, isOwned && available);
        }

        public static UnlockState ForSkin(string baseId, string skinId, PlayerState state)
        {
            if (!CatalogIds.TryParseSkin(skinId, out string parsedBase) || parsedBase != baseId) return Invalid;
            bool owned = state?.Inventory?.OwnedSkins?.Contains(skinId) == true;
            return new UnlockState(owned ? UnlockStatus.Owned : UnlockStatus.CanUnlock, 0, owned);
        }
    }
}
