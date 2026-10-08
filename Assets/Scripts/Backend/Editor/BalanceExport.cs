#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using NodeWar.Config;
using NodeWar.Simulation;
using UnityEditor;
using UnityEngine;

namespace NodeWar.Backend.Editor
{
    public static class BalanceExport
    {
        [MenuItem("Tools/Node War/Backend/Export Balance For Server")]
        public static void Export()
        {
            GameBalance balance = GameBalance.LoadShared();
            if (balance == null)
            {
                Debug.LogError("Cannot export: shared GameBalance asset is missing.");
                return;
            }

            if (!BalanceExportData.ReleaseValid(balance.Data, out string reason))
            {
                Debug.LogError("Cannot export core-rules release balance: " + reason);
                return;
            }
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../dotnet/NodeWarCloud/NodeWarCloud/Balances"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory,
                BalanceHasher.Hash(balance.Data).ToString(CultureInfo.InvariantCulture) + ".json");
            string json = BalanceExportData.Serialize(balance.Data);
            if (File.Exists(path) && File.ReadAllText(path) != json)
            {
                Debug.LogError("Cannot overwrite an existing content-addressed balance with different Data: " + path);
                return;
            }
            File.WriteAllText(path, json);
            Debug.Log("Exported server balance: " + path);
        }
    }
}
#endif
