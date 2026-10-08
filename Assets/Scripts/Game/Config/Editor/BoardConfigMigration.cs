using System.Text;
using UnityEditor;
using UnityEngine;
using NodeWar.Config;
using NodeWar.Simulation;

namespace NodeWar.EditorTools
{
    /// <summary>
    /// Re-saves every <see cref="BoardConfig"/> asset as a reference to a shipped map by
    /// ID, and clears what the asset held from before maps: the serialized 4x7 grid
    /// (<c>data</c>) and the two base draft lists. The game no longer reads any of it -
    /// the board and the pools come from <see cref="PremadeMaps"/> - so this is the
    /// asset catching up with the code, not a behaviour change.
    ///
    /// Through Unity's own serialisation rather than by hand, for the reason
    /// DistrictVisualSetup gives: a ScriptableObject asset is Unity-serialised, and
    /// hand-written YAML is how a project gets quietly corrupted. The asset keeps its
    /// GUID; only values change. Presentation fields (node scale, camera bounds, draft
    /// timing) and the bot loadout are untouched. Safe to run again: a migrated asset
    /// reports "already migrated" and nothing moves.
    ///
    /// <see cref="Migrate"/> returns its report as text and raises no dialog, so it can be
    /// run by remote evaluation as well as from the menu.
    /// </summary>
    public static class BoardConfigMigration
    {
        public const string MenuPath = "Tools/Node War/Migrate BoardConfig To Map ID";

        [MenuItem(MenuPath)]
        public static void MigrateFromMenu()
        {
            Debug.Log(Migrate());
        }

        /// <summary>Migrates every BoardConfig in the project to <see cref="PremadeMaps.Hourglass01Id"/>.</summary>
        public static string Migrate()
        {
            return Migrate(PremadeMaps.Hourglass01Id);
        }

        public static string Migrate(string mapId)
        {
            var report = new StringBuilder();
            if (!PremadeMaps.TryGet(mapId, out _))
                return "Unknown map '" + mapId + "'. Nothing changed.";

            string[] guids = AssetDatabase.FindAssets("t:BoardConfig");
            if (guids.Length == 0) return "No BoardConfig assets found.";

            int changed = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                BoardConfig config = AssetDatabase.LoadAssetAtPath<BoardConfig>(path);
                if (config == null) continue;

                var so = new SerializedObject(config);
                bool modified = false;

                SerializedProperty map = so.FindProperty("mapId");
                if (map != null && map.stringValue != mapId)
                {
                    map.stringValue = mapId;
                    modified = true;
                }

                modified |= ClearLegacyBoard(so.FindProperty("data"));
                modified |= ClearList(so, "baseDraftDistrictsP0", "baseDraftNodesP0");
                modified |= ClearList(so, "baseDraftDistrictsP1", "baseDraftNodesP1");

                if (modified)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(config);
                    changed++;
                    report.AppendLine("migrated " + path + " -> mapId '" + mapId + "'");
                }
                else report.AppendLine("already migrated " + path);
            }

            if (changed > 0) AssetDatabase.SaveAssets();
            report.Append(changed + " of " + guids.Length + " BoardConfig asset(s) changed.");
            return report.ToString();
        }

        /// <summary>Zeroes the old serialized grid: its numbers and its fixed placements.</summary>
        private static bool ClearLegacyBoard(SerializedProperty data)
        {
            if (data == null) return false;
            bool modified = false;

            SerializedProperty end = data.GetEndProperty();
            SerializedProperty p = data.Copy();
            bool enter = true;
            while (p.Next(enter) && !SerializedProperty.EqualContents(p, end))
            {
                enter = false;
                if (p.propertyType == SerializedPropertyType.Integer && p.intValue != 0)
                {
                    p.intValue = 0;
                    modified = true;
                }
                else if (p.isArray && p.propertyType == SerializedPropertyType.Generic && p.arraySize != 0)
                {
                    p.arraySize = 0;
                    modified = true;
                }
            }
            return modified;
        }

        private static bool ClearList(SerializedObject so, params string[] names)
        {
            foreach (string name in names)
            {
                SerializedProperty list = so.FindProperty(name);
                if (list == null || !list.isArray) continue;
                if (list.arraySize == 0) return false;
                list.arraySize = 0;
                return true;
            }
            return false;
        }
    }
}
