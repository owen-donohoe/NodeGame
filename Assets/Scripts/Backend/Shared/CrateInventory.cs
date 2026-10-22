using System;
using System.Collections.Generic;

namespace NodeWar.Backend
{
    /// <summary>The two crate sizes a player can hold on Home.</summary>
    public enum CrateSize
    {
        Small,
        Medium
    }

    public enum CrateRewardKind
    {
        Coins,
        Premium,
        District,
        Suit,
        Skin
    }

    /// <summary>
    /// One thing a crate gives. Id is a catalog or lobby ID for District, Suit and Skin and is
    /// empty for currencies; Amount is the currency amount, or 1 for an item.
    /// </summary>
    public readonly struct CrateReward
    {
        public readonly CrateRewardKind Kind;
        public readonly string Id;
        public readonly int Amount;

        public CrateReward(CrateRewardKind kind, string id, int amount)
        {
            Kind = kind;
            Id = id ?? string.Empty;
            Amount = amount;
        }
    }

    /// <summary>
    /// Crates the player has earned but not opened, and progress toward the next one.
    /// There is no server crate record yet: the only implementation is a local stub, so
    /// nothing here is authoritative. The server version will roll rewards itself.
    /// </summary>
    public interface ICrateInventory
    {
        int Count(CrateSize size);

        /// <summary>Progress toward the next earned crate, 0 to 1.</summary>
        float NextProgress { get; }

        CrateSize NextSize { get; }

        event Action Changed;

        /// <summary>Removes one crate and returns its rewards, or false if none is held.</summary>
        bool TryOpen(CrateSize size, out IReadOnlyList<CrateReward> rewards);
    }
}
