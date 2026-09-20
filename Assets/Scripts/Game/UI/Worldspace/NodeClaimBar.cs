using UnityEngine;
using UnityEngine.UI;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>
    /// Centered claim bar. White background with colored fill extending from
    /// the midpoint outward. P0 (blue) extends right, P1 (red) extends left.
    /// Fill amount represents normalized claim progress (0 = neutral, 1 = fully owned).
    /// </summary>
    public class NodeClaimBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image p0FillImage;
        [SerializeField] private Image p1FillImage;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Colors")]
        [SerializeField] private Color backgroundColor = Color.white;

        [Header("Gradients")]
        [SerializeField] private Gradient p0Gradient;
        [SerializeField] private Gradient p1Gradient;
        [SerializeField] private bool useGradient = true;

        [Header("Visibility")]
        [SerializeField] private float showDuration = 2f;
        [SerializeField] private float fadeSpeed = 3f;

        private SimulationState simState;
        private int nodeID;
        private bool initialized = false;
        private int lastClaimBar;
        private float showTimer;
        private float targetAlpha;
        private Canvas worldCanvas;
        private Camera viewCamera;

        private int claimThreshold = 10000;

        public void Initialize(SimulationState state, int id, int claimThreshold)
        {
            simState = state;
            nodeID = id;
            this.claimThreshold = claimThreshold;
            initialized = true;

            worldCanvas = GetComponentInParent<Canvas>();
            viewCamera = Camera.main;
            if (worldCanvas != null && worldCanvas.renderMode == RenderMode.WorldSpace)
                worldCanvas.worldCamera = viewCamera;

            // These are indicators, not controls. Do not intercept node taps.
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (backgroundImage != null)
                backgroundImage.color = backgroundColor;

            if (p0FillImage != null)
            {
                p0FillImage.color = Color.blue;
                p0FillImage.type = Image.Type.Filled;
                p0FillImage.fillMethod = Image.FillMethod.Horizontal;
                p0FillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                p0FillImage.fillAmount = 0f;
            }

            if (p1FillImage != null)
            {
                p1FillImage.color = Color.red;
                p1FillImage.type = Image.Type.Filled;
                p1FillImage.fillMethod = Image.FillMethod.Horizontal;
                p1FillImage.fillOrigin = (int)Image.OriginHorizontal.Right;
                p1FillImage.fillAmount = 0f;
            }

            lastClaimBar = state.nodes[nodeID].claimBar;
            showTimer = 0f;
            targetAlpha = 0f;

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            EnsureGradients();
        }

        private void Update()
        {
            if (!initialized) return;

            NodeData node = simState.nodes[nodeID];

            // Update fill amounts and colors
            if (node.claimBar > 0)
            {
                float p0Fill = (float)node.claimBar / claimThreshold;
                float clampedFill = Mathf.Clamp01(p0Fill);
                if (p0FillImage != null)
                {
                    p0FillImage.fillAmount = clampedFill;
                    if (useGradient)
                        p0FillImage.color = p0Gradient.Evaluate(clampedFill);
                }
                if (p1FillImage != null) p1FillImage.fillAmount = 0f;
            }
            else if (node.claimBar < 0)
            {
                float p1Fill = (float)(-node.claimBar) / claimThreshold;
                float clampedFill = Mathf.Clamp01(p1Fill);
                if (p1FillImage != null)
                {
                    p1FillImage.fillAmount = clampedFill;
                    if (useGradient)
                        p1FillImage.color = p1Gradient.Evaluate(clampedFill);
                }
                if (p0FillImage != null) p0FillImage.fillAmount = 0f;
            }
            else
            {
                if (p0FillImage != null) p0FillImage.fillAmount = 0f;
                if (p1FillImage != null) p1FillImage.fillAmount = 0f;
            }

            // Visibility: show when claim bar changes
            if (node.claimBar != lastClaimBar)
            {
                showTimer = showDuration;
                targetAlpha = 1f;
                lastClaimBar = node.claimBar;
            }

            if (showTimer > 0f)
            {
                showTimer -= Time.deltaTime;
                if (showTimer <= 0f)
                {
                    showTimer = 0f;
                    targetAlpha = 0f;
                }
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(
                    canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
            }
        }

        public void ForceShow(float duration)
        {
            showTimer = duration;
            targetAlpha = 1f;
        }

        private void LateUpdate()
        {
            if (!initialized || worldCanvas == null || worldCanvas.renderMode != RenderMode.WorldSpace)
                return;

            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera != null)
                worldCanvas.transform.rotation = viewCamera.transform.rotation;
        }

        // Unity serializes a newly added Gradient as white-to-white, not empty.
        // The base node prefab carries that untouched default for both players.
        private static bool NeedsPlayerGradient(Gradient gradient)
        {
            if (gradient == null) return true;
            GradientColorKey[] keys = gradient.colorKeys;
            if (keys.Length == 0) return true;
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i].color != Color.white) return false;
            }
            return true;
        }

        private void EnsureGradients()
        {
            if (NeedsPlayerGradient(p0Gradient))
            {
                p0Gradient = new Gradient();
                GradientColorKey[] keys = new GradientColorKey[2];
                keys[0] = new GradientColorKey(new Color(0.6f, 0.75f, 1f), 0f);
                keys[1] = new GradientColorKey(new Color(0.2f, 0.4f, 1f), 1f);
                GradientAlphaKey[] alpha = new GradientAlphaKey[2];
                alpha[0] = new GradientAlphaKey(1f, 0f);
                alpha[1] = new GradientAlphaKey(1f, 1f);
                p0Gradient.SetKeys(keys, alpha);
            }

            if (NeedsPlayerGradient(p1Gradient))
            {
                p1Gradient = new Gradient();
                GradientColorKey[] keys = new GradientColorKey[2];
                keys[0] = new GradientColorKey(new Color(1f, 0.7f, 0.6f), 0f);
                keys[1] = new GradientColorKey(new Color(1f, 0.2f, 0.2f), 1f);
                GradientAlphaKey[] alpha = new GradientAlphaKey[2];
                alpha[0] = new GradientAlphaKey(1f, 0f);
                alpha[1] = new GradientAlphaKey(1f, 1f);
                p1Gradient.SetKeys(keys, alpha);
            }
        }
    }
}
