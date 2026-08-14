using UnityEngine;
using UnityEngine.UI;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    public class NodeClaimBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Slider slider;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Gradient")]
        [SerializeField] private Gradient claimGradient;

        [Header("Visibility")]
        [SerializeField] private float showDuration = 2f;
        [SerializeField] private float fadeSpeed = 3f;

        private SimulationState simState;
        private int nodeID;
        private bool initialized = false;
        private int lastClaimBar;
        private float showTimer;
        private float targetAlpha;

        public void Initialize(SimulationState state, int id)
        {
            simState = state;
            nodeID = id;
            initialized = true;

            if (claimGradient == null)
            {
                claimGradient = CreateDefaultGradient();
            }

            slider.minValue = 0f;
            slider.maxValue = 1f;

            lastClaimBar = state.nodes[nodeID].claimBar;
            showTimer = 0f;
            targetAlpha = 0f;

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
        }

        private void Update()
        {
            if (!initialized) return;

            NodeData node = simState.nodes[nodeID];

            // Update slider visual
            float normalized = (node.claimBar + 10000f) / 20000f;
            slider.value = normalized;

            if (fillImage != null && claimGradient != null)
            {
                fillImage.color = claimGradient.Evaluate(normalized);
            }

            // ONLY trigger visibility when claim bar value actually changes
            if (node.claimBar != lastClaimBar)
            {
                showTimer = showDuration;
                targetAlpha = 1f;
                lastClaimBar = node.claimBar;
            }

            // Timer countdown — nothing resets it except the bar changing
            if (showTimer > 0f)
            {
                showTimer -= Time.deltaTime;
                if (showTimer <= 0f)
                {
                    showTimer = 0f;
                    targetAlpha = 0f;
                }
            }

            // Fade toward target
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
            }
        }

        public void ForceShow(float duration)
        {
            showTimer = duration;
            targetAlpha = 1f;
        }

        private Gradient CreateDefaultGradient()
        {
            Gradient g = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[3];
            colorKeys[0] = new GradientColorKey(new Color(1f, 0.3f, 0.3f), 0f);
            colorKeys[1] = new GradientColorKey(new Color(0.5f, 0.5f, 0.45f), 0.5f);
            colorKeys[2] = new GradientColorKey(new Color(0.3f, 0.5f, 1f), 1f);

            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1f, 0f);
            alphaKeys[1] = new GradientAlphaKey(1f, 1f);

            g.SetKeys(colorKeys, alphaKeys);
            return g;
        }
    }
}