using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NodeWar.Simulation;
using NodeWar.View;

namespace NodeWar.EditorTools
{
    /// <summary>
    /// Creates one <see cref="DistrictVisual"/> per <see cref="DistrictType"/>
    /// and the <see cref="DistrictVisualTable"/> that indexes them.
    ///
    /// Through Unity rather than by hand, for the reason UIToolkitHUDSetup
    /// gives: a ScriptableObject asset is Unity-serialised, and hand-written
    /// YAML with guessed GUIDs is how a project gets quietly corrupted.
    ///
    /// Safe to run more than once, and that is the point -- it is how a
    /// district added to the enum later gets its slot. Everything is looked up
    /// first and only created when missing, and an existing asset never has a
    /// field it already holds overwritten. Running it after art has been
    /// assigned changes nothing but the table's membership.
    ///
    /// The board prefab is matched by name against
    /// Assets/Prefabs/Game/RevisedNodes/, whose files are named
    /// "&lt;District&gt;Node Variant" or "&lt;District&gt;_Node Variant". A district
    /// with no prefab is left null and reported -- that is a finding, not a
    /// failure, and today it is true of three of them.
    /// </summary>
    public static class DistrictVisualSetup
    {
        private const string VisualsFolder = "Assets/Data/Game/Districts";
        private const string PrefabFolder = "Assets/Prefabs/Game/RevisedNodes";
        private const string TablePath = VisualsFolder + "/DistrictVisualTable.asset";

        /// <summary>
        /// DistrictType.None is the empty connector, whose board art is the
        /// Crossroads prefab. Nothing else in the enum disagrees with its
        /// prefab's name.
        /// </summary>
        private const string CrossroadsPrefabStem = "Crossroads";

        [MenuItem("Tools/Node War/Create District Visuals")]
        public static void CreateAll()
        {
            EnsureFolder(VisualsFolder);

            Array districts = Enum.GetValues(typeof(DistrictType));
            List<DistrictVisual> visuals = new List<DistrictVisual>(districts.Length);
            List<string> withoutPrefab = new List<string>();

            foreach (DistrictType district in districts)
            {
                string path = VisualsFolder + "/" + district + ".asset";

                DistrictVisual visual = AssetDatabase.LoadAssetAtPath<DistrictVisual>(path);
                bool created = visual == null;

                if (created)
                {
                    visual = ScriptableObject.CreateInstance<DistrictVisual>();
                    visual.district = district;
                    AssetDatabase.CreateAsset(visual, path);
                }

                // The district is identity, not art: fix it even on an asset
                // that already existed, because a wrong one breaks lookup
                // silently and no artist would ever be the one to set it.
                if (visual.district != district)
                {
                    visual.district = district;
                    EditorUtility.SetDirty(visual);
                }

                // Only ever fill an empty slot. An artist who has pointed this
                // at something else has made a decision this command does not
                // get to reverse.
                if (visual.boardPrefab == null)
                {
                    GameObject prefab = FindBoardPrefab(district);
                    if (prefab != null)
                    {
                        visual.boardPrefab = prefab;
                        EditorUtility.SetDirty(visual);
                    }
                }

                if (visual.boardPrefab == null) withoutPrefab.Add(district.ToString());

                visuals.Add(visual);
            }

            DistrictVisualTable table = AssetDatabase.LoadAssetAtPath<DistrictVisualTable>(TablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<DistrictVisualTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            SerializedObject so = new SerializedObject(table);
            SerializedProperty entries = so.FindProperty("entries");
            entries.arraySize = visuals.Count;
            for (int i = 0; i < visuals.Count; i++)
            {
                entries.GetArrayElementAtIndex(i).objectReferenceValue = visuals[i];
            }
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[DistrictVisualSetup] " + visuals.Count + " district visuals in " +
                      VisualsFolder + ". " +
                      (withoutPrefab.Count == 0
                          ? "Every district has a board prefab."
                          : "No board prefab for: " + string.Join(", ", withoutPrefab) + "."),
                      table);

            Selection.activeObject = table;
        }

        /// <summary>
        /// The node prefab whose name starts with the district's, allowing the
        /// underscore the folder is inconsistent about ("ArsenalNode Variant"
        /// against "Barracks_Node Variant").
        /// </summary>
        private static GameObject FindBoardPrefab(DistrictType district)
        {
            string stem = district == DistrictType.None ? CrossroadsPrefabStem : district.ToString();

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);

                if (!name.StartsWith(stem, StringComparison.Ordinal)) continue;

                // "Core" must not match a future "CoreYard": the next character
                // has to be the separator, not more of a longer name.
                if (name.Length > stem.Length)
                {
                    char next = name[stem.Length];
                    if (next != '_' && next != 'N' && next != ' ') continue;
                }

                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path).Replace(System.IO.Path.DirectorySeparatorChar, '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
