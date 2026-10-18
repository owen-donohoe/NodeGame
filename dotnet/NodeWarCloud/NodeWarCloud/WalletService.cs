using System;
using System.Threading.Tasks;
using NodeWar.Backend;

namespace NodeWar.Cloud
{
    /// <summary>A transaction the ledger refused: it would take a balance below zero.</summary>
    public sealed class WalletRefusedException : Exception
    {
        public WalletRefusedException(string message) : base(message) { }
    }

    /// <summary>
    /// Applies one transaction to one player's wallet under its write lock.
    /// Callers that also change another of the player's records in the same
    /// purchase must write both in one batch instead (none do yet).
    /// </summary>
    public sealed class WalletService
    {
        // Matches the other locked read-modify-write loops in this module.
        private const int RetryLimit = 3;

        private readonly IWalletStore store;
        private readonly Func<long> nowUnixSeconds;

        public WalletService(IWalletStore store, Func<long> nowUnixSeconds)
        {
            this.store = store;
            this.nowUnixSeconds = nowUnixSeconds;
        }

        /// <summary>
        /// Returns the wallet after the transaction. A repeated request ID returns the
        /// current wallet without applying twice.
        /// </summary>
        public async Task<WalletRecord> ApplyAsync(string requestId, string reason, long coins, long premium)
        {
            for (int attempt = 0; ; attempt++)
            {
                var (wallet, writeLock) = await store.ReadWalletLockedAsync();
                if (wallet == null)
                    throw new InvalidOperationException("The player has no wallet yet; they must sign in once first.");
                var transaction = new WalletTransaction
                {
                    Id = requestId, Reason = reason, Coins = coins, Premium = premium, AtUnixSeconds = nowUnixSeconds()
                };
                switch (WalletLedger.Apply(wallet, transaction))
                {
                    case WalletApplyResult.Duplicate: return wallet;
                    case WalletApplyResult.Insufficient: throw new WalletRefusedException("Not enough currency.");
                }
                try
                {
                    await store.WriteWalletLockedAsync(wallet, writeLock);
                    return wallet;
                }
                catch (RecordConflictException) when (attempt + 1 < RetryLimit) { }
            }
        }
    }
}
