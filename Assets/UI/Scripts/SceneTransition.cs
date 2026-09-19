using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace NodeWar.UI
{
    /// <summary>A scene-only sheet; lobby navigation never goes through this.</summary>
    public sealed class SceneTransition : MonoBehaviour
    {
        private const float SlideSeconds = 0.32f;
        private const float MinimumCoverSeconds = 0.25f;
        // Above Lobby/HUD/Draft and the entire signed-short uGUI sort range.
        private const float OverlayOrder = 32768f;

        private static SceneTransition instance;
        private PanelSettings panelSettings;
        private UIDocument document;
        private VisualElement sheet;
        private Tween slide;
        private bool blockingInput;
        private float offset;

        public static void Load(string sceneName)
        {
            // Ignore double taps and reentrant requests while a load is in flight.
            if (instance != null) return;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("[SceneTransition] Scene is not available: " + sceneName);
                return;
            }

            var theme = Resources.Load<ThemeStyleSheet>("SceneTransition/Theme");
            var layout = Resources.Load<VisualTreeAsset>("SceneTransition/LoadingSheet");
            if (theme == null || layout == null)
            {
                Debug.LogError("[SceneTransition] Missing Resources/SceneTransition assets.");
                return;
            }

            var host = new GameObject("SceneTransition");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<SceneTransition>();
            try
            {
                instance.Build(theme, layout);
                instance.StartCoroutine(instance.Transition(sceneName));
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                Destroy(host);
            }
        }

        private void Build(ThemeStyleSheet theme, VisualTreeAsset layout)
        {
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "SceneTransitionPanelSettings (Runtime)";
            panelSettings.themeStyleSheet = theme;
            // Match HUDPanelSettings: 390-wide reference, width-based scaling.
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(390, 844);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0f;
            panelSettings.sortingOrder = OverlayOrder;
            panelSettings.clearColor = false;

            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = layout;
            document.sortingOrder = OverlayOrder;
            sheet = document.rootVisualElement.Q("loading-sheet");
            SetOffset(100f);
        }

        private IEnumerator Transition(string sceneName)
        {
            try
            {
                BeginInputBlock();
                // Let the new document attach, resolve styles and lay out below screen.
                yield return null;
                yield return SlideTo(0f, Ease.OutCubic);

                float coveredAt = Time.unscaledTime;
                // Render the fully opaque sheet before starting any scene work.
                yield return null;
                AsyncOperation load = BeginLoad(sceneName);
                if (load != null)
                {
                    // Single mode keeps the old unload/active-scene behaviour.
                    // Activation stays enabled; no peer or activation barrier.
                    yield return load;
                    // Even if completion preceded Start, the destination now gets
                    // a whole frame of Start/Update before the reveal begins.
                    yield return null;
                    yield return null;
                }

                while (Time.unscaledTime - coveredAt < MinimumCoverSeconds)
                    yield return null;

                yield return SlideTo(-100f, Ease.InCubic);
                // Keep the input shield through the last rendered exit frame.
                yield return null;
            }
            finally
            {
                EndInputBlock();
                if (document != null) document.enabled = false;
                Destroy(gameObject);
            }
        }

        private static AsyncOperation BeginLoad(string sceneName)
        {
            try
            {
                return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            catch (System.Exception exception)
            {
                // Reveal the old scene and release input if Unity rejects the load.
                Debug.LogException(exception);
                return null;
            }
        }

        private IEnumerator SlideTo(float destination, Ease ease)
        {
            slide = DOTween.To(() => offset, SetOffset, destination, SlideSeconds)
                .SetEase(ease).SetUpdate(true);
            yield return slide.WaitForCompletion();
            SetOffset(destination);
        }

        private void SetOffset(float value)
        {
            offset = value;
            sheet.style.translate = new Translate(0, Length.Percent(value));
        }

        private void BeginInputBlock()
        {
            blockingInput = true;
            // A picking shield alone cannot stop direct Keyboard/Mouse/Touchscreen
            // polling in gameplay. Neutralize held controls, then consume their
            // state events while the input loop and network pump keep running.
            InputSystem.onEvent += ConsumeInput;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ResetControls();
            FocusShield();
        }

        private static bool IsControlDevice(InputDevice device)
        {
            return device is Pointer || device is Keyboard || device is Gamepad || device is Joystick;
        }

        private static void ResetControls()
        {
            foreach (InputDevice device in InputSystem.devices)
                if (IsControlDevice(device)) InputSystem.ResetDevice(device);
        }

        private static void ConsumeInput(InputEventPtr inputEvent, InputDevice device)
        {
            if (IsControlDevice(device) &&
                (inputEvent.IsA<StateEvent>() || inputEvent.IsA<DeltaStateEvent>() ||
                 inputEvent.IsA<TextEvent>()))
                inputEvent.handled = true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            FocusShield();
        }

        private void FocusShield()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            document.rootVisualElement.Q("transition-shield").Focus();
        }

        private void EndInputBlock()
        {
            if (!blockingInput) return;
            blockingInput = false;
            InputSystem.onEvent -= ConsumeInput;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            EndInputBlock();
            slide?.Kill();
            if (panelSettings != null) Destroy(panelSettings);
            if (instance == this) instance = null;
        }
    }
}
