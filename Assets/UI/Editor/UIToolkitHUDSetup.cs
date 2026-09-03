using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using NodeWar.Core;
using NodeWar.UI;

namespace NodeWar.EditorTools
{
    /// <summary>
    /// One-time Editor setup for the UI Toolkit in-match HUD, the Gameplay
    /// counterpart to UIToolkitLobbySetup.
    ///
    /// A separate file rather than a second menu item on that one. The two set
    /// up different scenes, they will stop being needed at different times, and
    /// the lobby setup should not grow a responsibility it has to be untangled
    /// from later. The cost is about twenty duplicated lines of PanelSettings
    /// boilerplate, which is cheaper than the coupling.
    ///
    /// It exists for the same reason the lobby one does: a PanelSettings asset
    /// and a scene object carrying a UIDocument are Unity-serialised, and
    /// hand-written YAML with guessed GUIDs is how scenes get quietly
    /// corrupted. Unity makes them, through its own APIs.
    ///
    /// Safe to run more than once. Everything is looked up first and only
    /// created when missing, and it never overwrites a PanelSettings you have
    /// since tuned.
    ///
    /// After running: select GameManager in the Gameplay scene and tick
    /// "Use UI Toolkit HUD". Untick to go back.
    /// </summary>
    public static class UIToolkitHUDSetup
    {
        private const string UIRoot = "Assets/UI";
        private const string ThemePath = UIRoot + "/UnityDefaultRuntimeTheme.tss";
        private const string PanelSettingsPath = UIRoot + "/HUDPanelSettings.asset";
        private const string LayoutPath = UIRoot + "/Layouts/GameplayHUD.uxml";
        private const string RootObjectName = "UIToolkitHUD";
        private const string GameplaySceneName = "Gameplay";

        [MenuItem("Tools/Node War/Set Up UI Toolkit HUD")]
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
                Debug.LogError("[UIToolkitHUDSetup] Could not load " + LayoutPath +
                               ". Has it imported? Try Assets > Reimport All.");
                return;
            }

            GameObject root = EnsureSceneRoot(panelSettings, layout);
            WireGameManager(root);

            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[UIToolkitHUDSetup] Done. Select GameManager and tick " +
                      "\"Use UI Toolkit HUD\" to switch HUDs, then save the scene.");

            Selection.activeGameObject = root;
        }

        /// <summary>
        /// Shares the lobby's runtime theme asset rather than making a second
        /// one - it is a single import line and there is nothing scene-specific
        /// about it. Created here only if the lobby setup has never run.
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
                Debug.LogError("[UIToolkitHUDSetup] Wrote " + ThemePath +
                               " but it did not import as a ThemeStyleSheet.");
            }

            return created;
        }

        /// <summary>
        /// Its own PanelSettings, not the lobby's. The HUD sits over a 3D scene
        /// and the lobby does not, so sort order, clearing and scale are things
        /// the two will want to answer differently - and sharing one asset means
        /// tuning either breaks the other.
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

            // Same reasoning as the lobby: ConstantPhysicalSize is what makes a
            // 44px touch target in Theme.uss about 44 real points on the device.
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
                Undo.RegisterCreatedObjectUndo(root, "Create UI Toolkit HUD");
            }

            UIDocument document = root.GetComponent<UIDocument>();
            if (document == null) document = root.AddComponent<UIDocument>();

            document.panelSettings = panelSettings;
            document.visualTreeAsset = layout;

            GameplayHUDController controller = root.GetComponent<GameplayHUDController>();
            if (controller == null) controller = root.AddComponent<GameplayHUDController>();

            SerializedObject so = new SerializedObject(controller);

            Assign(so, "hudLayout", layout);
            Assign(so, "nodeSheetLayout",
                   AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UIRoot + "/Layouts/NodeSheet.uxml"));

            so.ApplyModifiedPropertiesWithoutUndo();

            // Starts inactive. GameManager.ApplyHUDStackChoice turns it on when
            // the toggle says so, so running this setup changes nothing about
            // how the game currently plays.
            root.SetActive(false);

            return root;
        }

        private static void WireGameManager(GameObject root)
        {
            GameManager manager = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);

            if (manager == null)
            {
                Debug.LogWarning("[UIToolkitHUDSetup] No GameManager in the scene. Assign " +
                                 RootObjectName + " to its \"Ui Toolkit Hud Root\" field by hand.");
                return;
            }

            SerializedObject so = new SerializedObject(manager);
            SerializedProperty property = so.FindProperty("uiToolkitHudRoot");

            if (property == null)
            {
                Debug.LogWarning("[UIToolkitHUDSetup] GameManager has no uiToolkitHudRoot field. " +
                                 "Has it compiled?");
                return;
            }

            property.objectReferenceValue = root;
            so.ApplyModifiedProperties();
        }

        private static void Assign(SerializedObject so, string propertyName, VisualTreeAsset asset)
        {
            SerializedProperty property = so.FindProperty(propertyName);

            if (property == null)
            {
                Debug.LogWarning("[UIToolkitHUDSetup] GameplayHUDController has no " +
                                 propertyName + " field. Has it compiled?");
                return;
            }

            if (asset == null)
                Debug.LogWarning("[UIToolkitHUDSetup] No asset found for " + propertyName + ".");

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
