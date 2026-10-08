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
            // B-E gate: run the migration first. A legacy grid must fail this test.
            const string boardPath = "Assets/Data/Game/Board/DefaultBoardConfig.asset";
            var board = AssetDatabase.LoadAssetAtPath<ScriptableObject>(boardPath);
            Assert.IsNotNull(board);
            Assert.AreEqual(BoardConfigScriptGuid, GuidOfScript(board));
            Assert.AreEqual("5615db51283388145839515f3e5bfbc8", AssetDatabase.AssetPathToGUID(boardPath));
            var boardObject = new SerializedObject(board);
            Assert.AreEqual("hourglass-01", Find(boardObject, "mapId").stringValue);
            foreach (string field in new[] { "gridCols", "gridRows", "defaultLinkWeight",
                "startingVillagersPerPlayer", "startingFood", "startingMaterials", "startingMetal",
                "ownedMultiplier", "partiallyOwnedMultiplier", "unownedMultiplier",
                "enemyPartiallyOwnedMultiplier", "enemyOwnedMultiplier" })
                Assert.AreEqual(0, Find(boardObject, "data." + field).intValue, "B-E must clear legacy " + field);
            Assert.AreEqual(0, Find(boardObject, "data.initialPlacements").arraySize);
            Assert.AreEqual(0, Find(boardObject, "baseDraftDistrictsP0").arraySize);
            Assert.AreEqual(0, Find(boardObject, "baseDraftDistrictsP1").arraySize);
            Assert.AreEqual(6f, Find(boardObject, "nodeScale").floatValue);
            Assert.AreEqual(-10f, Find(boardObject, "boundsMinX").floatValue);
            Assert.AreEqual(30f, Find(boardObject, "boundsMaxX").floatValue);
            Assert.AreEqual(-15f, Find(boardObject, "boundsMinZ").floatValue);
            Assert.AreEqual(45f, Find(boardObject, "boundsMaxZ").floatValue);
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

            var pickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Game/UI/Draft/DraftSlot_Prefab.prefab");
            Assert.IsNotNull(pickPrefab);
            MonoBehaviour pick = null;
            foreach (MonoBehaviour behaviour in pickPrefab.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && GuidOfScript(behaviour) == "fb270639d978f684bb60da6b47a723ae") pick = behaviour;
            Assert.IsNotNull(pick, "The old DraftSlotUI binding must resolve to DraftPickUI.");
            Assert.AreEqual("NodeWar.UI.DraftPickUI", pick.GetType().FullName);
            Assert.AreEqual("fb270639d978f684bb60da6b47a723ae", GuidOfScript(pick));
            var pickObject = new SerializedObject(pick);
            foreach (string field in new[] { "iconImage", "backgroundImage", "labelText" })
                Assert.IsNotNull(Find(pickObject, field).objectReferenceValue, "Keep the card's " + field + " binding.");
        }

        [Test]
        public void RuntimeTerrain_PierFallbackHasBridgeGeometryAndLegalCellsHaveTwoCues()
        {
            // Runtime presentation is in Assembly-CSharp; an asmdef test cannot reference it
            // directly. Invoke its public factory, then inspect real Unity objects.
            var terrainType = System.Type.GetType("NodeWar.View.BoardTerrainView, Assembly-CSharp", true);
            var board = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Data/Game/Board/DefaultBoardConfig.asset");
            var terrain = (MonoBehaviour)terrainType.GetMethod("Create").Invoke(null, new object[] { board });
            var parent = new GameObject("B6_PierTest");
            try
            {
                Assert.AreEqual(49, terrain.transform.Find("Tiles").GetComponentsInChildren<MeshRenderer>().Length,
                    "All cells, including land, must read before drafting a piece.");
                foreach (Collider collider in terrain.GetComponentsInChildren<Collider>()) Assert.IsFalse(collider.enabled);
                foreach (MonoBehaviour component in terrain.GetComponentsInChildren<MonoBehaviour>())
                    Assert.AreNotEqual("NodeWar.View.NodeView", component.GetType().FullName, "Empty water has no node targets.");
                terrainType.GetMethod("ShowDraft").Invoke(terrain, new object[]
                    { null, true, NodeWar.Simulation.DistrictType.Pier });
                Transform legal = terrain.transform.Find("Legal_1_3");
                Assert.IsNotNull(legal);
                Assert.IsTrue(legal.gameObject.activeSelf);
                Assert.IsNotNull(legal.Find("Tint").GetComponent<Renderer>());
                Assert.AreEqual(4, legal.Find("Outline").GetComponent<LineRenderer>().positionCount);
                var pier = (GameObject)terrainType.GetMethod("CreatePierNode").Invoke(null,
                    new object[] { terrain, 1, 3, parent.transform });
                Transform deck = pier.transform.Find("Deck");
                Assert.IsNotNull(deck);
                Assert.Greater(deck.localScale.z, deck.localScale.x * 3f, "A narrow deck across the lake, not a square land tile.");
                Assert.AreEqual(3, pier.GetComponentsInChildren<MeshRenderer>().Length, "Deck and two rails.");
                Assert.AreEqual(LayerMask.NameToLayer("Nodes"), pier.layer);
                Assert.IsTrue(pier.GetComponent<BoxCollider>().enabled);
                bool hasNode = false;
                foreach (MonoBehaviour component in pier.GetComponents<MonoBehaviour>())
                    if (component.GetType().FullName == "NodeWar.View.NodeView") hasNode = true;
                Assert.IsTrue(hasNode);
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(terrain.gameObject);
            }
        }
    }
}
#endif
