using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using NodeWar.Core;
using NodeWar.Lobby;
using NodeWar.UI;

namespace NodeWar.EditorTools
{
    /// <summary>
    /// One-time Editor setup for the UI Toolkit draft screen, the third of
    /// these after the lobby and the HUD and written the same way for the same
    /// reason: a PanelSettings asset and a scene object carrying a UIDocument
    /// are Unity-serialised, and hand-written YAML with guessed GUIDs is how
    /// scenes get quietly corrupted.
    ///
    /// It does one thing the other two did not have to: the draft needs the
    /// world-space ghost and placed-piece prefabs, and the sticker sprite per
    /// district. Those already exist, wired onto the uGUI draft prefab that
    /// GameManager points at, so they are COPIED FROM THERE rather than looked
    /// up by path. GameManager's own reference is the only thing that knows
    /// which prefab is the draft's, and a second answer to that question is a
    /// second thing to keep in step.
    ///
    /// Safe to run more than once. Everything is looked up first and only
    /// created when missing, and it never overwrites a PanelSettings you have
    /// since tuned. It does re-copy the prefabs and stickers, because that is
    /// the point of running it again after changing them.
    ///
    /// After running: select GameManager in the Gameplay scene and tick
    /// "Use UI Toolkit Draft". Untick to go back to the uGUI draft.
    /// </summary>
    public static class UIToolkitDraftSetup
    {
        private const string UIRoot = "Assets/UI";
        private const string ThemePath = UIRoot + "/UnityDefaultRuntimeTheme.tss";
        private const string PanelSettingsPath = UIRoot + "/DraftPanelSettings.asset";
        private const string LayoutPath = UIRoot + "/Layouts/DraftScreen.uxml";
        private const string NodeDefinitionFolder = "Assets/Data/Lobby/Nodes";
        private const string RootObjectName = "UIToolkitDraft";
        private const string GameplaySceneName = "Gameplay";

        [MenuItem("Tools/Node War/Set Up UI Toolkit Draft")]
        public static void SetUp()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (scene.name != GameplaySceneName)
            {
                EditorUtility.DisplayDialog(
                    "Wrong scene",
                    "Open Assets/Scenes/Gameplay.unity first.\n\n" +
                    "Active scene is \"" + scene.name + "\".",
                    "OK");
                return;
            }

            ThemeStyleSheet theme = EnsureTheme();
            if (theme == null) return;

            PanelSettings panelSettings = EnsurePanelSettings(theme);
            if (panelSettings == null) return;

            VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            if (layout == null)
            {
                Debug.LogError("[UIToolkitDraftSetup] Could not load " + LayoutPath +
                               ". Has it imported? Try Assets > Reimport All.");
                return;
            }

            GameManager manager = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);

            GameObject root = EnsureSceneRoot(panelSettings, layout, manager);
            WireGameManager(manager, root);

            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[UIToolkitDraftSetup] Done. Select GameManager and tick " +
                      "\"Use UI Toolkit Draft\", then save the scene.");

            Selection.activeGameObject = root;
        }

        /// <summary>
        /// Shares the runtime theme asset with the lobby and the HUD. It is one
        /// import line with nothing surface-specific in it; created here only if
        /// neither of the other setups has ever run.
        /// </summary>
        private static ThemeStyleSheet EnsureTheme()
        {
            ThemeStyleSheet existing = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (existing != null) return existing;

            EnsureFolder(UIRoot);

            File.WriteAllText(Path.GetFullPath(ThemePath),
                "@import url(\"unity-theme://default\");\n");

            AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceSynchronousImport);

            ThemeStyleSheet created = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);

            if (created == null)
            {
                Debug.LogError("[UIToolkitDraftSetup] Wrote " + ThemePath +
                               " but it did not import as a ThemeStyleSheet.");
            }

            return created;
        }

        /// <summary>
        /// Its own PanelSettings rather than the HUD's. The two are never on at
        /// the same time, so sharing would work today - but they are separate
        /// surfaces with separate sort orders, and one asset tuned for the match
        /// is one asset that has to be re-checked every time the draft changes.
        ///
        /// Same 390x844 reference frame as everything else, so a unit here is
        /// about a point on a phone and the 44 minimum touch target on the ✕
        /// means what it says.
        /// </summary>
        private static PanelSettings EnsurePanelSettings(ThemeStyleSheet theme)
        {
            PanelSettings existing =
                AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            if (existing != null)
            {
                if (existing.themeStyleSheet == null)
                {
                    existing.themeStyleSheet = theme;
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                }
                return existing;
            }

            EnsureFolder(UIRoot);

            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(390, 844);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0f;

            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            AssetDatabase.SaveAssets();

            return settings;
        }

        private static GameObject EnsureSceneRoot(PanelSettings panelSettings,
            VisualTreeAsset layout, GameManager manager)
        {
            GameObject root = GameObject.Find(RootObjectName);

            if (root == null)
            {
                // Inactive objects are not found by GameObject.Find, and this one
                // is left inactive on purpose - so look for it the way that works
                // before concluding it is missing and making a second.
                DraftScreenController[] existing =
                    Object.FindObjectsByType<DraftScreenController>(FindObjectsInactive.Include);

                if (existing.Length > 0) root = existing[0].gameObject;
            }

            if (root == null)
            {
                root = new GameObject(RootObjectName);
                Undo.RegisterCreatedObjectUndo(root, "Create UI Toolkit Draft");
            }

            UIDocument document = root.GetComponent<UIDocument>();
            if (document == null) document = root.AddComponent<UIDocument>();

            document.panelSettings = panelSettings;
            document.visualTreeAsset = layout;

            DraftScreenController controller = root.GetComponent<DraftScreenController>();
            if (controller == null) controller = root.AddComponent<DraftScreenController>();

            SerializedObject so = new SerializedObject(controller);

            AssignObject(so, "draftLayout", layout);
            CopyWorldPiecesFromUGUIDraft(so, manager);
            AssignNodeDefinitions(so);

            so.ApplyModifiedPropertiesWithoutUndo();

            // Starts inactive. GameManager switches it on when the toggle says
            // so, so running this setup changes nothing about how the game
            // currently plays.
            root.SetActive(false);

            return root;
        }

        /// <summary>
        /// Takes the ghost prefab, the placed-piece prefab and the sticker table
        /// from the uGUI draft prefab GameManager already points at.
        ///
        /// The ghost prefab is on DraftPlacementController and the other two are
        /// on DraftUI, which is why this reaches for two components rather than
        /// one. That split is the uGUI stack's, not ours - the new controller
        /// holds all three, because on that stack the chrome and the placement
        /// cannot be separated.
        /// </summary>
        private static void CopyWorldPiecesFromUGUIDraft(SerializedObject so, GameManager manager)
        {
            GameObject prefab = FindDraftUIPrefab(manager);

            if (prefab == null)
            {
                Debug.LogWarning("[UIToolkitDraftSetup] Could not find the uGUI draft prefab " +
                                 "through GameManager, so the ghost prefab, placed-piece prefab " +
                                 "and sticker sprites were not copied. Assign them on " +
                                 RootObjectName + " by hand.");
                return;
            }

            DraftUI source = prefab.GetComponent<DraftUI>();
            DraftPlacementController placement = prefab.GetComponent<DraftPlacementController>();

            if (source != null)
            {
                SerializedObject sourceSO = new SerializedObject(source);

                CopyObjectProperty(sourceSO, "confirmedPlacementPrefab",
                                   so, "confirmedPlacementPrefab");
                CopyStickers(sourceSO, so);
            }

            if (placement != null)
            {
                SerializedObject placementSO = new SerializedObject(placement);
                CopyObjectProperty(placementSO, "ghostPreviewPrefab", so, "ghostPreviewPrefab");
            }
        }

        private static GameObject FindDraftUIPrefab(GameManager manager)
        {
            if (manager == null) return null;

            SerializedObject so = new SerializedObject(manager);
            SerializedProperty property = so.FindProperty("draftUIPrefab");

            return property != null ? property.objectReferenceValue as GameObject : null;
        }

        /// <summary>
        /// The sticker table, element by element. The two structs have the same
        /// two fields but are different types, so this copies values rather than
        /// the array - which is also what keeps the new controller from having
        /// to depend on the old one's nested type.
        /// </summary>
        private static void CopyStickers(SerializedObject from, SerializedObject to)
        {
            SerializedProperty source = from.FindProperty("stickerMappings");
            SerializedProperty target = to.FindProperty("stickerMappings");

            if (source == null || target == null)
            {
                Debug.LogWarning("[UIToolkitDraftSetup] No stickerMappings on one side; " +
                                 "the draft cards will show monograms only.");
                return;
            }

            target.arraySize = source.arraySize;

            for (int i = 0; i < source.arraySize; i++)
            {
                SerializedProperty s = source.GetArrayElementAtIndex(i);
                SerializedProperty t = target.GetArrayElementAtIndex(i);

                t.FindPropertyRelative("districtType").enumValueIndex =
                    s.FindPropertyRelative("districtType").enumValueIndex;
                t.FindPropertyRelative("sprite").objectReferenceValue =
                    s.FindPropertyRelative("sprite").objectReferenceValue;
            }
        }

        /// <summary>
        /// Every NodeDefinition in the lobby's data folder, so a card's name is
        /// the name the lobby showed. Four districts have no definition - Farm,
        /// Mine, Village and Forge are base draft nodes no loadout slot can hold
        /// - and DraftPieceInfo falls back to the enum name for those.
        /// </summary>
        private static void AssignNodeDefinitions(SerializedObject so)
        {
            SerializedProperty property = so.FindProperty("nodeDefinitions");
            if (property == null) return;

            string[] guids = AssetDatabase.FindAssets("t:NodeDefinition",
                new[] { NodeDefinitionFolder });

            List<NodeDefinition> found = new List<NodeDefinition>();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                NodeDefinition definition = AssetDatabase.LoadAssetAtPath<NodeDefinition>(path);
                if (definition != null) found.Add(definition);
            }

            property.arraySize = found.Count;

            for (int i = 0; i < found.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = found[i];

            if (found.Count == 0)
            {
                Debug.LogWarning("[UIToolkitDraftSetup] No NodeDefinitions found under " +
                                 NodeDefinitionFolder + ". Cards will use enum names.");
            }
        }

        private static void WireGameManager(GameManager manager, GameObject root)
        {
            if (manager == null)
            {
                Debug.LogWarning("[UIToolkitDraftSetup] No GameManager in the scene. Assign " +
                                 RootObjectName + " to its \"Ui Toolkit Draft Root\" field by hand.");
                return;
            }

            SerializedObject so = new SerializedObject(manager);
            SerializedProperty property = so.FindProperty("uiToolkitDraftRoot");

            if (property == null)
            {
                Debug.LogWarning("[UIToolkitDraftSetup] GameManager has no uiToolkitDraftRoot " +
                                 "field. Has it compiled?");
                return;
            }

            property.objectReferenceValue = root;
            so.ApplyModifiedProperties();
        }

        private static void CopyObjectProperty(SerializedObject from, string fromName,
            SerializedObject to, string toName)
        {
            SerializedProperty source = from.FindProperty(fromName);
            SerializedProperty target = to.FindProperty(toName);

            if (source == null || target == null)
            {
                Debug.LogWarning("[UIToolkitDraftSetup] Could not copy " + fromName +
                                 " to " + toName + "; one side has no such field.");
                return;
            }

            target.objectReferenceValue = source.objectReferenceValue;
        }

        private static void AssignObject(SerializedObject so, string propertyName, Object asset)
        {
            SerializedProperty property = so.FindProperty(propertyName);

            if (property == null)
            {
                Debug.LogWarning("[UIToolkitDraftSetup] DraftScreenController has no " +
                                 propertyName + " field. Has it compiled?");
                return;
            }

            property.objectReferenceValue = asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
