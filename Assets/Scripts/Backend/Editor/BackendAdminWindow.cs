#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using UnityEditor;
using UnityEngine;

namespace NodeWar.Backend.Editor
{
    /// <summary>Play-mode operator tools; authorization is checked by the server.</summary>
    public sealed class BackendAdminWindow : EditorWindow
    {
        private string targetPlayerId = "";
        private long coins, premium;
        private string reason = "", itemIds = "", message = "";
        private bool busy, failed;

        [MenuItem("Tools/Node War/Backend/Admin")]
        private static void Open() => GetWindow<BackendAdminWindow>("Backend Admin");

        private static string SignedInPlayerId()
        {
            if (!EditorApplication.isPlaying) return null;
            // Authentication's singleton is unavailable until services initialize.
            try
            {
                var auth = AuthenticationService.Instance;
                return auth.IsSignedIn ? auth.PlayerId : null;
            }
            catch (Exception) { return null; }
        }

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            string playerId = SignedInPlayerId();
            EditorGUILayout.LabelField("Signed-in player ID", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(playerId ?? "Enter Play mode and sign in", GUILayout.Height(20));
            using (new EditorGUI.DisabledScope(playerId == null || busy))
            {
                if (GUILayout.Button("Copy")) EditorGUIUtility.systemCopyBuffer = playerId;
                targetPlayerId = EditorGUILayout.TextField("Target player ID", targetPlayerId);
                EditorGUILayout.LabelField("Blank target = me", EditorStyles.miniLabel);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Currency", EditorStyles.boldLabel);
                coins = EditorGUILayout.LongField("Coins", coins);
                premium = EditorGUILayout.LongField("Premium", premium);
                reason = EditorGUILayout.TextField("Reason", reason);
                if (GUILayout.Button("Grant currency"))
                    Grant<WalletRecord>("AdminGrantCurrency", new Dictionary<string, object>
                    {
                        ["targetPlayerId"] = targetPlayerId, ["coins"] = coins, ["premium"] = premium,
                        ["reason"] = reason, ["requestId"] = Guid.NewGuid().ToString()
                    }, wallet => $"Wallet: {wallet.Coins} coins, {wallet.Premium} premium.");

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Items", EditorStyles.boldLabel);
                itemIds = EditorGUILayout.TextField("Item IDs (comma separated)", itemIds);
                if (GUILayout.Button("Grant items")) GrantItems(false);
                if (GUILayout.Button("Grant all content")) GrantItems(true);
            }
            if (busy) EditorGUILayout.LabelField("Grant in progress...");
            if (!string.IsNullOrEmpty(message))
                EditorGUILayout.HelpBox(message, failed ? MessageType.Error : MessageType.Info);
        }

        private void GrantItems(bool allActive) => Grant<InventoryRecord>("AdminGrantItems",
            new Dictionary<string, object>
            {
                ["targetPlayerId"] = targetPlayerId, ["allActive"] = allActive,
                ["itemIds"] = allActive ? Array.Empty<string>() : itemIds.Split(',')
                    .Select(id => id.Trim()).Where(id => id.Length > 0).ToArray()
            }, inventory => $"Inventory: {inventory.OwnedVariants.Count} variants, {inventory.OwnedSkins.Count} skins.");

        private async void Grant<T>(string endpoint, Dictionary<string, object> args, Func<T, string> describe)
        {
            string caller = SignedInPlayerId();
            if (busy || caller == null) return;
            string target = (string)args["targetPlayerId"];
            busy = true;
            failed = false;
            message = "";
            bool granted = false;
            try
            {
                T result = await CloudCodeService.Instance.CallModuleEndpointAsync<T>(BackendServices.CloudModule, endpoint, args);
                granted = true;
                message = describe(result);
                if ((string.IsNullOrEmpty(target) || target == caller) && SignedInPlayerId() == caller)
                    await BackendServices.PlayerState.GetAsync();
            }
            catch (Exception ex)
            {
                failed = true;
                message = (granted ? "Grant succeeded, but refreshing the result failed: " : "") + ex.Message;
            }
            finally
            {
                busy = false;
                if (this != null) Repaint();
            }
        }
    }
}
#endif
