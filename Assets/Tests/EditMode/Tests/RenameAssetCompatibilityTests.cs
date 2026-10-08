#if UNITY_EDITOR
// Editor-only: runs in Unity's Test Runner, where AssetDatabase exists. The
// dotnet projects compile this folder too, without UNITY_EDITOR, so the whole
// file is out of their build.
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NodeWar.Tests
{
    /// <summary>
    /// A renamed Unity class must keep its script GUID (so every asset, prefab
    /// and scene that points at it still does) and every serialized value its
    /// assets already hold. The GUIDs are those of the scripts before the
    /// district/deck/position vocabulary rename; the values are what the
    /// checked-in assets say. Property lookups try the new serialized name
    /// then the old one, so the test reads the same before and after.
    /// </summary>
    public class RenameAssetCompatibilityTests
    {
        private const string DefinitionScriptGuid = "96268278c45b7e843a0bc05b0f2dc522"; // was NodeDefinition.cs
        private const string PositionerScriptGuid = "2d61d348016264d438b84b725d8a56fc"; // was NodeSlotManager.cs
        private const string BoardConfigScriptGuid = "7dfee6c68e0ca34469b710921eaf766d"; // BoardConfig.cs, never moved

        private static SerializedProperty Find(SerializedObject so, params string[] names)
        {
            foreach (string name in names)
            {
                SerializedProperty p = so.FindProperty(name);
                if (p != null) return p;
            }
            Assert.Fail("None of " + string.Join(", ", names) + " is a serialized property of " + so.targetObject.name);
            return null;
        }

        private static string GuidOfScript(Object instance)
        {
            MonoScript script = instance is ScriptableObject so ? MonoScript.FromScriptableObject(so)
                : MonoScript.FromMonoBehaviour((MonoBehaviour)instance);
            return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));
        }

        [Test]
        public void MovedScripts_KeepGuidAndSerializedValues()
        {
            // Board: the script keeps its GUID, and the asset is in one of two states. As first
            // saved it still holds the old 4x7 grid and base draft lists. After the
            // "Migrate BoardConfig To Map ID" command it names a shipped map and holds none of
            // that. Either way its presentation numbers and bot loadout are untouched.
            var board = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Data/Game/Board/DefaultBoardConfig.asset");
            Assert.IsNotNull(board);
            Assert.AreEqual(BoardConfigScriptGuid, GuidOfScript(board));
            var boardObject = new SerializedObject(board);
            SerializedProperty mapId = boardObject.FindProperty("mapId");
            bool migrated = mapId != null && mapId.stringValue == "hourglass-01" &&
                Find(boardObject, "data.gridCols").intValue == 0;
            if (!migrated)
            {
                Assert.AreEqual(4, Find(boardObject, "data.gridCols").intValue);
                Assert.AreEqual(7, Find(boardObject, "data.gridRows").intValue);
                Assert.AreEqual(4, Find(boardObject, "data.defaultLinkWeight", "data.defaultEdgeWeight").intValue);
                Assert.AreEqual(2, Find(boardObject, "data.initialPlacements").arraySize);
                foreach (string[] names in new[]
                {
                    new[] { "baseDraftDistrictsP0", "baseDraftNodesP0" },
                    new[] { "baseDraftDistrictsP1", "baseDraftNodesP1" }
                })
                {
                    SerializedProperty list = Find(boardObject, names);
                    Assert.AreEqual(3, list.arraySize);
                    for (int i = 0; i < 3; i++)
                        Assert.AreEqual(i + 1, list.GetArrayElementAtIndex(i).FindPropertyRelative("districtType").intValue);
                }
            }
            else
            {
                Assert.AreEqual(0, Find(boardObject, "data.gridRows").intValue);
                Assert.AreEqual(0, Find(boardObject, "data.initialPlacements").arraySize);
                Assert.AreEqual(0, Find(boardObject, "baseDraftDistrictsP0", "baseDraftNodesP0").arraySize);
                Assert.AreEqual(0, Find(boardObject, "baseDraftDistrictsP1", "baseDraftNodesP1").arraySize);
            }
            Assert.AreEqual(6f, Find(boardObject, "nodeScale").floatValue);
            Assert.AreEqual(15f, Find(boardObject, "draftTurnDuration").floatValue);
            Assert.AreEqual(2, Find(boardObject, "maxConsecutiveTimeouts").intValue);
            Assert.AreEqual(0, Find(boardObject, "botLoadoutDistricts", "botLoadoutNodes").arraySize);

            // Definition assets: script GUID, ID string and name.
            var barracks = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Data/Lobby/Nodes/Barracks.asset");
            Assert.IsNotNull(barracks);
            Assert.AreEqual(DefinitionScriptGuid, GuidOfScript(barracks));
            var definition = new SerializedObject(barracks);
            Assert.AreEqual("node_barracks", Find(definition, "districtID", "nodeID").stringValue);
            Assert.AreEqual("Barracks", Find(definition, "displayName").stringValue);

            // The district prefab's positioner component.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Game/RevisedNodes/BaseNode.prefab");
            Assert.IsNotNull(prefab);
            bool found = false;
            foreach (MonoBehaviour behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && GuidOfScript(behaviour) == PositionerScriptGuid) found = true;
            Assert.IsTrue(found, "BaseNode.prefab no longer resolves the positioner script by its old GUID.");
        }
    }
}
#endif
