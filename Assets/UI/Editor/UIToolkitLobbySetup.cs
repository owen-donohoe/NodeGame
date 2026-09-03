using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using NodeWar.Lobby;

namespace NodeWar.EditorTools
{
    /// <summary>
    /// One-time Editor setup for the UI Toolkit lobby.
    ///
    /// This exists because three pieces of the shell cannot be authored as text
    /// by hand with any confidence: a PanelSettings asset, a runtime theme, and
    /// a scene object carrying a UIDocument. All three are Unity-serialised, and
    /// hand-written YAML with guessed GUIDs is how scenes get quietly corrupted.
    /// So Unity creates them, through its own APIs, and this menu item is the
    /// trigger.
    ///
    /// It is safe to run more than once. Everything it makes is looked up first
    /// and only created when missing, and it never overwrites a PanelSettings
    /// you have since tuned.
    ///
    /// After running: select LobbyManager in the Lobby scene and tick
    /// "Use UI Toolkit Lobby" to switch stacks. Untick to go back.
    /// </summary>
    public static class UIToolkitLobbySetup
    {
        private const string UIRoot = "Assets/UI";
        private const string DataRoot = "Assets/Data/Lobby";
        private const string ThemePath = UIRoot + "/UnityDefaultRuntimeTheme.tss";
        private const string PanelSettingsPath = UIRoot + "/LobbyPanelSettings.asset";
        private const string LayoutPath = UIRoot + "/Layouts/LobbyRoot.uxml";
        private const string RootObjectName = "UIToolkitLobby";
        private const string LobbySceneName = "Lobby";

        [MenuItem("Tools/Node War/Set Up UI Toolkit Lobby")]
        public static void SetUp()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (scene.name != LobbySceneName)
            {
                EditorUtility.DisplayDialog(
                    "Wrong scene",
                    "Open Assets/Scenes/Lobby.unity first.\n\n" +
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
                Debug.LogError("[UIToolkitLobbySetup] Could not load " + LayoutPath +
                               ". Has it imported? Try Assets > Reimport All.");
                return;
            }

            GameObject root = EnsureSceneRoot(panelSettings, layout);
            WireLobbyManager(root);

            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[UIToolkitLobbySetup] Done. Select LobbyManager and tick " +
                      "\"Use UI Toolkit Lobby\" to switch stacks, then save the scene.");

            Selection.activeGameObject = root;
        }

        /// <summary>
        /// The runtime theme. A .tss is a plain text file that the theme
        /// importer turns into a ThemeStyleSheet, and the default one is a
        /// single import line - this is the same content Unity's own
        /// "Create > UI Toolkit > TSS Theme File" produces. Writing the text and
        /// importing it is therefore safe, unlike guessing at binary asset YAML.
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
                Debug.LogError("[UIToolkitLobbySetup] Wrote " + ThemePath +
                               " but it did not import as a ThemeStyleSheet.");
            }

            return created;
        }

        private static PanelSettings EnsurePanelSettings(ThemeStyleSheet theme)
        {
            PanelSettings existing =
                AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            if (existing != null)
            {
                // Only fill in a missing theme. Anything else the user has
                // changed is theirs to keep.
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

            // ConstantPhysicalSize is the reason the theme can talk in points.
            // A 44px touch target in Theme.uss is then about 44 real points on
            // the device, which is what makes the minimum meaningful. It also
            // matches how GestureThresholds already reasons about mm.
            settings.scaleMode = PanelScaleMode.ConstantPhysicalSize;
            settings.referenceDpi = 96f;
            settings.fallbackDpi = 96f;

            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            AssetDatabase.SaveAssets();

            return settings;
        }

        private static GameObject EnsureSceneRoot(PanelSettings panelSettings, VisualTreeAsset layout)
        {
            GameObject root = GameObject.Find(RootObjectName);

            if (root == null)
            {
                root = new GameObject(RootObjectName);
                Undo.RegisterCreatedObjectUndo(root, "Create UI Toolkit Lobby");
            }

            UIDocument document = root.GetComponent<UIDocument>();
            if (document == null) document = root.AddComponent<UIDocument>();

            document.panelSettings = panelSettings;
            document.visualTreeAsset = layout;

            LobbyUIController controller = root.GetComponent<LobbyUIController>();
            if (controller == null) controller = root.AddComponent<LobbyUIController>();

            // These are private [SerializeField]s; SerializedObject is the
            // supported way to set one without widening its access.
            //
            // Each session that adds a page adds a row here. A page whose layout
            // is missing degrades to a placeholder rather than an exception, so
            // a half-updated table is visible rather than fatal.
            SerializedObject so = new SerializedObject(controller);

            AssignLayout(so, "rootLayout", layout);
            AssignLayout(so, "homePageLayout", Load(UIRoot + "/Layouts/HomePage.uxml"));
            AssignLayout(so, "playPopupLayout", Load(UIRoot + "/Layouts/PlayPopup.uxml"));
            AssignLayout(so, "workshopPageLayout", Load(UIRoot + "/Layouts/WorkshopPage.uxml"));
            AssignLayout(so, "profilePageLayout", Load(UIRoot + "/Layouts/ProfilePage.uxml"));
            AssignLayout(so, "shopPageLayout", Load(UIRoot + "/Layouts/ShopPage.uxml"));
            AssignLayout(so, "socialPageLayout", Load(UIRoot + "/Layouts/SocialPage.uxml"));

            AssignDefinitions<SuitDefinition>(so, "allSuits", DataRoot + "/Suits");
            AssignDefinitions<NodeDefinition>(so, "allNodes", DataRoot + "/Nodes");

            SerializedProperty managerProperty = so.FindProperty("lobbyManager");
            if (managerProperty != null)
            {
                managerProperty.objectReferenceValue =
                    Object.FindAnyObjectByType<LobbyManager>(FindObjectsInactive.Include);
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // Starts inactive. LobbyManager.ApplyUIStackChoice turns it on when
            // the toggle says so, and leaving it off means running this setup
            // changes nothing about how the lobby currently behaves.
            root.SetActive(false);

            return root;
        }

        private static void WireLobbyManager(GameObject root)
        {
            LobbyManager manager =
                Object.FindAnyObjectByType<LobbyManager>(FindObjectsInactive.Include);

            if (manager == null)
            {
                Debug.LogWarning("[UIToolkitLobbySetup] No LobbyManager in the scene. " +
                                 "Assign " + RootObjectName + " to its " +
                                 "\"Ui Toolkit Lobby Root\" field by hand.");
                return;
            }

            SerializedObject so = new SerializedObject(manager);
            SerializedProperty property = so.FindProperty("uiToolkitLobbyRoot");

            if (property == null)
            {
                Debug.LogWarning("[UIToolkitLobbySetup] LobbyManager has no " +
                                 "uiToolkitLobbyRoot field. Has it compiled?");
                return;
            }

            property.objectReferenceValue = root;
            so.ApplyModifiedProperties();
        }

        private static VisualTreeAsset Load(string path)
        {
            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);

            if (asset == null)
                Debug.LogWarning("[UIToolkitLobbySetup] Layout not found: " + path);

            return asset;
        }

        /// <summary>
        /// Fills a ScriptableObject array field from every asset of that type in
        /// a folder.
        ///
        /// The Workshop needs the 9 NodeDefinitions and 5 SuitDefinitions at
        /// runtime, and the project has no Resources folder to load them from -
        /// GroupSelectionPanel solves the same problem by having them dragged
        /// into the scene one at a time. Collecting them here means adding a new
        /// district is "make the asset, re-run this menu item" rather than
        /// "remember which two arrays to drag it into".
        ///
        /// Sorted by path so the picker's order is stable across machines;
        /// FindAssets does not promise an order.
        /// </summary>
        private static void AssignDefinitions<T>(SerializedObject so, string propertyName, string folder)
            where T : ScriptableObject
        {
            SerializedProperty property = so.FindProperty(propertyName);

            if (property == null)
            {
                Debug.LogWarning("[UIToolkitLobbySetup] LobbyUIController has no " +
                                 propertyName + " field. Has it compiled?");
                return;
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning("[UIToolkitLobbySetup] No such folder: " + folder +
                                 ". " + propertyName + " left empty; the Workshop will " +
                                 "show a labelled empty list.");
                property.arraySize = 0;
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder });
            string[] paths = new string[guids.Length];

            for (int i = 0; i < guids.Length; i++)
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);

            System.Array.Sort(paths, System.StringComparer.Ordinal);

            property.arraySize = paths.Length;

            for (int i = 0; i < paths.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<T>(paths[i]);
            }

            if (paths.Length == 0)
            {
                Debug.LogWarning("[UIToolkitLobbySetup] No " + typeof(T).Name +
                                 " assets found in " + folder + ".");
            }
        }

        private static void AssignLayout(SerializedObject so, string propertyName, VisualTreeAsset asset)
        {
            SerializedProperty property = so.FindProperty(propertyName);

            if (property == null)
            {
                Debug.LogWarning("[UIToolkitLobbySetup] LobbyUIController has no " +
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
