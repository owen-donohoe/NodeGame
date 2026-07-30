using UnityEngine;
using UnityEngine.UI;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    public class VillagerHealthRing : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundRing;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Colors")]
        [SerializeField] private Color fullHealthColor = new Color(0.3f, 1f, 0.4f);
        [SerializeField] private Color midHealthColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private Color lowHealthColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private Color backgroundRingColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);

        [Header("Thresholds")]
        [SerializeField] private float lowHealthPercent = 0.3f;
        [SerializeField] private float midHealthPercent = 0.6f;

        [Header("Visibility")]
        [SerializeField] private float showDuration = 2f;
        [SerializeField] private float fadeSpeed = 3f;

        private SimulationState simState;
        private int villagerID;
        private bool initialized = false;
        private int lastHP;
        private float showTimer;
        private float targetAlpha;

        public void Initialize(SimulationState state, int id)
        {
            simState = state;
            villagerID = id;
            initialized = true;

            if (fillImage != null)
            {
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Radial360;
                fillImage.fillOrigin = (int)Image.Origin360.Top;
                fillImage.fillClockwise = true;
            }

            if (backgroundRing != null)
            {
                backgroundRing.color = backgroundRingColor;
            }

            lastHP = state.villagers[id].hp;
            showTimer = 0f;
            targetAlpha = 0f;

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
        }

        private void Update()
        {
            if (!initialized) return;
            if (villagerID >= simState.villagers.Length) return;

            VillagerData villager = simState.villagers[villagerID];

            if (villager.state == VillagerState.Dead || villager.isConsumed)
            {
                if (canvasGroup != null)
                    canvasGroup.alpha = 0f;
                return;
            }

            // Calculate fill
            float hpFraction = (float)villager.hp / (float)villager.maxHP;
            if (fillImage != null)
            {
                fillImage.fillAmount = hpFraction;
                fillImage.color = GetHealthColor(hpFraction);
            }

            // ONLY trigger visibility when HP actually changes
            if (villager.hp != lastHP)
            {
                showTimer = showDuration;
                targetAlpha = 1f;
                lastHP = villager.hp;
            }

            // Timer countdown — runs independently, nothing resets it except an HP change
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

        private Color GetHealthColor(float fraction)
        {
            if (fraction <= lowHealthPercent)
                return lowHealthColor;
            if (fraction <= midHealthPercent)
                return Color.Lerp(lowHealthColor, midHealthColor, (fraction - lowHealthPercent) / (midHealthPercent - lowHealthPercent));
            return Color.Lerp(midHealthColor, fullHealthColor, (fraction - midHealthPercent) / (1f - midHealthPercent));
        }
    }
}