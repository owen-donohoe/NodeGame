using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NodeWar.Backend
{
    /// <summary>
    /// The one place a caller gets a backend service. Each service has a UGS
    /// implementation and a local fake; builds always use UGS, and the Editor
    /// can switch to the fakes (Tools > Node War > Backend) to work offline.
    /// </summary>
    public static class BackendServices
    {
        /// <summary>The Cloud Code module name: dotnet/NodeWarCloud's solution name.</summary>
        public const string CloudModule = "NodeWarCloud";

        private static IPlayerStateService playerState;
        private static IAccountService account;
        private static IInventoryService inventory;

        /// <summary>
        /// The last player state any backend call returned this session, or null
        /// before the first. For what must be read synchronously, such as the
        /// equipped eras a match launches with; anything that can wait should
        /// ask the service. Only returned while the player it was fetched for is
        /// still the one signed in, so it never describes someone else.
        /// </summary>
        public static PlayerState LastKnownState
        {
            get
            {
                string current = account?.Current?.PlayerId;
                return current != null && current == lastKnownFor ? lastKnown : null;
            }
        }

        private static PlayerState lastKnown;
        private static string lastKnownFor;

        /// <summary>Called with every player state a service returns.</summary>
        internal static void Remember(PlayerState state)
        {
            if (state == null) return;
            lastKnown = state;
            lastKnownFor = Account.Current?.PlayerId;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            playerState = null;
            account = null;
            inventory = null;
            lastKnown = null;
            lastKnownFor = null;
        }

        public static IPlayerStateService PlayerState
        {
            get
            {
                if (playerState == null)
                {
                    if (UseLocalFakes) CreateLocalServices();
                    else playerState = new RememberingPlayerStateService(new UgsPlayerStateService());
                }
                return playerState;
            }
        }

        public static IInventoryService Inventory
        {
            get
            {
                if (inventory == null)
                {
                    if (UseLocalFakes) CreateLocalServices();
                    else inventory = new RememberingInventoryService(new UgsInventoryService());
                }
                return inventory;
            }
        }

        private static void CreateLocalServices()
        {
            var store = new InMemoryPlayerRecordStore();
            var localInventory = new LocalInventoryService(store, CatalogBases.All());
            inventory = new RememberingInventoryService(localInventory);
            playerState = new RememberingPlayerStateService(new LocalPlayerStateService(store, localInventory.GrantDefaults));
        }

        public static IAccountService Account
        {
            get
            {
                if (account == null)
                    account = UseLocalFakes
                        ? (IAccountService)new LocalAccountService()
                        : new UgsAccountService();
                return account;
            }
        }

#if UNITY_EDITOR
        private const string LocalFakesPref = "NodeWar.Backend.UseLocalFakes";
        private const string LocalFakesMenu = "Tools/Node War/Backend/Use Local Fakes";

        /// <summary>Editor only. Takes effect from the next Play session.</summary>
        public static bool UseLocalFakes => EditorPrefs.GetBool(LocalFakesPref, false);

        [MenuItem(LocalFakesMenu)]
        private static void ToggleLocalFakes()
        {
            EditorPrefs.SetBool(LocalFakesPref, !UseLocalFakes);
        }

        [MenuItem(LocalFakesMenu, true)]
        private static bool ToggleLocalFakesValidate()
        {
            Menu.SetChecked(LocalFakesMenu, UseLocalFakes);
            return true;
        }
#else
        public static bool UseLocalFakes => false;
#endif
    }
}

namespace NodeWar.Backend
{
    /// <summary>Passes calls through and remembers what came back (BackendServices.LastKnownState).</summary>
    internal sealed class RememberingPlayerStateService : IPlayerStateService
    {
        private readonly IPlayerStateService inner;
        public RememberingPlayerStateService(IPlayerStateService inner) { this.inner = inner; }

        public async System.Threading.Tasks.Task<PlayerState> GetAsync()
        {
            PlayerState state = await inner.GetAsync();
            BackendServices.Remember(state);
            return state;
        }
    }

    internal sealed class RememberingInventoryService : IInventoryService
    {
        private readonly IInventoryService inner;
        public RememberingInventoryService(IInventoryService inner) { this.inner = inner; }

        public async System.Threading.Tasks.Task<PlayerState> EquipAsync(EquippedRecord changes)
        {
            PlayerState state = await inner.EquipAsync(changes);
            BackendServices.Remember(state);
            return state;
        }
    }
}
