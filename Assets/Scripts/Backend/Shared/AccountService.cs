using System;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    public enum AccountStatus
    {
        /// <summary>Not signed in at all. Only between a sign-out and the next sign-in.</summary>
        SignedOut,

        /// <summary>Anonymous. Progress lives on this device only and dies with a reinstall.</summary>
        Guest,

        /// <summary>Linked to a Unity Player Account. The same player on any device.</summary>
        Linked
    }

    public sealed class AccountInfo
    {
        public AccountStatus Status;

        /// <summary>The UGS Player ID. Null when signed out.</summary>
        public string PlayerId;

        /// <summary>The UGS player name, or null if none has been fetched.</summary>
        public string DisplayName;
    }

    public enum LinkResult
    {
        Linked,

        /// <summary>The player closed or cancelled the Unity sign-in.</summary>
        Cancelled,

        /// <summary>
        /// That Unity account already belongs to another player. The caller
        /// offers <see cref="IAccountService.SwitchToLinkedAccountAsync"/> or
        /// cancel. The two players are never merged.
        /// </summary>
        AlreadyLinkedElsewhere,

        Failed
    }

    /// <summary>
    /// Who the player is. Every method can fail or be refused, and the UI
    /// shows <see cref="Current"/> after the call rather than assuming the
    /// outcome.
    /// </summary>
    public interface IAccountService
    {
        AccountInfo Current { get; }

        /// <summary>Raised on the calling context whenever <see cref="Current"/> changes.</summary>
        event Action<AccountInfo> Changed;

        /// <summary>Signs in as whoever this device last was (a new guest the first time).</summary>
        Task<AccountInfo> EnsureSignedInAsync();

        /// <summary>
        /// Guest only: signs in to a Unity Player Account and links it to this
        /// player, so the current progress follows the account.
        /// </summary>
        Task<LinkResult> LinkAsync();

        /// <summary>
        /// After <see cref="LinkResult.AlreadyLinkedElsewhere"/>: become the
        /// player that Unity account belongs to. This device's guest progress
        /// is abandoned.
        /// </summary>
        Task<AccountInfo> SwitchToLinkedAccountAsync();

        /// <summary>
        /// Signs in with a Unity Player Account from a guest or signed-out
        /// state. This is how a player gets back to an existing account on a new
        /// device. From a guest with progress, the UI warns first: that guest is
        /// abandoned.
        /// </summary>
        Task<LinkResult> SignInWithAccountAsync();

        /// <summary>
        /// Signs out and forgets this device's cached sign-in, so the next launch
        /// does not silently come back as the same player.
        /// </summary>
        Task SignOutAsync();
    }

    /// <summary>
    /// Offline stand-in. Starts as a guest; <see cref="SimulateConflict"/> makes
    /// the next link report the Unity account as already taken, so the conflict
    /// dialog can be exercised without a second real account.
    /// </summary>
    public sealed class LocalAccountService : IAccountService
    {
        public const string LocalDisplayName = "LocalPlayer";

        private const string GuestId = "local-guest";
        private const string OtherPlayerId = "local-linked-elsewhere";

        public bool SimulateConflict;

        public AccountInfo Current { get; private set; } = new AccountInfo { Status = AccountStatus.SignedOut };

        public event Action<AccountInfo> Changed;

        public Task<AccountInfo> EnsureSignedInAsync()
        {
            if (Current.Status == AccountStatus.SignedOut)
                Set(AccountStatus.Guest, GuestId, null);
            return Task.FromResult(Current);
        }

        public Task<LinkResult> LinkAsync()
        {
            if (Current.Status != AccountStatus.Guest)
                return Task.FromResult(LinkResult.Failed);
            if (SimulateConflict)
                return Task.FromResult(LinkResult.AlreadyLinkedElsewhere);
            Set(AccountStatus.Linked, Current.PlayerId, LocalDisplayName);
            return Task.FromResult(LinkResult.Linked);
        }

        public Task<AccountInfo> SwitchToLinkedAccountAsync()
        {
            Set(AccountStatus.Linked, OtherPlayerId, LocalDisplayName);
            return Task.FromResult(Current);
        }

        public Task<LinkResult> SignInWithAccountAsync()
        {
            if (Current.Status == AccountStatus.Linked)
                return Task.FromResult(LinkResult.Failed);
            Set(AccountStatus.Linked, OtherPlayerId, LocalDisplayName);
            return Task.FromResult(LinkResult.Linked);
        }

        public Task SignOutAsync()
        {
            Set(AccountStatus.SignedOut, null, null);
            return Task.CompletedTask;
        }

        private void Set(AccountStatus status, string playerId, string displayName)
        {
            Current = new AccountInfo { Status = status, PlayerId = playerId, DisplayName = displayName };
            Changed?.Invoke(Current);
        }
    }

    /// <summary>
    /// When to ask a guest to link their account, once: as soon as they have
    /// progress a reinstall would lose. Not before, when there is nothing to
    /// protect and the ask is noise.
    /// </summary>
    public static class LinkPromptPolicy
    {
        public static bool ShouldPrompt(AccountInfo account, PlayerState state, bool alreadyShown)
        {
            if (alreadyShown || account == null || account.Status != AccountStatus.Guest || state == null)
                return false;
            return HasProgress(state);
        }

        /// <summary>
        /// A ranked result or an unlock: anything the server would lose with this
        /// guest. Every new player is granted the era-0 variants and default
        /// skins, so those are not progress; only something earned beyond them is.
        /// </summary>
        public static bool HasProgress(PlayerState state)
        {
            bool played = state.History?.MatchIds != null && state.History.MatchIds.Count > 0;
            return played || HasEarnedItem(state.Inventory);
        }

        private static bool HasEarnedItem(InventoryRecord inventory)
        {
            if (inventory == null) return false;
            if (inventory.OwnedVariants != null)
                foreach (string id in inventory.OwnedVariants)
                    if (!CatalogIds.TryParseVariant(id, out _, out int era) || era > 0) return true;
            if (inventory.OwnedSkins != null)
                foreach (string id in inventory.OwnedSkins)
                    if (!CatalogIds.TryParseSkin(id, out string baseId) || id != CatalogIds.DefaultSkin(baseId)) return true;
            return false;
        }
    }
}
