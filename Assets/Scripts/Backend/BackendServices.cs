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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            playerState = null;
            account = null;
            inventory = null;
        }

        public static IPlayerStateService PlayerState
        {
            get
            {
                if (playerState == null)
                {
                    if (UseLocalFakes) CreateLocalServices();
                    else playerState = new UgsPlayerStateService();
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
                    else inventory = new UgsInventoryService();
                }
                return inventory;
            }
        }

        private static void CreateLocalServices()
        {
            var store = new InMemoryPlayerRecordStore();
            var localInventory = new LocalInventoryService(store, CatalogBases.All());
            inventory = localInventory;
            playerState = new LocalPlayerStateService(store, localInventory.GrantDefaults);
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
