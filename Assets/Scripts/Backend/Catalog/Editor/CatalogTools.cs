#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace NodeWar.Backend.EditorTools
{
    // Uses Unity's predefined Assembly-CSharp-Editor: an asmdef here could not
    // reference CatalogDefinition in the predefined runtime assembly.
    public static class CatalogTools
    {
        private const string ExportPath = "dotnet/NodeWarCloud/NodeWarCloud/Catalog/catalog.json";

        [MenuItem("Tools/Node War/Backend/Generate Catalog")]
        public static void Generate()
        {
            var definition = FindDefinition();
            if (definition == null) return;
            Undo.RecordObject(definition, "Generate Catalog");
            if (definition.entries == null) definition.entries = new List<CatalogEntry>();
            var ids = new HashSet<string>(definition.entries.Where(e => e != null).Select(e => e.id), StringComparer.Ordinal);
            foreach (string baseId in CatalogBases.All())
            {
                for (int era = 0; era < CatalogIds.EraCount; era++)
                    Add(definition, ids, CatalogIds.Variant(baseId, era), CatalogEntryKind.Variant, baseId, era);
                Add(definition, ids, CatalogIds.DefaultSkin(baseId), CatalogEntryKind.Skin, baseId, -1);
            }
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            Selection.activeObject = definition;
        }

        private static void Add(CatalogDefinition definition, HashSet<string> ids,
            string id, CatalogEntryKind kind, string baseId, int era)
        {
            if (ids.Add(id)) definition.entries.Add(new CatalogEntry { id = id, kind = kind, baseId = baseId, era = era });
        }

        [MenuItem("Tools/Node War/Backend/Export Catalog For Server")]
        public static void Export()
        {
            var definition = FindDefinition();
            if (definition == null) return;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ExportPath));
            try
            {
                var candidate = definition.entries?.Select(e => e == null ? null : new ExportEntry
                { Id = e.id, Kind = e.kind.ToString(), BaseId = e.baseId, Era = e.era, Retired = e.retired }).ToList();
                var previous = File.Exists(path)
                    ? JsonConvert.DeserializeObject<List<ExportEntry>>(File.ReadAllText(path))
                        ?? throw new InvalidDataException("Previous catalog must be a JSON array.")
                    : new List<ExportEntry>();
                var errors = Validate(candidate, previous);
                if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonConvert.SerializeObject(candidate, Formatting.Indented) + "\n");
                EditorUtility.DisplayDialog("Catalog exported", ExportPath +
                    "\nRun dotnet test dotnet/NodeWar.sln for authoritative catalog validation before committing.", "OK");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Catalog export refused", exception.Message, "OK");
            }
        }

        private static CatalogDefinition FindDefinition()
        {
            if (Selection.activeObject is CatalogDefinition selected) return selected;
            string[] guids = AssetDatabase.FindAssets("t:CatalogDefinition");
            if (guids.Length == 1)
                return AssetDatabase.LoadAssetAtPath<CatalogDefinition>(AssetDatabase.GUIDToAssetPath(guids[0]));
            EditorUtility.DisplayDialog("Select a catalog", guids.Length == 0
                ? "Create a catalog using Assets > Create > Node War > Backend > Catalog, then select it."
                : "More than one catalog exists. Select the catalog to edit or export.", "OK");
            return null;
        }

        private sealed class ExportEntry
        {
            public string Id;
            public string Kind;
            public string BaseId;
            public int Era;
            public bool Retired;
        }

        // Editor-side checks mirror the current shape and identity rules, but Unity
        // cannot reference Progression. The dotnet CatalogTests harness is authoritative:
        // it calls CatalogValidation.Validate AND ValidateAgainstPrevious on real data.
        private static List<string> Validate(List<ExportEntry> candidate, List<ExportEntry> previous)
        {
            var errors = new List<string>();
            if (candidate == null) { errors.Add("Catalog entries must not be null."); return errors; }
            var byId = new Dictionary<string, ExportEntry>(StringComparer.Ordinal);
            var variants = new HashSet<(string, int)>();
            foreach (var item in candidate)
            {
                if (item == null) { errors.Add("Catalog items must not be null."); continue; }
                if (string.IsNullOrWhiteSpace(item.Id) || !Regex.IsMatch(item.Id, @"\A[a-z0-9_]+(\.[a-z0-9_]+)*\z"))
                    errors.Add($"Invalid item ID '{item.Id}'.");
                if (item.Id != null && !byId.TryAdd(item.Id, item)) errors.Add($"Duplicate item ID '{item.Id}'.");
                if (string.IsNullOrWhiteSpace(item.BaseId)) errors.Add($"Item '{item.Id}' needs a BaseId.");
                if (item.Kind == "Variant")
                {
                    if (item.Era < 0 || item.Era >= CatalogIds.EraCount) errors.Add($"Variant '{item.Id}' has an out-of-range era.");
                    if (!item.Retired && !variants.Add((item.BaseId, item.Era)))
                        errors.Add($"Duplicate active variant for '{item.BaseId}', era {item.Era}.");
                }
                else if (item.Kind == "Skin")
                {
                    if (item.Era != -1) errors.Add($"Skin '{item.Id}' must have era -1.");
                }
                else errors.Add($"Unknown kind on '{item.Id}'.");
            }
            var previousIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in previous)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Id))
                { errors.Add("Previous catalog contains an item without an ID."); continue; }
                if (!previousIds.Add(item.Id)) errors.Add($"Previous catalog has duplicate ID '{item.Id}'.");
                if (!byId.TryGetValue(item.Id, out var next)) errors.Add($"Item '{item.Id}' must not be removed or renamed.");
                else if (next.Kind != item.Kind || next.BaseId != item.BaseId || next.Era != item.Era)
                    errors.Add($"Item ID '{item.Id}' must not be reused with a different Kind, BaseId or Era.");
            }
            return errors;
        }
    }
}
#endif
