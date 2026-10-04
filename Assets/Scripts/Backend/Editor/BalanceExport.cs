#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
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

            string directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../dotnet/NodeWarCloud/NodeWarCloud/Balances"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory,
                BalanceHasher.Hash(balance.Data).ToString(CultureInfo.InvariantCulture) + ".json");
            // Opt-out serialization includes public fields; no StringEnumConverter:
            // enums stay integers. Use a fresh serializer, ignoring global defaults.
            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver(),
                Formatting = Formatting.Indented,
                Culture = CultureInfo.InvariantCulture,
                TypeNameHandling = TypeNameHandling.None
            });
            using (var writer = new StreamWriter(path))
            using (var json = new JsonTextWriter(writer))
                serializer.Serialize(json, balance.Data);

            Debug.Log("Exported server balance: " + path);
        }
    }
}
#endif
