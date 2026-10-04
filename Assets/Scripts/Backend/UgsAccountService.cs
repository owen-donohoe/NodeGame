using System;
using System.Threading.Tasks;
using UnityEngine;

using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;

namespace NodeWar.Backend
{
    /// <summary>
    /// Accounts through UGS Authentication, with Unity Player Accounts (UPA) as
    /// the linkable identity. One UGS Player ID per player: a guest links UPA to
    /// keep their progress, and signing in to UPA on another device reaches the
    /// same Player ID and so the same Cloud Save data. Steam will link to that
    /// same Player ID later.
    ///
    /// UPA sign-in happens in a browser. StartSignInAsync only opens it; the
    /// outcome arrives through PlayerAccountService's events, and a player who
    /// just closes the browser produces no event at all, so the wait is bounded.
    /// </summary>
    public sealed class UgsAccountService : IAccountService
    {
        private static readonly TimeSpan BrowserSignInTimeout = TimeSpan.FromMinutes(5);

        public AccountInfo Current { get; private set; } = new AccountInfo { Status = AccountStatus.SignedOut };

        public event Action<AccountInfo> Changed;

        public async Task<AccountInfo> EnsureSignedInAsync()
        {
            await GameServices.EnsureReadyAsync();
            return await RefreshAsync();
        }

        public async Task<LinkResult> LinkAsync()
        {
            await EnsureSignedInAsync();
            if (Current.Status != AccountStatus.Guest)
                return LinkResult.Failed;

            LinkResult browser = await SignInToPlayerAccountAsync();
            if (browser != LinkResult.Linked)
                return browser;

            try
            {
                await AuthenticationService.Instance.LinkWithUnityAsync(PlayerAccountService.Instance.AccessToken);
            }
            catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                // Keep the UPA session: SwitchToLinkedAccountAsync needs its token.
                return LinkResult.AlreadyLinkedElsewhere;
            }
            catch (RequestFailedException e)
            {
                Debug.LogWarning("[Account] Link failed: " + e.Message);
                return LinkResult.Failed;
            }

            await RefreshAsync();
            return LinkResult.Linked;
        }

        public async Task<AccountInfo> SwitchToLinkedAccountAsync()
        {
            if (!PlayerAccountService.Instance.IsSignedIn)
                throw new InvalidOperationException("No Unity Player Account session to switch to.");

            await SignInWithPlayerAccountTokenAsync();
            return Current;
        }

        public async Task<LinkResult> SignInWithAccountAsync()
        {
            await GameServices.InitializeAsync();
            if (Current.Status == AccountStatus.Linked)
                return LinkResult.Failed;

            LinkResult browser = await SignInToPlayerAccountAsync();
            if (browser != LinkResult.Linked)
                return browser;

            try
            {
                await SignInWithPlayerAccountTokenAsync();
                return LinkResult.Linked;
            }
            catch (RequestFailedException e)
            {
                Debug.LogWarning("[Account] Sign-in failed: " + e.Message);
                await RefreshAsync();
                return LinkResult.Failed;
            }
        }

        public Task SignOutAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                // clearCredentials: without it the cached session token signs the
                // same player straight back in on the next launch.
                AuthenticationService.Instance.SignOut(clearCredentials: true);
                if (PlayerAccountService.Instance.IsSignedIn)
                    PlayerAccountService.Instance.SignOut();
            }
            Set(new AccountInfo { Status = AccountStatus.SignedOut });
            return Task.CompletedTask;
        }

        /// <summary>
        /// Replaces whoever is signed in with the player the held UPA token
        /// belongs to. The previous player's session token is cleared, which for
        /// a guest means that guest is gone for good.
        /// </summary>
        private async Task SignInWithPlayerAccountTokenAsync()
        {
            if (AuthenticationService.Instance.IsSignedIn)
                AuthenticationService.Instance.SignOut(clearCredentials: true);
            await AuthenticationService.Instance.SignInWithUnityAsync(PlayerAccountService.Instance.AccessToken);
            await RefreshAsync();
        }

        private static async Task<LinkResult> SignInToPlayerAccountAsync()
        {
            IPlayerAccountService upa = PlayerAccountService.Instance;
            if (upa.IsSignedIn)
                return LinkResult.Linked;

            var outcome = new TaskCompletionSource<LinkResult>();
            void OnSignedIn() => outcome.TrySetResult(LinkResult.Linked);
            void OnFailed(RequestFailedException e)
            {
                Debug.LogWarning("[Account] Unity sign-in failed: " + e.Message);
                outcome.TrySetResult(LinkResult.Failed);
            }

            upa.SignedIn += OnSignedIn;
            upa.SignInFailed += OnFailed;
            try
            {
                await upa.StartSignInAsync();
                Task finished = await Task.WhenAny(outcome.Task, Task.Delay(BrowserSignInTimeout));
                return finished == outcome.Task ? outcome.Task.Result : LinkResult.Cancelled;
            }
            catch (RequestFailedException e)
            {
                Debug.LogWarning("[Account] Could not start Unity sign-in: " + e.Message);
                return LinkResult.Failed;
            }
            finally
            {
                upa.SignedIn -= OnSignedIn;
                upa.SignInFailed -= OnFailed;
            }
        }

        private async Task<AccountInfo> RefreshAsync()
        {
            IAuthenticationService auth = AuthenticationService.Instance;
            if (!auth.IsSignedIn)
            {
                Set(new AccountInfo { Status = AccountStatus.SignedOut });
                return Current;
            }

            PlayerInfo info = await auth.GetPlayerInfoAsync();
            string name = await auth.GetPlayerNameAsync();
            Set(new AccountInfo
            {
                Status = info.GetUnityId() != null ? AccountStatus.Linked : AccountStatus.Guest,
                PlayerId = auth.PlayerId,
                DisplayName = name
            });
            return Current;
        }

        private void Set(AccountInfo info)
        {
            Current = info;
            Changed?.Invoke(info);
        }
    }
}
