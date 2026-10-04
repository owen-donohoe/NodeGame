using System.Threading.Tasks;
using UnityEngine;

using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Authentication;

namespace NodeWar.Backend
{
    /// <summary>
    /// The one place Unity Gaming Services are initialised and the player is
    /// signed in. Relay, Cloud Code and Cloud Save all refuse calls until both
    /// have happened, so every caller awaits EnsureReadyAsync first rather than
    /// repeating the pair.
    /// </summary>
    public static class GameServices
    {
        public const string DevelopmentEnvironment = "development";
        public const string ProductionEnvironment = "production";

        private static Task ready;

        // Reset like the project's other statics, so turning domain reload off
        // never lets a Task from the last play session skip sign-in for this one.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            ready = null;
        }

        /// <summary>
        /// The environment a build talks to, or null in the Editor. The Editor
        /// leaves it unset so Project Settings > Services > Environments decides;
        /// a build has no such panel, so the build type decides instead. A
        /// development build must never write player data into production.
        /// </summary>
        public static string BuildEnvironment
        {
            get
            {
#if UNITY_EDITOR
                return null;
#elif DEVELOPMENT_BUILD
                return DevelopmentEnvironment;
#else
                return ProductionEnvironment;
#endif
            }
        }

        /// <summary>
        /// Safe to call from anywhere, any number of times. Concurrent callers
        /// share one attempt; a failed attempt is retried by the next caller,
        /// and so is a finished one after the player has signed out.
        /// </summary>
        public static Task EnsureReadyAsync()
        {
            bool signedOutSince = ready != null && ready.Status == TaskStatus.RanToCompletion
                && !AuthenticationService.Instance.IsSignedIn;
            if (ready == null || ready.IsFaulted || ready.IsCanceled || signedOutSince)
                ready = InitializeAndSignInAsync();
            return ready;
        }

        /// <summary>
        /// Initialises UGS without signing anyone in. For the one caller that must
        /// not create a guest: signing in to an existing account.
        /// </summary>
        public static async Task InitializeAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
                string environment = BuildEnvironment;
                if (environment != null)
                    options.SetEnvironmentName(environment);

                await UnityServices.InitializeAsync(options);
                Debug.Log("[GameServices] Initialised, environment: "
                    + (environment ?? "Editor setting"));
            }
        }

        private static async Task InitializeAndSignInAsync()
        {
            await InitializeAsync();

            // With a cached session token this resumes whoever the device last
            // was, linked or not; only a device with none gets a new guest.
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }
}
